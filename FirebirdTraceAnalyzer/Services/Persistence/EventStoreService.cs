using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Models.Storage;
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
    private const int SchemaVersion = 2;

    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public EventStoreService(string dbPath) : this(dbPath, writable: true)
    {
    }

    /// <param name="writable">
    /// true — обычное открытие (WAL, схема создаётся при отсутствии). false — только чтение
    /// (для импорта чужого файла): не мутируем источник (ни pragma, ни создание схемы).
    /// </param>
    private EventStoreService(string dbPath, bool writable)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));

        if (writable)
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();

            Exec("PRAGMA journal_mode=WAL;");
            Exec("PRAGMA synchronous=NORMAL;"); // безопасно при WAL, заметно ускоряет пакетную запись
            Exec("PRAGMA foreign_keys=ON;");
            EnsureSchema();
        }
        else
        {
            // Открываем источник только на чтение: файл пользователя не трогаем.
            _connection = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
            _connection.Open();
        }
    }

    // ---------------------------------------------------------------- schema

    private void EnsureSchema()
    {
        // Пользовательскую БД не мигрируем: при несовпадении версии пересоздаём (стор — восстановимый
        // кэш/архив распарсенного). Дёшево по сопровождению, потеря данных допустима по договорённости.
        var version = ReadUserVersion();
        if (version != 0 && version != SchemaVersion)
        {
            Logger.Info("EventStore schema v{Old} != v{New} — сброс хранилища", version, SchemaVersion);
            Exec(@"DROP TABLE IF EXISTS perf_table_item; DROP TABLE IF EXISTS error_line;
                   DROP TABLE IF EXISTS sql_parameter; DROP TABLE IF EXISTS event;
                   DROP TABLE IF EXISTS attachment; DROP TABLE IF EXISTS sql_text;
                   DROP TABLE IF EXISTS files;");
        }

        Exec(@"
CREATE TABLE IF NOT EXISTS files (
    id INTEGER PRIMARY KEY AUTOINCREMENT, hash TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL, path TEXT NOT NULL, size INTEGER NOT NULL,
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
    file_id INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
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

CREATE INDEX IF NOT EXISTS ix_event_file ON event(file_id);
CREATE INDEX IF NOT EXISTS ix_event_ts ON event(ts);
CREATE INDEX IF NOT EXISTS ix_param_event ON sql_parameter(event_seq);
CREATE INDEX IF NOT EXISTS ix_errline_event ON error_line(event_seq);
CREATE INDEX IF NOT EXISTS ix_perfitem_event ON perf_table_item(event_seq);");
        // ix_event_type убран: запросы стора не фильтруют по типу (фильтрация типов — в памяти UI),
        // индекс на 5.7M строк только раздувал БД.

        Exec($"PRAGMA user_version={SchemaVersion};");
    }

    private int ReadUserVersion()
    {
        using var c = _connection.CreateCommand();
        c.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(c.ExecuteScalar() ?? 0);
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

        // Родительскую строку файла вставляем ДО событий (на её id ссылается event.file_id по FK).
        // event_count проставим фактическим после цикла. RETURNING id — целочисленный ключ файла.
        long fileId;
        using (var ins = _connection.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = @"INSERT INTO files(hash,name,path,size,start_ts,end_ts,event_count,imported_ts)
                                VALUES($h,$n,$p,$s,$st,$et,0,$i) RETURNING id;";
            ins.Parameters.AddWithValue("$h", file.FileHash);
            ins.Parameters.AddWithValue("$n", file.FileName);
            ins.Parameters.AddWithValue("$p", file.FilePath);
            ins.Parameters.AddWithValue("$s", file.FileSize);
            ins.Parameters.AddWithValue("$st", file.StartTrace.Ticks);
            ins.Parameters.AddWithValue("$et", file.EndTrace.Ticks);
            ins.Parameters.AddWithValue("$i", DateTime.UtcNow.Ticks);
            fileId = (long)ins.ExecuteScalar()!;
        }

        long count = 0;
        using (var writer = new BatchWriter(_connection, tx, fileId))
        {
            foreach (var ev in events)
            {
                writer.Write(ev);
                count++;
            }
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

    /// <summary>
    /// Пакетный писатель на переиспользуемых prepared-командах (создаются один раз на файл).
    /// Устраняет аллокацию SqliteCommand/параметров на каждое событие/строку — главный перф-выигрыш.
    /// Дедуп-кэши ключуются строкой (дешёвый hash); SHA-256 считается только для новых уникальных
    /// значений (для UNIQUE-колонки и кросс-файлового дедупа).
    /// </summary>
    private sealed class BatchWriter : IDisposable
    {
        private readonly long _fileId;
        private readonly SqliteCommand _ev, _sqlIns, _sqlSel, _attIns, _attSel, _param, _err, _perfItem;
        private readonly Dictionary<string, SqliteParameter> _evp = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _sqlCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _attCache = new(StringComparer.Ordinal);

        public BatchWriter(SqliteConnection c, SqliteTransaction tx, long fileId)
        {
            _fileId = fileId;

            _ev = Cmd(c, tx, @"
INSERT INTO event(file_id,ts,trace_id,hex_trace_id,event_type,attachment_ref,session_id,sql_ref,statement_id,
    txn_present,txn_id,txn_isolation,txn_consistency,txn_lock,txn_access,restart_count,procedure_name,
    trigger_name,trigger_table,trigger_timing,trigger_event,component,
    perf_present,perf_execute_ms,perf_fetch,perf_read,perf_write,perf_mark,perf_table_state)
VALUES($file,$ts,$tid,$hex,$type,$att,$sess,$sql,$stmt,
    $txp,$txid,$txiso,$txcons,$txlock,$txacc,$rc,$proc,
    $trn,$trt,$trtm,$tre,$comp,
    $pp,$pe,$pf,$pr,$pw,$pm,$pts)
RETURNING seq;");
            foreach (var n in new[]
                     {
                         "$file", "$ts", "$tid", "$hex", "$type", "$att", "$sess", "$sql", "$stmt", "$txp", "$txid",
                         "$txiso", "$txcons", "$txlock", "$txacc", "$rc", "$proc", "$trn", "$trt", "$trtm", "$tre",
                         "$comp", "$pp", "$pe", "$pf", "$pr", "$pw", "$pm", "$pts"
                     })
            {
                var p = _ev.CreateParameter();
                p.ParameterName = n;
                _ev.Parameters.Add(p);
                _evp[n] = p;
            }
            _ev.Prepare();

            _sqlIns = Cmd(c, tx, "INSERT OR IGNORE INTO sql_text(sha,text) VALUES($sha,$t);", "$sha", "$t");
            _sqlSel = Cmd(c, tx, "SELECT id FROM sql_text WHERE sha=$sha;", "$sha");
            _attIns = Cmd(c, tx, @"INSERT OR IGNORE INTO attachment(sha,att_id,db_path,user,role,charset,protocol,address,port,process_path,process_id)
                                   VALUES($sha,$ai,$db,$u,$r,$c,$pr,$ad,$po,$pp,$pi);",
                "$sha", "$ai", "$db", "$u", "$r", "$c", "$pr", "$ad", "$po", "$pp", "$pi");
            _attSel = Cmd(c, tx, "SELECT id FROM attachment WHERE sha=$sha;", "$sha");
            _param = Cmd(c, tx, "INSERT INTO sql_parameter(event_seq,ord,name,dtype,value) VALUES($s,$o,$n,$d,$v);",
                "$s", "$o", "$n", "$d", "$v");
            _err = Cmd(c, tx, "INSERT INTO error_line(event_seq,ord,code,message) VALUES($s,$o,$c,$m);",
                "$s", "$o", "$c", "$m");
            _perfItem = Cmd(c, tx, @"INSERT INTO perf_table_item(event_seq,ord,table_name,natural_count,index_count,
                update_count,insert_count,delete_count,backout_count,purge_count,expunge_count)
                VALUES($s,$o,$tn,$na,$ix,$up,$in,$de,$ba,$pu,$ex);",
                "$s", "$o", "$tn", "$na", "$ix", "$up", "$in", "$de", "$ba", "$pu", "$ex");
        }

        public void Write(EventBase ev)
        {
            void S(string n, object? v) => _evp[n].Value = v ?? DBNull.Value;

            S("$file", _fileId);
            S("$ts", ev.Timestamp.Ticks);
            S("$tid", ev.TraceId);
            S("$hex", ev.HexTraceId);
            S("$type", (int)ev.EventType);

            long? attRef = null; int? sessionId = null; long? sqlRef = null; long? statementId = null;
            int? txnPresent = null; long? txnId = null;
            string? txIso = null, txCons = null, txLock = null, txAcc = null;
            int? restart = null; string? procName = null;
            string? trName = null, trTable = null, trTiming = null, trEvent = null, component = null;
            int? perfPresent = null; int? pe = null, pf = null, pr = null, pw = null, pm = null;
            var perfTableState = 0;

            switch (ev)
            {
                case TraceInitEvent e: sessionId = e.Session.SessionId; break;
                case TraceFinishEvent e: sessionId = e.Session.SessionId; break;
                case AttachDatabaseEvent e: attRef = InternAttachment(e.Attachment); break;
                case DetachDatabaseEvent e: attRef = InternAttachment(e.Attachment); break;

                case StatementEventBase e:
                    attRef = InternAttachment(e.Attachment);
                    sqlRef = InternSql(e.Sql);
                    statementId = e.StatementId;
                    (txnPresent, txnId, txIso, txCons, txLock, txAcc) = Txn(e.Transaction);
                    if (e is StatementRestartEvent r) restart = r.RestartCount;
                    (perfPresent, pe, pf, pr, pw, pm, perfTableState) = Perf(e);
                    break;

                case ProcedureEventBase e:
                    attRef = InternAttachment(e.Attachment);
                    procName = e.ProcedureName;
                    (txnPresent, txnId, txIso, txCons, txLock, txAcc) = Txn(e.Transaction);
                    (perfPresent, pe, pf, pr, pw, pm, perfTableState) = Perf(e);
                    break;

                case TriggerEventBase e:
                    attRef = InternAttachment(e.Attachment);
                    trName = e.TriggerName; trTable = e.Table; trTiming = e.Timing; trEvent = e.Event;
                    (txnPresent, txnId, txIso, txCons, txLock, txAcc) = Txn(e.Transaction);
                    (perfPresent, pe, pf, pr, pw, pm, perfTableState) = Perf(e);
                    break;

                case ErrorEvent e:
                    attRef = InternAttachment(e.Attachment);
                    component = e.Component;
                    break;

                default:
                    throw new NotSupportedException($"Event type {ev.GetType().Name} is not supported by the store.");
            }

            S("$att", attRef); S("$sess", sessionId); S("$sql", sqlRef); S("$stmt", statementId);
            S("$txp", txnPresent); S("$txid", txnId); S("$txiso", txIso); S("$txcons", txCons);
            S("$txlock", txLock); S("$txacc", txAcc); S("$rc", restart); S("$proc", procName);
            S("$trn", trName); S("$trt", trTable); S("$trtm", trTiming); S("$tre", trEvent); S("$comp", component);
            S("$pp", perfPresent); S("$pe", pe); S("$pf", pf); S("$pr", pr); S("$pw", pw); S("$pm", pm);
            S("$pts", perfTableState);

            var seq = (long)_ev.ExecuteScalar()!;
            WriteChildren(seq, ev);
        }

        private void WriteChildren(long seq, EventBase ev)
        {
            switch (ev)
            {
                case StatementEventBase e: WriteParameters(seq, e.Parameters); WritePerfTable(seq, ev); break;
                case ProcedureEventBase e: WriteParameters(seq, e.Parameters); WritePerfTable(seq, ev); break;
                case TriggerEventBase: WritePerfTable(seq, ev); break;
                case ErrorEvent e: WriteErrorLines(seq, e.Errors); break;
            }
        }

        private void WriteParameters(long seq, IReadOnlyList<SqlParameters> parameters)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i];
                _param.Parameters["$s"].Value = seq;
                _param.Parameters["$o"].Value = i;
                _param.Parameters["$n"].Value = p.Name;
                _param.Parameters["$d"].Value = p.Dtype;
                _param.Parameters["$v"].Value = p.Value;
                _param.ExecuteNonQuery();
            }
        }

        private void WriteErrorLines(long seq, IReadOnlyList<ErrorLines> errors)
        {
            for (var i = 0; i < errors.Count; i++)
            {
                var e = errors[i];
                _err.Parameters["$s"].Value = seq;
                _err.Parameters["$o"].Value = i;
                _err.Parameters["$c"].Value = e.ErrorCode;
                _err.Parameters["$m"].Value = e.Message;
                _err.ExecuteNonQuery();
            }
        }

        private void WritePerfTable(long seq, EventBase ev)
        {
            var items = GetPerfTable(ev)?.Items;
            if (items is null) return;
            for (var i = 0; i < items.Count; i++)
            {
                var it = items[i];
                _perfItem.Parameters["$s"].Value = seq;
                _perfItem.Parameters["$o"].Value = i;
                _perfItem.Parameters["$tn"].Value = it.TableName;
                _perfItem.Parameters["$na"].Value = it.NaturalCount;
                _perfItem.Parameters["$ix"].Value = it.IndexCount;
                _perfItem.Parameters["$up"].Value = it.UpdateCount;
                _perfItem.Parameters["$in"].Value = it.InsertCount;
                _perfItem.Parameters["$de"].Value = it.DeleteCount;
                _perfItem.Parameters["$ba"].Value = it.BackoutCount;
                _perfItem.Parameters["$pu"].Value = it.PurgeCount;
                _perfItem.Parameters["$ex"].Value = it.ExpungeCount;
                _perfItem.ExecuteNonQuery();
            }
        }

        private long InternSql(string sql)
        {
            if (_sqlCache.TryGetValue(sql, out var cached)) return cached;

            var sha = Sha(sql);
            _sqlIns.Parameters["$sha"].Value = sha;
            _sqlIns.Parameters["$t"].Value = sql;
            _sqlIns.ExecuteNonQuery();
            _sqlSel.Parameters["$sha"].Value = sha;
            var id = (long)_sqlSel.ExecuteScalar()!;
            _sqlCache[sql] = id;
            return id;
        }

        private long InternAttachment(AttachmentInfo a)
        {
            var key = $"{a.AttachmentId} {a.DatabasePath} {a.User} {a.Role} {a.Charset} {a.Protocol} {a.Address} {a.Port} {a.ProcessPath} {a.ProcessId}";
            if (_attCache.TryGetValue(key, out var cached)) return cached;

            var sha = Sha(key);
            _attIns.Parameters["$sha"].Value = sha;
            _attIns.Parameters["$ai"].Value = a.AttachmentId;
            _attIns.Parameters["$db"].Value = a.DatabasePath;
            _attIns.Parameters["$u"].Value = a.User;
            _attIns.Parameters["$r"].Value = a.Role;
            _attIns.Parameters["$c"].Value = a.Charset;
            _attIns.Parameters["$pr"].Value = a.Protocol;
            _attIns.Parameters["$ad"].Value = a.Address;
            _attIns.Parameters["$po"].Value = a.Port;
            _attIns.Parameters["$pp"].Value = (object?)a.ProcessPath ?? DBNull.Value;
            _attIns.Parameters["$pi"].Value = (object?)a.ProcessId ?? DBNull.Value;
            _attIns.ExecuteNonQuery();
            _attSel.Parameters["$sha"].Value = sha;
            var id = (long)_attSel.ExecuteScalar()!;
            _attCache[key] = id;
            return id;
        }

        private static SqliteCommand Cmd(SqliteConnection c, SqliteTransaction tx, string sql, params string[] names)
        {
            var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            foreach (var n in names)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = n;
                cmd.Parameters.Add(p);
            }
            if (names.Length > 0) cmd.Prepare();
            return cmd;
        }

        public void Dispose()
        {
            _ev.Dispose(); _sqlIns.Dispose(); _sqlSel.Dispose(); _attIns.Dispose(); _attSel.Dispose();
            _param.Dispose(); _err.Dispose(); _perfItem.Dispose();
        }
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

    // ---------------------------------------------------------------- transfer

    public int ImportFrom(string sourceDbPath)
    {
        if (string.IsNullOrWhiteSpace(sourceDbPath))
            throw new ArgumentException("Source path is empty.", nameof(sourceDbPath));
        if (!File.Exists(sourceDbPath))
            throw new FileNotFoundException("Source store not found.", sourceDbPath);

        // Источник открываем только на чтение — файл пользователя не мутируем.
        using var source = new EventStoreService(sourceDbPath, writable: false);

        var imported = 0;
        foreach (var file in source.ListFiles())
        {
            // Уже есть файл с таким хэшем — пропускаем (контент идентичен, дублировать незачем).
            if (ContainsFile(file.FileHash))
                continue;

            // ReadFile→WriteFile: дедуп SQL/подключений происходит на уровне БД (UNIQUE(sha) + INSERT OR IGNORE),
            // поэтому переносимые словари сливаются с существующими без дублей.
            WriteFile(file, source.ReadFile(file.FileHash));
            imported++;
        }

        Logger.Info("EventStore: imported {Count} file(s) from {Path}", imported, sourceDbPath);
        return imported;
    }

    public void ExportTo(string targetDbPath, IEnumerable<TraceFileInfoModel> files)
    {
        if (string.IsNullOrWhiteSpace(targetDbPath))
            throw new ArgumentException("Target path is empty.", nameof(targetDbPath));

        // Чистый экспорт: если файл существует — удаляем его (и WAL-спутники), чтобы получить
        // самодостаточную БД ровно с выбранными файлами.
        DeleteDbFiles(targetDbPath);

        var exported = 0;
        using (var target = new EventStoreService(targetDbPath))
        {
            foreach (var file in files)
            {
                if (!ContainsFile(file.FileHash))
                    continue;

                target.WriteFile(file, ReadFile(file.FileHash));
                exported++;
            }
        }
        // Dispose закрывает соединение → WAL чекпойнтится в основной файл: экспорт — один .db.

        Logger.Info("EventStore: exported {Count} file(s) to {Path}", exported, targetDbPath);
    }

    private static void DeleteDbFiles(string dbPath)
    {
        foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
            if (File.Exists(path))
                File.Delete(path);
    }

    // ---------------------------------------------------------------- read

    public bool ContainsFile(string fileHash)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM files WHERE hash=$h LIMIT 1;";
        cmd.Parameters.AddWithValue("$h", fileHash);
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>Целочисленный id файла по его хэшу (null — файла нет).</summary>
    private long? FileId(string fileHash)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id FROM files WHERE hash=$h;";
        cmd.Parameters.AddWithValue("$h", fileHash);
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? null : Convert.ToInt64(v);
    }

    public IReadOnlyList<EventBase> ReadFile(string fileHash)
    {
        var result = new List<EventBase>();
        var attCache = new Dictionary<long, AttachmentInfo>();
        var sqlCache = new Dictionary<long, string>();

        var fileId = FileId(fileHash);
        if (fileId is null)
            return result;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM event WHERE file_id=$fid ORDER BY seq;";
        cmd.Parameters.AddWithValue("$fid", fileId.Value);
        using var reader = cmd.ExecuteReader();
        var rows = new List<Row>();
        while (reader.Read())
            rows.Add(ReadRow(reader));

        // Дочерние коллекции — пакетно (без N+1).
        var paramsBySeq = LoadParameters(fileId.Value);
        var errorsBySeq = LoadErrorLines(fileId.Value);
        var perfBySeq = LoadPerfItems(fileId.Value);

        foreach (var row in rows)
            result.Add(BuildEvent(row, attCache, sqlCache,
                paramsBySeq.GetValueOrDefault(row.Seq),
                errorsBySeq.GetValueOrDefault(row.Seq),
                perfBySeq.GetValueOrDefault(row.Seq)));

        return result;
    }

    public IEnumerable<EventBase> Query(DateTime? from = null, DateTime? to = null)
    {
        var attCache = new Dictionary<long, AttachmentInfo>();
        var sqlCache = new Dictionary<long, string>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM event WHERE ($f IS NULL OR ts>=$f) AND ($t IS NULL OR ts<=$t) ORDER BY ts, seq;";
        cmd.Parameters.AddWithValue("$f", (object?)from?.Ticks ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", (object?)to?.Ticks ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = ReadRow(reader);
            yield return BuildEvent(row, attCache, sqlCache,
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
        // Полная очистка возвращает место и пересобирает структуру/индексы. На пустой БД VACUUM дёшев.
        Exec("VACUUM;");
        Exec("PRAGMA wal_checkpoint(TRUNCATE);");
    }

    public void Compact()
    {
        // Осиротевшие дедуп-словари: строки, на которые не осталось ссылок из event. DeleteFile их
        // не трогает (дедуп между файлами), поэтому они копятся — чистим здесь, при обслуживании.
        Exec("DELETE FROM sql_text WHERE id NOT IN (SELECT sql_ref FROM event WHERE sql_ref IS NOT NULL);");
        Exec("DELETE FROM attachment WHERE id NOT IN (SELECT attachment_ref FROM event WHERE attachment_ref IS NOT NULL);");
        // Пересборка БД+индексов и возврат места на диск; затем усечение WAL-файла.
        Exec("VACUUM;");
        Exec("PRAGMA wal_checkpoint(TRUNCATE);");
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
        var events = Scalar("SELECT COALESCE(SUM(event_count),0) FROM files;");
        var sql = Scalar("SELECT COUNT(*) FROM sql_text;");
        var att = Scalar("SELECT COUNT(*) FROM attachment;");
        var raw = Scalar("SELECT COALESCE(SUM(size),0) FROM files;");

        DateTime? start = null, end = null;
        using (var c = _connection.CreateCommand())
        {
            c.CommandText = "SELECT MIN(start_ts), MAX(end_ts) FROM files;";
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

    public EventStoreSizeBreakdown GetSizeBreakdown()
    {
        long Scalar(string sql)
        {
            using var c = _connection.CreateCommand();
            c.CommandText = sql;
            var v = c.ExecuteScalar();
            return v is null or DBNull ? 0 : Convert.ToInt64(v);
        }

        long dbSize = 0;
        try { dbSize = new FileInfo(_dbPath).Length; } catch { /* файл ещё не сброшен — не критично */ }

        // Байты текста берём через LENGTH(CAST(x AS BLOB)) — это размер UTF-8, а не число символов.
        return new EventStoreSizeBreakdown
        {
            DbSizeBytes = dbSize,
            EventRows = Scalar("SELECT COUNT(*) FROM event;"),
            SqlTextRows = Scalar("SELECT COUNT(*) FROM sql_text;"),
            AttachmentRows = Scalar("SELECT COUNT(*) FROM attachment;"),
            ParameterRows = Scalar("SELECT COUNT(*) FROM sql_parameter;"),
            ErrorLineRows = Scalar("SELECT COUNT(*) FROM error_line;"),
            PerfItemRows = Scalar("SELECT COUNT(*) FROM perf_table_item;"),
            SqlTextBytes = Scalar("SELECT COALESCE(SUM(LENGTH(CAST(text AS BLOB))),0) FROM sql_text;"),
            ParameterBytes = Scalar("SELECT COALESCE(SUM(LENGTH(CAST(value AS BLOB))),0) FROM sql_parameter;"),
            ErrorMessageBytes = Scalar("SELECT COALESCE(SUM(LENGTH(CAST(message AS BLOB))),0) FROM error_line;")
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

    private EventBase BuildEvent(Row row, Dictionary<long, AttachmentInfo> attCache, Dictionary<long, string> sqlCache,
        List<SqlParameters>? parameters, List<ErrorLines>? errors, List<PerformanceTableItem>? perfItems)
    {
        var type = (EventType)row.Type;
        var ts = new DateTime(row.Ts);
        AttachmentInfo Att() => LoadAttachment(row.AttRef!.Value, attCache);
        string Sql() => LoadSql(row.SqlRef!.Value, sqlCache);
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
            EventType.ExecuteStatementStart => new StatementStartEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn(), StatementId = row.StatementId, Sql = Sql(), Parameters = prm },
            EventType.ExecuteStatementRestart => new StatementRestartEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn(), StatementId = row.StatementId, Sql = Sql(), Parameters = prm, RestartCount = row.Restart },
            EventType.ExecuteStatementFinish => new StatementFinishEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn(), StatementId = row.StatementId, Sql = Sql(), Parameters = prm, Performance = Perf(), PerformanceTable = PerfTable() },
            EventType.FailedExecuteStatementFinish => new FailedStatementFinishEvent { Timestamp = ts, TraceId = row.TraceId, HexTraceId = row.Hex, EventType = type, Attachment = Att(), Transaction = Txn(), StatementId = row.StatementId, Sql = Sql(), Parameters = prm, Performance = Perf(), PerformanceTable = PerfTable() },
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

    private string LoadSql(long id, Dictionary<long, string> cache)
    {
        // SQL сильно дедуплицирован (один и тот же текст на тысячи событий), поэтому мемоизируем по id:
        // без кэша это был бы запрос-на-событие (N+1) — главная стоимость чтения файла.
        if (cache.TryGetValue(id, out var cached)) return cached;

        using var c = _connection.CreateCommand();
        c.CommandText = "SELECT text FROM sql_text WHERE id=$id;";
        c.Parameters.AddWithValue("$id", id);
        var text = (string)c.ExecuteScalar()!;
        cache[id] = text;
        return text;
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

    private Dictionary<long, List<SqlParameters>> LoadParameters(long fileId)
    {
        var map = new Dictionary<long, List<SqlParameters>>();
        using var c = _connection.CreateCommand();
        c.CommandText = @"SELECT p.event_seq,p.name,p.dtype,p.value FROM sql_parameter p
                          JOIN event e ON e.seq=p.event_seq WHERE e.file_id=$fid ORDER BY p.event_seq,p.ord;";
        c.Parameters.AddWithValue("$fid", fileId);
        using var r = c.ExecuteReader();
        while (r.Read())
            (map.TryGetValue(r.GetInt64(0), out var l) ? l : map[r.GetInt64(0)] = new())
                .Add(new SqlParameters { Name = r.GetString(1), Dtype = r.GetString(2), Value = r.GetString(3) });
        return map;
    }

    private Dictionary<long, List<ErrorLines>> LoadErrorLines(long fileId)
    {
        var map = new Dictionary<long, List<ErrorLines>>();
        using var c = _connection.CreateCommand();
        c.CommandText = @"SELECT x.event_seq,x.code,x.message FROM error_line x
                          JOIN event e ON e.seq=x.event_seq WHERE e.file_id=$fid ORDER BY x.event_seq,x.ord;";
        c.Parameters.AddWithValue("$fid", fileId);
        using var r = c.ExecuteReader();
        while (r.Read())
            (map.TryGetValue(r.GetInt64(0), out var l) ? l : map[r.GetInt64(0)] = new())
                .Add(new ErrorLines { ErrorCode = r.GetInt32(1), Message = r.GetString(2) });
        return map;
    }

    private Dictionary<long, List<PerformanceTableItem>> LoadPerfItems(long fileId)
    {
        var map = new Dictionary<long, List<PerformanceTableItem>>();
        using var c = _connection.CreateCommand();
        c.CommandText = @"SELECT i.event_seq,i.table_name,i.natural_count,i.index_count,i.update_count,i.insert_count,
                          i.delete_count,i.backout_count,i.purge_count,i.expunge_count FROM perf_table_item i
                          JOIN event e ON e.seq=i.event_seq WHERE e.file_id=$fid ORDER BY i.event_seq,i.ord;";
        c.Parameters.AddWithValue("$fid", fileId);
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

    // ---------------------------------------------------------------- ad-hoc query

    public StorageQueryResult ExecuteQuery(string sql, int maxRows, CancellationToken cancellationToken = default)
    {
        ValidateReadOnly(sql);

        var sw = Stopwatch.StartNew();

        // Физический запрет записи на время запроса (соединение общее, но диспетчер сериализует).
        Exec("PRAGMA query_only=ON;");
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;
            using var reader = cmd.ExecuteReader();

            var columns = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                columns[i] = reader.GetName(i);

            var rows = new List<object?[]>();
            var truncated = false;

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (rows.Count >= maxRows)
                {
                    truncated = true;
                    break;
                }

                var row = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[i] = value is DBNull ? null : value;
                }
                rows.Add(row);
            }

            sw.Stop();
            return new StorageQueryResult(columns, rows, truncated, sw.ElapsedMilliseconds);
        }
        finally
        {
            // Возвращаем режим записи — дальше стор используется для парсинга/мутаций.
            Exec("PRAGMA query_only=OFF;");
        }
    }

    /// <summary>Пропускает только одиночный читающий запрос (SELECT/WITH). Реальный барьер —
    /// PRAGMA query_only; здесь даём понятную ошибку до выполнения.</summary>
    private static void ValidateReadOnly(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("Пустой запрос.");

        var trimmed = sql.Trim().TrimEnd(';').TrimStart();

        // Запрещаем цепочку операторов (';' внутри после срезания единственного хвостового).
        if (trimmed.Contains(';'))
            throw new InvalidOperationException("Разрешён только один SELECT-запрос (без ';').");

        var head = trimmed.TrimStart('(').TrimStart();
        if (!head.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && !head.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Разрешены только запросы SELECT / WITH (только чтение).");
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
    }
}
