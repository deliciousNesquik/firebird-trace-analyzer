using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services.Persistence;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Гарантия обратимости хранилища (Этап 1): записал события → прочитал → они идентичны исходным.
/// Покрывает все 16 типов событий и краевые случаи (нет транзакции, пустая/null таблица perf,
/// отсутствующие поля подключения), а также дедуп/статистику/список/удаление/очистку.
/// </summary>
public sealed class EventStoreRoundTripTests
{
    // ---- сравнение через канонический дамп по рефлексии (модели без value-equality) ----

    private static string Dump(object? o)
    {
        var sb = new StringBuilder();
        Write(sb, o);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, object? o)
    {
        switch (o)
        {
            case null: sb.Append('∅'); return;
            case string s: sb.Append('"').Append(s).Append('"'); return;
            case DateTime dt: sb.Append("DT:").Append(dt.Ticks); return;
            case bool or int or long or short or byte or double or float or decimal or Enum:
                sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture)); return;
            case IEnumerable en:
                sb.Append('[');
                var first = true;
                foreach (var item in en) { if (!first) sb.Append(';'); first = false; Write(sb, item); }
                sb.Append(']'); return;
            default:
                sb.Append(o.GetType().Name).Append('{');
                var props = o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0)
                    .OrderBy(p => p.Name, StringComparer.Ordinal);
                var f = true;
                foreach (var p in props)
                {
                    if (!f) sb.Append(','); f = false;
                    sb.Append(p.Name).Append('=');
                    Write(sb, p.GetValue(o));
                }
                sb.Append('}'); return;
        }
    }

    // ---- фабрики образцов ----

    private static AttachmentInfo AttFull() => new()
    {
        AttachmentId = 12345, DatabasePath = "/db/main.fdb", User = "SYSDBA", Role = "RDB$ADMIN",
        Charset = "UTF8", Protocol = "TCPv4", Address = "10.0.1.20/54321", Port = 3050,
        ProcessPath = "/usr/bin/app", ProcessId = 999
    };

    private static AttachmentInfo AttMinimal() => new()
    {
        AttachmentId = 7, DatabasePath = "/db/x.fdb", User = "U", Role = "NONE",
        Charset = "WIN1251", Protocol = "<internal>", Address = "<internal>", Port = 0,
        ProcessPath = null, ProcessId = null
    };

    private static TransactionInfo Txn() => new()
    {
        TransactionId = 5567, IsolationLevel = "READ_COMMITTED", ConsistencyMode = "REC_VERSION",
        LockMode = "NOWAIT", AccessMode = "READ_WRITE"
    };

    private static IReadOnlyList<SqlParameters> Params() => new List<SqlParameters>
    {
        new() { Name = "param0", Dtype = "bigint", Value = "42" },
        new() { Name = "param1", Dtype = "varchar", Value = "Привет, мир" },
        new() { Name = "param2", Dtype = "blob", Value = "NULL" },
    };

    private static PerformanceInfo Perf() => new()
    { ExecuteMs = 1180, FetchCount = 842, ReadCount = 17, WriteCount = 3, MarkCount = 1 };

    private static PerformanceTable PerfTableWithItems() => new()
    {
        Items = new List<PerformanceTableItem>
        {
            new() { TableName = "ORDERS", NaturalCount = 1, IndexCount = 2, UpdateCount = 3, InsertCount = 4,
                    DeleteCount = 5, BackoutCount = 6, PurgeCount = 7, ExpungeCount = 8 }
        }
    };

    private const string Sql1 = "SELECT c.id, c.name\nFROM CLIENTS c\nWHERE c.active = 1";
    private const string Sql2 = "UPDATE ORDERS SET status = 'DONE' WHERE id = ?";

    private static List<EventBase> SampleEvents() =>
    [
        new TraceInitEvent { Timestamp = T(0), TraceId = 1, HexTraceId = "0x01", EventType = EventType.TraceInit, Session = new TraceSessionInfo { SessionId = 100 } },
        new TraceFinishEvent { Timestamp = T(1), TraceId = 1, HexTraceId = "0x01", EventType = EventType.TraceFinish, Session = new TraceSessionInfo { SessionId = 100 } },
        new AttachDatabaseEvent { Timestamp = T(2), TraceId = 2, HexTraceId = "0x02", EventType = EventType.AttachDatabase, Attachment = AttFull() },
        new DetachDatabaseEvent { Timestamp = T(3), TraceId = 2, HexTraceId = "0x02", EventType = EventType.DetachDatabase, Attachment = AttMinimal() },
        new StatementStartEvent { Timestamp = T(4), TraceId = 3, HexTraceId = "0x03", EventType = EventType.ExecuteStatementStart, Attachment = AttFull(), Transaction = Txn(), StatementId = 55, Sql = Sql1, Parameters = Params() },
        new StatementStartEvent { Timestamp = T(5), TraceId = 3, HexTraceId = "0x03", EventType = EventType.ExecuteStatementStart, Attachment = AttFull(), Transaction = null, StatementId = null, Sql = Sql1, Parameters = new List<SqlParameters>() },
        new StatementRestartEvent { Timestamp = T(6), TraceId = 3, HexTraceId = "0x03", EventType = EventType.ExecuteStatementRestart, Attachment = AttFull(), Transaction = Txn(), StatementId = 55, Sql = Sql1, Parameters = Params(), RestartCount = 2 },
        new StatementFinishEvent { Timestamp = T(7), TraceId = 3, HexTraceId = "0x03", EventType = EventType.ExecuteStatementFinish, Attachment = AttFull(), Transaction = Txn(), StatementId = 55, Sql = Sql2, Parameters = Params(), Performance = Perf(), PerformanceTable = PerfTableWithItems() },
        new FailedStatementFinishEvent { Timestamp = T(8), TraceId = 3, HexTraceId = "0x03", EventType = EventType.FailedExecuteStatementFinish, Attachment = AttFull(), Transaction = Txn(), StatementId = 55, Sql = Sql2, Parameters = Params(), Performance = Perf(), PerformanceTable = null },
        new ProcedureStartEvent { Timestamp = T(9), TraceId = 4, HexTraceId = "0x04", EventType = EventType.ExecuteProcedureStart, Attachment = AttFull(), Transaction = Txn(), ProcedureName = "SP_RECALC", Parameters = Params() },
        new ProcedureFinishEvent { Timestamp = T(10), TraceId = 4, HexTraceId = "0x04", EventType = EventType.ExecuteProcedureFinish, Attachment = AttFull(), Transaction = Txn(), ProcedureName = "SP_RECALC", Parameters = Params(), Performance = Perf(), PerformanceTable = new PerformanceTable { Items = null } },
        new FailedProcedureFinishEvent { Timestamp = T(11), TraceId = 4, HexTraceId = "0x04", EventType = EventType.FailedExecuteProcedureFinish, Attachment = AttFull(), Transaction = Txn(), ProcedureName = "SP_RECALC", Parameters = new List<SqlParameters>(), Performance = Perf(), PerformanceTable = PerfTableWithItems() },
        new TriggerStartEvent { Timestamp = T(12), TraceId = 5, HexTraceId = "0x05", EventType = EventType.ExecuteTriggerStart, Attachment = AttFull(), Transaction = Txn(), TriggerName = "TR_A", Table = "ORDERS", Timing = "BEFORE", Event = "INSERT" },
        new TriggerFinishEvent { Timestamp = T(13), TraceId = 5, HexTraceId = "0x05", EventType = EventType.ExecuteTriggerFinish, Attachment = AttFull(), Transaction = Txn(), TriggerName = "TR_B", Table = null, Timing = null, Event = "ON CONNECT", Performance = Perf(), PerformanceTable = PerfTableWithItems() },
        new FailedTriggerFinishEvent { Timestamp = T(14), TraceId = 5, HexTraceId = "0x05", EventType = EventType.FailedExecuteTriggerFinish, Attachment = AttFull(), Transaction = Txn(), TriggerName = "TR_C", Table = "T", Timing = "AFTER", Event = "UPDATE", Performance = Perf(), PerformanceTable = null },
        new ErrorEvent { Timestamp = T(15), TraceId = 6, HexTraceId = "0x06", EventType = EventType.Error, Attachment = AttFull(), Component = "JStatement::execute", Errors = new List<ErrorLines> { new() { ErrorCode = 335544345, Message = "deadlock" }, new() { ErrorCode = 335544336, Message = "update conflicts" } } },
    ];

    private static DateTime T(int i) => new DateTime(2026, 7, 7, 11, 0, 0, DateTimeKind.Utc).AddMilliseconds(i * 137 + 0.4);

    private static TraceFileInfoModel File(string hash, string name, long size = 1000) =>
        new(name, "/logs/" + name, size, T(0), T(15), 16, hash);

    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), $"eventstore-test-{Guid.NewGuid():N}.db");

    // ---- сами тесты ----

    [Fact]
    public void WriteThenRead_RoundTripsEveryEventTypeLosslessly()
    {
        var db = TempDb();
        var original = SampleEvents();
        try
        {
            using (var store = new EventStoreService(db))
                store.WriteFile(File("HASH_A", "a.log"), original);

            using var store2 = new EventStoreService(db);
            var read = store2.ReadFile("HASH_A");

            Assert.Equal(original.Count, read.Count);
            for (var i = 0; i < original.Count; i++)
                Assert.Equal(Dump(original[i]), Dump(read[i]));
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public void SameSqlAcrossFiles_IsDeduplicated()
    {
        var db = TempDb();
        try
        {
            using var store = new EventStoreService(db);
            // Оба файла содержат один и тот же Sql1 → в sql_text должна быть 1 строка на этот текст.
            store.WriteFile(File("H1", "1.log"), SampleEvents());
            store.WriteFile(File("H2", "2.log"), SampleEvents());

            var stats = store.GetStatistics();
            Assert.Equal(2, stats.FileCount);
            Assert.Equal(32, stats.EventCount);
            // В образце ровно 2 уникальных SQL (Sql1, Sql2) на оба файла благодаря дедупу.
            Assert.Equal(2, stats.UniqueSqlCount);
            // Уникальных подключений — 2 (AttFull, AttMinimal), общих на оба файла.
            Assert.Equal(2, stats.UniqueAttachmentCount);
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public void Rewrite_SameHash_ReplacesEvents()
    {
        var db = TempDb();
        try
        {
            using var store = new EventStoreService(db);
            store.WriteFile(File("H", "f.log"), SampleEvents());
            store.WriteFile(File("H", "f.log"), SampleEvents().Take(3).ToList());
            Assert.Equal(3, store.ReadFile("H").Count);
            Assert.Equal(1, store.GetStatistics().FileCount);
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public void ListDeleteClear_Work()
    {
        var db = TempDb();
        try
        {
            using var store = new EventStoreService(db);
            store.WriteFile(File("H1", "1.log"), SampleEvents());
            store.WriteFile(File("H2", "2.log"), SampleEvents());

            var files = store.ListFiles();
            Assert.Equal(2, files.Count);
            Assert.Contains(files, f => f.FileHash == "H1");

            store.DeleteFile("H1");
            Assert.False(store.ContainsFile("H1"));
            Assert.True(store.ContainsFile("H2"));
            Assert.Empty(store.ReadFile("H1"));

            store.Clear();
            Assert.Equal(0, store.GetStatistics().FileCount);
            Assert.Equal(0, store.GetStatistics().EventCount);
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public void Query_ByTimeRange_ReturnsSlice()
    {
        var db = TempDb();
        try
        {
            using var store = new EventStoreService(db);
            store.WriteFile(File("H", "f.log"), SampleEvents());

            var all = store.Query().ToList();
            Assert.Equal(16, all.Count);

            var slice = store.Query(from: T(4), to: T(7)).ToList();
            Assert.Equal(4, slice.Count); // события с индексами 4..7
            Assert.All(slice, e => Assert.InRange(e.Timestamp, T(4), T(7)));
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public void ExportThenImport_RoundTripsFilesAndDedups()
    {
        var srcDb = TempDb();
        var exportDb = TempDb();
        var destDb = TempDb();
        try
        {
            // Источник: два файла с общими SQL/подключениями (дедуп внутри источника).
            using (var src = new EventStoreService(srcDb))
            {
                src.WriteFile(File("H1", "1.log"), SampleEvents());
                src.WriteFile(File("H2", "2.log"), SampleEvents());
                src.ExportTo(exportDb, src.ListFiles());
            }

            // Приёмник: пустой, импортируем из экспортированного файла.
            using var dest = new EventStoreService(destDb);
            var imported = dest.ImportFrom(exportDb);

            Assert.Equal(2, imported);
            var stats = dest.GetStatistics();
            Assert.Equal(2, stats.FileCount);
            Assert.Equal(32, stats.EventCount);
            // Дедуп сохраняется после переноса: те же 2 уникальных SQL и 2 подключения.
            Assert.Equal(2, stats.UniqueSqlCount);
            Assert.Equal(2, stats.UniqueAttachmentCount);

            // Содержимое идентично исходному по одному из файлов.
            var original = SampleEvents();
            var read = dest.ReadFile("H1");
            Assert.Equal(original.Count, read.Count);
            for (var i = 0; i < original.Count; i++)
                Assert.Equal(Dump(original[i]), Dump(read[i]));
        }
        finally { TryDelete(srcDb); TryDelete(exportDb); TryDelete(destDb); }
    }

    [Fact]
    public void Import_SkipsFilesAlreadyPresent()
    {
        var srcDb = TempDb();
        var destDb = TempDb();
        try
        {
            using (var src = new EventStoreService(srcDb))
            {
                src.WriteFile(File("H1", "1.log"), SampleEvents());
                src.WriteFile(File("H2", "2.log"), SampleEvents());
            }

            using var dest = new EventStoreService(destDb);
            dest.WriteFile(File("H1", "1.log"), SampleEvents()); // H1 уже есть в приёмнике

            var imported = dest.ImportFrom(srcDb);

            Assert.Equal(1, imported); // импортирован только H2
            Assert.Equal(2, dest.GetStatistics().FileCount);
            Assert.True(dest.ContainsFile("H1"));
            Assert.True(dest.ContainsFile("H2"));
        }
        finally { TryDelete(srcDb); TryDelete(destDb); }
    }

    [Fact]
    public void SizeBreakdown_ReportsRowsAndTextPayload()
    {
        var db = TempDb();
        try
        {
            using var store = new EventStoreService(db);
            store.WriteFile(File("H", "f.log"), SampleEvents());

            var b = store.GetSizeBreakdown();

            Assert.Equal(16, b.EventRows);
            Assert.Equal(2, b.SqlTextRows);          // Sql1, Sql2 (дедуп)
            Assert.Equal(2, b.ErrorLineRows);        // единственный ErrorEvent с двумя строками
            Assert.True(b.ParameterRows > 0);         // несколько событий с параметрами
            Assert.True(b.SqlTextBytes > 0);
            Assert.True(b.DbSizeBytes > 0);
            // «Остальное» = размер БД − текстовые нагрузки, не отрицательное.
            Assert.True(b.OtherBytes >= 0);
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public void ExecuteQuery_ReturnsDynamicColumnsAndRows()
    {
        var db = TempDb();
        try
        {
            using var store = new EventStoreService(db);
            store.WriteFile(File("H", "f.log"), SampleEvents());

            var r = store.ExecuteQuery(
                "SELECT event_type AS et, COUNT(*) AS cnt FROM event GROUP BY event_type ORDER BY event_type",
                1000);

            Assert.Equal(new[] { "et", "cnt" }, r.Columns.ToArray());
            Assert.True(r.Rows.Count > 0);
            Assert.False(r.Truncated);
            // Сумма счётчиков по типам = всего событий.
            var total = r.Rows.Sum(row => Convert.ToInt64(row[1]));
            Assert.Equal(16, total);
        }
        finally { TryDelete(db); }
    }

    [Fact]
    public void ExecuteQuery_RejectsWrites_TruncatesAndStaysWritable()
    {
        var db = TempDb();
        try
        {
            using var store = new EventStoreService(db);
            store.WriteFile(File("H", "f.log"), SampleEvents());

            // Не-SELECT и цепочки операторов отклоняются (до выполнения).
            Assert.ThrowsAny<Exception>(() => store.ExecuteQuery("DELETE FROM event", 100));
            Assert.ThrowsAny<Exception>(() => store.ExecuteQuery("UPDATE files SET name='x'", 100));
            Assert.ThrowsAny<Exception>(() => store.ExecuteQuery("SELECT 1; DROP TABLE files", 100));

            // Усечение по лимиту строк.
            var r = store.ExecuteQuery("SELECT seq FROM event", maxRows: 5);
            Assert.Equal(5, r.Rows.Count);
            Assert.True(r.Truncated);

            // После запроса стор снова доступен для записи (PRAGMA query_only=OFF восстановлен).
            store.WriteFile(File("H2", "2.log"), SampleEvents());
            Assert.Equal(2, store.GetStatistics().FileCount);
        }
        finally { TryDelete(db); }
    }

    private static void TryDelete(string db)
    {
        foreach (var p in new[] { db, db + "-wal", db + "-shm" })
            try { if (System.IO.File.Exists(p)) System.IO.File.Delete(p); } catch { /* ignore */ }
    }
}
