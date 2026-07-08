using System.Security.Cryptography;
using System.Text;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;
using Microsoft.Data.Sqlite;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Persistence;

/// <summary>
/// SQLite-реализация <see cref="IEventStore"/>. Единая таблица событий (single-table inheritance,
/// nullable-колонки на подтипы) + дедуп-словари <c>sql_text</c>/<c>attachment</c> + дочерние таблицы
/// для коллекций (параметры, строки ошибок, статистика по таблицам). Всё локально; транзакция на файл.
/// Этап 1 — корректность и обратимость (round-trip), без микрооптимизаций.
/// </summary>
public sealed class EventStoreService : IEventStore
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const int SchemaVersion = 1;

    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public EventStoreService(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        Exec("PRAGMA journal_mode=WAL;");
        Exec("PRAGMA foreign_keys=ON;");
        EnsureSchema();
    }

    // ---------------------------------------------------------------- schema

    private void EnsureSchema()
    {
        Exec(@"
CREATE TABLE IF NOT EXISTS files (
    hash TEXT PRIMARY KEY, name TEXT NOT NULL, path TEXT NOT NULL, size INTEGER NOT NULL,
    start_ts INTEGER NOT NULL, end_ts INTEGER NOT NULL, event_count INTEGER NOT NULL,
    imported_ts INTEGER NOT NULL);

CREATE TABLE IF NOT EXISTS sql_text (
    id INTEGER PRIMARY KEY AUTOINCREMENT, sha TEXT NOT NULL UNIQUE, text TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS attachment (
    id INTEGER PRIMARY KEY AUTOINCREMENT, sha TEXT NOT NULL UNIQUE,
    att_id INTEGER NOT NULL, db_path TEXT NOT NULL, user TEXT NOT NULL, role TEXT NOT NULL,
    charset TEXT NOT NULL, protocol TEXT NOT NULL, address TEXT NOT NULL, port INTEGER NOT NULL,
    process_path TEXT, process_id INTEGER);

CREATE TABLE IF NOT EXISTS event (
    seq INTEGER PRIMARY KEY AUTOINCREMENT,
    file_hash TEXT NOT NULL REFERENCES files(hash) ON DELETE CASCADE,
    ts INTEGER NOT NULL, trace_id INTEGER NOT NULL, hex_trace_id TEXT NOT NULL, event_type INTEGER NOT NULL,
    attachment_ref INTEGER REFERENCES attachment(id), session_id INTEGER, sql_ref INTEGER REFERENCES sql_text(id),
    statement_id INTEGER,
    txn_present INTEGER, txn_id INTEGER, txn_isolation TEXT, txn_consistency TEXT, txn_lock TEXT, txn_access TEXT,
    restart_count INTEGER, procedure_name TEXT,
    trigger_name TEXT, trigger_table TEXT, trigger_timing TEXT, trigger_event TEXT,
    component TEXT,
    perf_present INTEGER, perf_execute_ms INTEGER, perf_fetch INTEGER, perf_read INTEGER, perf_write INTEGER, perf_mark INTEGER,
    perf_table_state INTEGER NOT NULL DEFAULT 0);

CREATE TABLE IF NOT EXISTS sql_parameter (
    event_seq INTEGER NOT NULL REFERENCES event(seq) ON DELETE CASCADE,
    ord INTEGER NOT NULL, name TEXT NOT NULL, dtype TEXT NOT NULL, value TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS error_line (
    event_seq INTEGER NOT NULL REFERENCES event(seq) ON DELETE CASCADE,
    ord INTEGER NOT NULL, code INTEGER NOT NULL, message TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS perf_table_item (
    event_seq INTEGER NOT NULL REFERENCES event(seq) ON DELETE CASCADE,
    ord INTEGER NOT NULL, table_name TEXT NOT NULL,
    natural_count INTEGER NOT NULL, index_count INTEGER NOT NULL, update_count INTEGER NOT NULL,
    insert_count INTEGER NOT NULL, delete_count INTEGER NOT NULL, backout_count INTEGER NOT NULL,
    purge_count INTEGER NOT NULL, expunge_count INTEGER NOT NULL);

CREATE INDEX IF NOT EXISTS ix_event_file ON event(file_hash);
CREATE INDEX IF NOT EXISTS ix_event_ts ON event(ts);
CREATE INDEX IF NOT EXISTS ix_event_type ON event(event_type);
CREATE INDEX IF NOT EXISTS ix_param_event ON sql_parameter(event_seq);
CREATE INDEX IF NOT EXISTS ix_errline_event ON error_line(event_seq);
CREATE INDEX IF NOT EXISTS ix_perfitem_event ON perf_table_item(event_seq);");

        Exec($"PRAGMA user_version={SchemaVersion};");
    }

    // ---------------------------------------------------------------- write

    public void WriteFile(TraceFileInfoModel file, IEnumerable<EventBase> events)
    {
        using var tx = _connection.BeginTransaction();

        // Замена: убираем прежний файл (CASCADE снимет его события и дочерние строки).
        using (var del = _connection.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM files WHERE hash=$h;";
            del.Parameters.AddWithValue("$h", file.FileHash);
            del.ExecuteNonQuery();
        }

        // Родительскую строку файла вставляем ДО событий (на неё ссылается event.file_hash по FK).
        // event_count проставим фактическим после цикла.
        using (var ins = _connection.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = @"INSERT INTO files(hash,name,path,size,start_ts,end_ts,event_count,imported_ts)
                                VALUES($h,$n,$p,$s,$st,$et,0,$i);";
            ins.Parameters.AddWithValue("$h", file.FileHash);
            ins.Parameters.AddWithValue("$n", file.FileName);
            ins.Parameters.AddWithValue("$p", file.FilePath);
            ins.Parameters.AddWithValue("$s", file.FileSize);
            ins.Parameters.AddWithValue("$st", file.StartTrace.Ticks);
            ins.Parameters.AddWithValue("$et", file.EndTrace.Ticks);
            ins.Parameters.AddWithValue("$i", DateTime.UtcNow.Ticks);
            ins.ExecuteNonQuery();
        }

        long count = 0;
        // Кэш дедупа в пределах записи (плюс UNIQUE в БД обеспечивает кросс-файловый дедуп).
        var sqlCache = new Dictionary<string, long>(StringComparer.Ordinal);
        var attCache = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var ev in events)
        {
            var seq = InsertEvent(tx, file.FileHash, ev, sqlCache, attCache);
            InsertChildren(tx, seq, ev);
            count++;
        }

        using (var upd = _connection.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = "UPDATE files SET event_count=$c WHERE hash=$h;";
            upd.Parameters.AddWithValue("$c", count);
            upd.Parameters.AddWithValue("$h", file.FileHash);
            upd.ExecuteNonQuery();
        }

        tx.Commit();
        Logger.Info("EventStore: wrote {Count} event(s) for file {Name}", count, file.FileName);
    }

    private long InsertEvent(SqliteTransaction tx, string fileHash, EventBase ev,
        Dictionary<string, long> sqlCache, Dictionary<string, long> attCache)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO event(file_hash,ts,trace_id,hex_trace_id,event_type,attachment_ref,session_id,sql_ref,statement_id,
    txn_present,txn_id,txn_isolation,txn_consistency,txn_lock,txn_access,restart_count,procedure_name,
    trigger_name,trigger_table,trigger_timing,trigger_event,component,
    perf_present,perf_execute_ms,perf_fetch,perf_read,perf_write,perf_mark,perf_table_state)
VALUES($file,$ts,$tid,$hex,$type,$att,$sess,$sql,$stmt,
    $txp,$txid,$txiso,$txcons,$txlock,$txacc,$rc,$proc,
    $trn,$trt,$trtm,$tre,$comp,
    $pp,$pe,$pf,$pr,$pw,$pm,$pts);";

        void P(string n, object? v) => cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);

        P("$file", fileHash);
        P("$ts", ev.Timestamp.Ticks);
        P("$tid", ev.TraceId);
        P("$hex", ev.HexTraceId);
        P("$type", (int)ev.EventType);

        // attachment / session / sql / statement / transaction / type-specific
        long? attRef = null; int? sessionId = null; long? sqlRef = null; long? statementId = null;
        int? txnPresent = null; long? txnId = null;
        string? txIso = null, txCons = null, txLock = null, txAcc = null;
        int? restart = null; string? procName = null;
        string? trName = null, trTable = null, trTiming = null, trEvent = null, component = null;
        int? perfPresent = null; int? pe = null, pf = null, pr = null, pw = null, pm = null;
        int perfTableState = 0;

        switch (ev)
        {
            case TraceInitEvent e: sessionId = e.Session.SessionId; break;
            case TraceFinishEvent e: sessionId = e.Session.SessionId; break;
            case AttachDatabaseEvent e: attRef = InternAttachment(tx, e.Attachment, attCache); break;
            case DetachDatabaseEvent e: attRef = InternAttachment(tx, e.Attachment, attCache); break;

            case StatementEventBase e:
                attRef = InternAttachment(tx, e.Attachment, attCache);
                sqlRef = InternSql(tx, e.Sql, sqlCache);
                statementId = e.StatementId;
                (txnPresent, txnId, txIso, txCons, txLock, txAcc) = Txn(e.Transaction);
                if (e is StatementRestartEvent r) restart = r.RestartCount;
                (perfPresent, pe, pf, pr, pw, pm, perfTableState) = Perf(e);
                break;

            case ProcedureEventBase e:
                attRef = InternAttachment(tx, e.Attachment, attCache);
                procName = e.ProcedureName;
                (txnPresent, txnId, txIso, txCons, txLock, txAcc) = Txn(e.Transaction);
                (perfPresent, pe, pf, pr, pw, pm, perfTableState) = Perf(e);
                break;

            case TriggerEventBase e:
                attRef = InternAttachment(tx, e.Attachment, attCache);
                trName = e.TriggerName; trTable = e.Table; trTiming = e.Timing; trEvent = e.Event;
                (txnPresent, txnId, txIso, txCons, txLock, txAcc) = Txn(e.Transaction);
                (perfPresent, pe, pf, pr, pw, pm, perfTableState) = Perf(e);
                break;

            case ErrorEvent e:
                attRef = InternAttachment(tx, e.Attachment, attCache);
                component = e.Component;
                break;

            default:
                throw new NotSupportedException($"Event type {ev.GetType().Name} is not supported by the store.");
        }

        P("$att", attRef); P("$sess", sessionId); P("$sql", sqlRef); P("$stmt", statementId);
        P("$txp", txnPresent); P("$txid", txnId); P("$txiso", txIso); P("$txcons", txCons);
        P("$txlock", txLock); P("$txacc", txAcc); P("$rc", restart); P("$proc", procName);
        P("$trn", trName); P("$trt", trTable); P("$trtm", trTiming); P("$tre", trEvent); P("$comp", component);
        P("$pp", perfPresent); P("$pe", pe); P("$pf", pf); P("$pr", pr); P("$pw", pw); P("$pm", pm);
        P("$pts", perfTableState);

        cmd.ExecuteNonQuery();
        return LastRowId(tx);
    }

    private long LastRowId(SqliteTransaction tx)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT last_insert_rowid();";
        return (long)cmd.ExecuteScalar()!;
    }

    private void InsertChildren(SqliteTransaction tx, long seq, EventBase ev)
    {
        switch (ev)
        {
            case StatementEventBase e: InsertParameters(tx, seq, e.Parameters); InsertPerfTable(tx, seq, e); break;
            case ProcedureEventBase e: InsertParameters(tx, seq, e.Parameters); InsertPerfTable(tx, seq, e); break;
            case TriggerEventBase e: InsertPerfTable(tx, seq, e); break;
            case ErrorEvent e: InsertErrorLines(tx, seq, e.Errors); break;
        }
    }

    private void InsertParameters(SqliteTransaction tx, long seq, IReadOnlyList<SqlParameters> parameters)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO sql_parameter(event_seq,ord,name,dtype,value) VALUES($s,$o,$n,$d,$v);";
            cmd.Parameters.AddWithValue("$s", seq);
            cmd.Parameters.AddWithValue("$o", i);
            cmd.Parameters.AddWithValue("$n", p.Name);
            cmd.Parameters.AddWithValue("$d", p.Dtype);
            cmd.Parameters.AddWithValue("$v", p.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private void InsertErrorLines(SqliteTransaction tx, long seq, IReadOnlyList<ErrorLines> errors)
    {
        for (var i = 0; i < errors.Count; i++)
        {
            var e = errors[i];
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO error_line(event_seq,ord,code,message) VALUES($s,$o,$c,$m);";
            cmd.Parameters.AddWithValue("$s", seq);
            cmd.Parameters.AddWithValue("$o", i);
            cmd.Parameters.AddWithValue("$c", e.ErrorCode);
            cmd.Parameters.AddWithValue("$m", e.Message);
            cmd.ExecuteNonQuery();
        }
    }

    private void InsertPerfTable(SqliteTransaction tx, long seq, EventBase ev)
    {
        var items = GetPerfTable(ev)?.Items;
        if (items is null) return;
        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO perf_table_item(event_seq,ord,table_name,natural_count,index_count,
                update_count,insert_count,delete_count,backout_count,purge_count,expunge_count)
                VALUES($s,$o,$tn,$na,$ix,$up,$in,$de,$ba,$pu,$ex);";
            cmd.Parameters.AddWithValue("$s", seq);
            cmd.Parameters.AddWithValue("$o", i);
            cmd.Parameters.AddWithValue("$tn", it.TableName);
            cmd.Parameters.AddWithValue("$na", it.NaturalCount);
            cmd.Parameters.AddWithValue("$ix", it.IndexCount);
            cmd.Parameters.AddWithValue("$up", it.UpdateCount);
            cmd.Parameters.AddWithValue("$in", it.InsertCount);
            cmd.Parameters.AddWithValue("$de", it.DeleteCount);
            cmd.Parameters.AddWithValue("$ba", it.BackoutCount);
            cmd.Parameters.AddWithValue("$pu", it.PurgeCount);
            cmd.Parameters.AddWithValue("$ex", it.ExpungeCount);
            cmd.ExecuteNonQuery();
        }
    }

    private long InternSql(SqliteTransaction tx, string sql, Dictionary<string, long> cache)
    {
        var sha = Sha(sql);
        if (cache.TryGetValue(sha, out var cached)) return cached;

        using (var ins = _connection.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = "INSERT OR IGNORE INTO sql_text(sha,text) VALUES($sha,$t);";
            ins.Parameters.AddWithValue("$sha", sha);
            ins.Parameters.AddWithValue("$t", sql);
            ins.ExecuteNonQuery();
        }

        using var sel = _connection.CreateCommand();
        sel.Transaction = tx;
        sel.CommandText = "SELECT id FROM sql_text WHERE sha=$sha;";
        sel.Parameters.AddWithValue("$sha", sha);
        var id = (long)sel.ExecuteScalar()!;
        cache[sha] = id;
        return id;
    }

    private long InternAttachment(SqliteTransaction tx, AttachmentInfo a, Dictionary<string, long> cache)
    {
        var key = $"{a.AttachmentId}{a.DatabasePath}{a.User}{a.Role}{a.Charset}{a.Protocol}{a.Address}{a.Port}{a.ProcessPath}{a.ProcessId}";
        var sha = Sha(key);
        if (cache.TryGetValue(sha, out var cached)) return cached;

        using (var ins = _connection.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = @"INSERT OR IGNORE INTO attachment(sha,att_id,db_path,user,role,charset,protocol,address,port,process_path,process_id)
                                VALUES($sha,$ai,$db,$u,$r,$c,$pr,$ad,$po,$pp,$pi);";
            ins.Parameters.AddWithValue("$sha", sha);
            ins.Parameters.AddWithValue("$ai", a.AttachmentId);
            ins.Parameters.AddWithValue("$db", a.DatabasePath);
            ins.Parameters.AddWithValue("$u", a.User);
            ins.Parameters.AddWithValue("$r", a.Role);
            ins.Parameters.AddWithValue("$c", a.Charset);
            ins.Parameters.AddWithValue("$pr", a.Protocol);
            ins.Parameters.AddWithValue("$ad", a.Address);
            ins.Parameters.AddWithValue("$po", a.Port);
            ins.Parameters.AddWithValue("$pp", (object?)a.ProcessPath ?? DBNull.Value);
            ins.Parameters.AddWithValue("$pi", (object?)a.ProcessId ?? DBNull.Value);
            ins.ExecuteNonQuery();
        }

        using var sel = _connection.CreateCommand();
        sel.Transaction = tx;
        sel.CommandText = "SELECT id FROM attachment WHERE sha=$sha;";
        sel.Parameters.AddWithValue("$sha", sha);
        var id = (long)sel.ExecuteScalar()!;
        cache[sha] = id;
        return id;
    }

    private static (int present, long? id, string? iso, string? cons, string? lockm, string? acc) Txn(TransactionInfo? t)
    {
        if (t is null) return (0, null, null, null, null, null);
        return (1, t.TransactionId, t.IsolationLevel, t.ConsistencyMode, t.LockMode, t.AccessMode);
    }

    private static (int present, int? ms, int? f, int? r, int? w, int? m, int perfTableState) Perf(EventBase ev)
    {
        var perf = GetPerf(ev);
        if (perf is null) return (0, null, null, null, null, null, 0);
        var table = GetPerfTable(ev);
        var state = table is null ? 0 : table.Items is null ? 1 : 2;
        return (1, perf.ExecuteMs, perf.FetchCount, perf.ReadCount, perf.WriteCount, perf.MarkCount, state);
    }

    private static PerformanceInfo? GetPerf(EventBase ev) => ev switch
    {
        StatementFinishEvent e => e.Performance,
        FailedStatementFinishEvent e => e.Performance,
        ProcedureFinishEvent e => e.Performance,
        FailedProcedureFinishEvent e => e.Performance,
        TriggerFinishEvent e => e.Performance,
        FailedTriggerFinishEvent e => e.Performance,
        _ => null
    };

    private static PerformanceTable? GetPerfTable(EventBase ev) => ev switch
    {
        StatementFinishEvent e => e.PerformanceTable,
        FailedStatementFinishEvent e => e.PerformanceTable,
        ProcedureFinishEvent e => e.PerformanceTable,
        FailedProcedureFinishEvent e => e.PerformanceTable,
        TriggerFinishEvent e => e.PerformanceTable,
        FailedTriggerFinishEvent e => e.PerformanceTable,
        _ => null
    };

    // ---------------------------------------------------------------- read

    public bool ContainsFile(string fileHash)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM files WHERE hash=$h LIMIT 1;";
        cmd.Parameters.AddWithValue("$h", fileHash);
        return cmd.ExecuteScalar() is not null;
    }

    public IReadOnlyList<EventBase> ReadFile(string fileHash)
    {
        var result = new List<EventBase>();
        var attCache = new Dictionary<long, AttachmentInfo>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM event WHERE file_hash=$h ORDER BY seq;";
        cmd.Parameters.AddWithValue("$h", fileHash);
        using var reader = cmd.ExecuteReader();
        var seqs = new List<long>();
        var rows = new List<Row>();
        while (reader.Read())
        {
            var row = ReadRow(reader);
            rows.Add(row);
            seqs.Add(row.Seq);
        }

        // Дочерние коллекции — пакетно (без N+1).
        var paramsBySeq = LoadParameters(fileHash);
        var errorsBySeq = LoadErrorLines(fileHash);
        var perfBySeq = LoadPerfItems(fileHash);

        foreach (var row in rows)
            result.Add(BuildEvent(row, attCache,
                paramsBySeq.GetValueOrDefault(row.Seq),
                errorsBySeq.GetValueOrDefault(row.Seq),
                perfBySeq.GetValueOrDefault(row.Seq)));

        return result;
    }

    public IEnumerable<EventBase> Query(DateTime? from = null, DateTime? to = null)
    {
        var attCache = new Dictionary<long, AttachmentInfo>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM event WHERE ($f IS NULL OR ts>=$f) AND ($t IS NULL OR ts<=$t) ORDER BY ts, seq;";
        cmd.Parameters.AddWithValue("$f", (object?)from?.Ticks ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", (object?)to?.Ticks ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = ReadRow(reader);
            yield return BuildEvent(row, attCache,
                LoadParametersFor(row.Seq), LoadErrorLinesFor(row.Seq), LoadPerfItemsFor(row.Seq));
        }
    }

    // ---------------------------------------------------------------- manage / stats

    public IReadOnlyList<TraceFileInfoModel> ListFiles()
    {
        var list = new List<TraceFileInfoModel>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT hash,name,path,size,start_ts,end_ts,event_count FROM files ORDER BY start_ts;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TraceFileInfoModel(r.GetString(1), r.GetString(2), r.GetInt64(3),
                new DateTime(r.GetInt64(4)), new DateTime(r.GetInt64(5)), r.GetInt64(6), r.GetString(0)));
        return list;
    }

    public void DeleteFile(string fileHash)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM files WHERE hash=$h;"; // CASCADE снимет events/дочерние
        cmd.Parameters.AddWithValue("$h", fileHash);
        cmd.ExecuteNonQuery();
    }

    public void Clear()
    {
        Exec("DELETE FROM perf_table_item; DELETE FROM error_line; DELETE FROM sql_parameter; " +
             "DELETE FROM event; DELETE FROM files; DELETE FROM sql_text; DELETE FROM attachment;");
    }

    public EventStoreStatistics GetStatistics()
    {
        long Scalar(string sql)
        {
            using var c = _connection.CreateCommand();
            c.CommandText = sql;
            var v = c.ExecuteScalar();
            return v is null or DBNull ? 0 : Convert.ToInt64(v);
        }

        var files = (int)Scalar("SELECT COUNT(*) FROM files;");
        var events = Scalar("SELECT COUNT(*) FROM event;");
        var sql = Scalar("SELECT COUNT(*) FROM sql_text;");
        var att = Scalar("SELECT COUNT(*) FROM attachment;");
        var raw = Scalar("SELECT COALESCE(SUM(size),0) FROM files;");

        DateTime? start = null, end = null;
        using (var c = _connection.CreateCommand())
        {
            c.CommandText = "SELECT MIN(ts), MAX(ts) FROM event;";
            using var r = c.ExecuteReader();
            if (r.Read() && !r.IsDBNull(0))
            {
                start = new DateTime(r.GetInt64(0));
                end = new DateTime(r.GetInt64(1));
            }
        }

        long dbSize = 0;
        try { dbSize = new FileInfo(_dbPath).Length; } catch { /* файл ещё не сброшен — не критично */ }

        return new EventStoreStatistics
        {
            FileCount = files, EventCount = events, UniqueSqlCount = sql, UniqueAttachmentCount = att,
            RangeStart = start, RangeEnd = end, DbSizeBytes = dbSize, RawSizeBytes = raw
        };
    }

    // ---------------------------------------------------------------- row → event

    private sealed record Row(
        long Seq, long Ts, int TraceId, string Hex, int Type,
        long? AttRef, int? SessionId, long? SqlRef, long? StatementId,
        int? TxnPresent, long? TxnId, string? TxIso, string? TxCons, string? TxLock, string? TxAcc,
        int? Restart, string? Proc, string? TrName, string? TrTable, string? TrTiming, string? TrEvent,
        string? Component, int? PerfPresent, int? Pe, int? Pf, int? Pr, int? Pw, int? Pm, int PerfTableState);

    private static Row ReadRow(SqliteDataReader r)
    {
        int O(string n) => r.GetOrdinal(n);
        long? L(string n) => r.IsDBNull(O(n)) ? null : r.GetInt64(O(n));
        int? I(string n) => r.IsDBNull(O(n)) ? null : r.GetInt32(O(n));
        string? S(string n) => r.IsDBNull(O(n)) ? null : r.GetString(O(n));
        return new Row(
            r.GetInt64(O("seq")), r.GetInt64(O("ts")), r.GetInt32(O("trace_id")), r.GetString(O("hex_trace_id")),
            r.GetInt32(O("event_type")), L("attachment_ref"), I("session_id"), L("sql_ref"), L("statement_id"),
            I("txn_present"), L("txn_id"), S("txn_isolation"), S("txn_consistency"), S("txn_lock"), S("txn_access"),
            I("restart_count"), S("procedure_name"), S("trigger_name"), S("trigger_table"), S("trigger_timing"),
            S("trigger_event"), S("component"), I("perf_present"), I("perf_execute_ms"), I("perf_fetch"),
            I("perf_read"), I("perf_write"), I("perf_mark"), r.GetInt32(O("perf_table_state")));
    }

    private EventBase BuildEvent(Row row, Dictionary<long, AttachmentInfo> attCache,
        List<SqlParameters>? parameters, List<ErrorLines>? errors, List<PerformanceTableItem>? perfItems)
    {
        var type = (EventType)row.Type;
        var ts = new DateTime(row.Ts);
        AttachmentInfo Att() => LoadAttachment(row.AttRef!.Value, attCache);
        TransactionInfo? Txn() => row.TxnPresent == 1
            ? new TransactionInfo { TransactionId = row.TxnId, IsolationLevel = row.TxIso, ConsistencyMode = row.TxCons, LockMode = row.TxLock, AccessMode = row.TxAcc }
            : null;
        var prm = (IReadOnlyList<SqlParameters>)(parameters ?? new List<SqlParameters>());
        PerformanceInfo Perf() => new() { ExecuteMs = row.Pe!.Value, FetchCount = row.Pf!.Value, ReadCount = row.Pr!.Value, WriteCount = row.Pw!.Value, MarkCount = row.Pm!.Value };
        PerformanceTable? PerfTable() => row.PerfTableState switch
        {
            1 => new PerformanceTable { Items = null },
            2 => new PerformanceTable { Items = perfItems ?? new List<PerformanceTableItem>() },
            _ => null
        };

        return type switch
        {
            EventType.TraceInit => new TraceInitEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Session = new TraceSessionInfo { SessionId = row.SessionId!.Value } },
            EventType.TraceFinish => new TraceFinishEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Session = new TraceSessionInfo { SessionId = row.SessionId!.Value } },
            EventType.AttachDatabase => new AttachDatabaseEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att() },
            EventType.DetachDatabase => new DetachDatabaseEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att() },
            EventType.ExecuteStatementStart => new StatementStartEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn(), StatementId = row.StatementId, Sql = LoadSql(row.SqlRef!.Value), Parameters = prm },
            EventType.ExecuteStatementRestart => new StatementRestartEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn(), StatementId = row.StatementId, Sql = LoadSql(row.SqlRef!.Value), Parameters = prm, RestartCount = row.Restart },
            EventType.ExecuteStatementFinish => new StatementFinishEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn(), StatementId = row.StatementId, Sql = LoadSql(row.SqlRef!.Value), Parameters = prm, Performance = Perf(), PerformanceTable = PerfTable() },
            EventType.FailedExecuteStatementFinish => new FailedStatementFinishEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn(), StatementId = row.StatementId, Sql = LoadSql(row.SqlRef!.Value), Parameters = prm, Performance = Perf(), PerformanceTable = PerfTable() },
            EventType.ExecuteProcedureStart => new ProcedureStartEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn()!, ProcedureName = row.Proc!, Parameters = prm },
            EventType.ExecuteProcedureFinish => new ProcedureFinishEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn()!, ProcedureName = row.Proc!, Parameters = prm, Performance = Perf(), PerformanceTable = PerfTable() },
            EventType.FailedExecuteProcedureFinish => new FailedProcedureFinishEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn()!, ProcedureName = row.Proc!, Parameters = prm, Performance = Perf(), PerformanceTable = PerfTable() },
            EventType.ExecuteTriggerStart => new TriggerStartEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn()!, TriggerName = row.TrName!, Table = row.TrTable, Timing = row.TrTiming, Event = row.TrEvent! },
            EventType.ExecuteTriggerFinish => new TriggerFinishEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn()!, TriggerName = row.TrName!, Table = row.TrTable, Timing = row.TrTiming, Event = row.TrEvent!, Performance = Perf(), PerformanceTable = PerfTable() },
            EventType.FailedExecuteTriggerFinish => new FailedTriggerFinishEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn()!, TriggerName = row.TrName!, Table = row.TrTable, Timing = row.TrTiming, Event = row.TrEvent!, Performance = Perf(), PerformanceTable = PerfTable() },
            EventType.Error => new ErrorEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Component = row.Component!, Errors = (IReadOnlyList<ErrorLines>)(errors ?? new List<ErrorLines>()) },
            _ => throw new NotSupportedException($"EventType {type} is not supported by the store.")
        };
    }

    // ---------------------------------------------------------------- loaders

    private string LoadSql(long id)
    {
        using var c = _connection.CreateCommand();
        c.CommandText = "SELECT text FROM sql_text WHERE id=$id;";
        c.Parameters.AddWithValue("$id", id);
        return (string)c.ExecuteScalar()!;
    }

    private AttachmentInfo LoadAttachment(long id, Dictionary<long, AttachmentInfo> cache)
    {
        if (cache.TryGetValue(id, out var a)) return a;
        using var c = _connection.CreateCommand();
        c.CommandText = "SELECT att_id,db_path,user,role,charset,protocol,address,port,process_path,process_id FROM attachment WHERE id=$id;";
        c.Parameters.AddWithValue("$id", id);
        using var r = c.ExecuteReader();
        r.Read();
        var info = new AttachmentInfo
        {
            AttachmentId = r.GetInt64(0), DatabasePath = r.GetString(1), User = r.GetString(2), Role = r.GetString(3),
            Charset = r.GetString(4), Protocol = r.GetString(5), Address = r.GetString(6), Port = r.GetInt32(7),
            ProcessPath = r.IsDBNull(8) ? null : r.GetString(8), ProcessId = r.IsDBNull(9) ? null : r.GetInt32(9)
        };
        cache[id] = info;
        return info;
    }

    private Dictionary<long, List<SqlParameters>> LoadParameters(string fileHash)
    {
        var map = new Dictionary<long, List<SqlParameters>>();
        using var c = _connection.CreateCommand();
        c.CommandText = @"SELECT p.event_seq,p.name,p.dtype,p.value FROM sql_parameter p
                          JOIN event e ON e.seq=p.event_seq WHERE e.file_hash=$h ORDER BY p.event_seq,p.ord;";
        c.Parameters.AddWithValue("$h", fileHash);
        using var r = c.ExecuteReader();
        while (r.Read())
            (map.TryGetValue(r.GetInt64(0), out var l) ? l : map[r.GetInt64(0)] = new())
                .Add(new SqlParameters { Name = r.GetString(1), Dtype = r.GetString(2), Value = r.GetString(3) });
        return map;
    }

    private Dictionary<long, List<ErrorLines>> LoadErrorLines(string fileHash)
    {
        var map = new Dictionary<long, List<ErrorLines>>();
        using var c = _connection.CreateCommand();
        c.CommandText = @"SELECT x.event_seq,x.code,x.message FROM error_line x
                          JOIN event e ON e.seq=x.event_seq WHERE e.file_hash=$h ORDER BY x.event_seq,x.ord;";
        c.Parameters.AddWithValue("$h", fileHash);
        using var r = c.ExecuteReader();
        while (r.Read())
            (map.TryGetValue(r.GetInt64(0), out var l) ? l : map[r.GetInt64(0)] = new())
                .Add(new ErrorLines { ErrorCode = r.GetInt32(1), Message = r.GetString(2) });
        return map;
    }

    private Dictionary<long, List<PerformanceTableItem>> LoadPerfItems(string fileHash)
    {
        var map = new Dictionary<long, List<PerformanceTableItem>>();
        using var c = _connection.CreateCommand();
        c.CommandText = @"SELECT i.event_seq,i.table_name,i.natural_count,i.index_count,i.update_count,i.insert_count,
                          i.delete_count,i.backout_count,i.purge_count,i.expunge_count FROM perf_table_item i
                          JOIN event e ON e.seq=i.event_seq WHERE e.file_hash=$h ORDER BY i.event_seq,i.ord;";
        c.Parameters.AddWithValue("$h", fileHash);
        using var r = c.ExecuteReader();
        while (r.Read())
            (map.TryGetValue(r.GetInt64(0), out var l) ? l : map[r.GetInt64(0)] = new())
                .Add(new PerformanceTableItem
                {
                    TableName = r.GetString(1), NaturalCount = r.GetInt32(2), IndexCount = r.GetInt32(3),
                    UpdateCount = r.GetInt32(4), InsertCount = r.GetInt32(5), DeleteCount = r.GetInt32(6),
                    BackoutCount = r.GetInt32(7), PurgeCount = r.GetInt32(8), ExpungeCount = r.GetInt32(9)
                });
        return map;
    }

    private List<SqlParameters>? LoadParametersFor(long seq)
    {
        List<SqlParameters>? list = null;
        using var c = _connection.CreateCommand();
        c.CommandText = "SELECT name,dtype,value FROM sql_parameter WHERE event_seq=$s ORDER BY ord;";
        c.Parameters.AddWithValue("$s", seq);
        using var r = c.ExecuteReader();
        while (r.Read()) (list ??= new()).Add(new SqlParameters { Name = r.GetString(0), Dtype = r.GetString(1), Value = r.GetString(2) });
        return list;
    }

    private List<ErrorLines>? LoadErrorLinesFor(long seq)
    {
        List<ErrorLines>? list = null;
        using var c = _connection.CreateCommand();
        c.CommandText = "SELECT code,message FROM error_line WHERE event_seq=$s ORDER BY ord;";
        c.Parameters.AddWithValue("$s", seq);
        using var r = c.ExecuteReader();
        while (r.Read()) (list ??= new()).Add(new ErrorLines { ErrorCode = r.GetInt32(0), Message = r.GetString(1) });
        return list;
    }

    private List<PerformanceTableItem>? LoadPerfItemsFor(long seq)
    {
        List<PerformanceTableItem>? list = null;
        using var c = _connection.CreateCommand();
        c.CommandText = @"SELECT table_name,natural_count,index_count,update_count,insert_count,delete_count,
                          backout_count,purge_count,expunge_count FROM perf_table_item WHERE event_seq=$s ORDER BY ord;";
        c.Parameters.AddWithValue("$s", seq);
        using var r = c.ExecuteReader();
        while (r.Read()) (list ??= new()).Add(new PerformanceTableItem
        {
            TableName = r.GetString(0), NaturalCount = r.GetInt32(1), IndexCount = r.GetInt32(2), UpdateCount = r.GetInt32(3),
            InsertCount = r.GetInt32(4), DeleteCount = r.GetInt32(5), BackoutCount = r.GetInt32(6), PurgeCount = r.GetInt32(7), ExpungeCount = r.GetInt32(8)
        });
        return list;
    }

    // ---------------------------------------------------------------- infra

    private static string Sha(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
    }
}
