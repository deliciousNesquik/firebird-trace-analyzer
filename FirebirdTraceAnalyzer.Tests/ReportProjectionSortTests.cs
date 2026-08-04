using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Reports;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// M14: сортировка сгруппированного отчёта идентифицирует колонку по её Order (уникален), а не по
/// DisplayName. При дублирующихся именах колонок (одно поле как Sum и как Avg) сортировка должна
/// уходить в ВЫБРАННУЮ колонку, а не в первую по имени. Старые шаблоны (SortByColumn=DisplayName)
/// продолжают работать через откат.
/// </summary>
public sealed class ReportProjectionSortTests
{
    private static ReportProjectionService NewService() => new(new EventPropertyAccessor());

    private static EventBase Stmt(int traceId, int execMs) =>
        new StatementFinishEvent
        {
            Timestamp = new DateTime(2026, 7, 21, 10, 0, 0), TraceId = traceId, HexTraceId = "0x03",
            EventType = EventType.ExecuteStatementFinish,
            Attachment = new AttachmentInfo
            {
                AttachmentId = 1, DatabasePath = "/db", User = "U", Role = "NONE", Charset = "UTF8",
                Protocol = "TCPv4", Address = "a", Port = 3050, ProcessPath = null, ProcessId = null
            },
            Transaction = new TransactionInfo
            {
                TransactionId = 1, IsolationLevel = "READ_COMMITTED", ConsistencyMode = "REC_VERSION",
                LockMode = "NOWAIT", AccessMode = "READ_WRITE"
            },
            StatementId = 1, Sql = "SELECT 1", Parameters = new List<SqlParameters>(),
            Performance = new PerformanceInfo { ExecuteMs = execMs, FetchCount = 0, ReadCount = 0, WriteCount = 0, MarkCount = 0 },
            PerformanceTable = null
        };

    // Группы подобраны так, что порядок по Sum и по Avg РАЗНЫЙ:
    // Trace 1: [100]        → Sum=100, Avg=100
    // Trace 2: [40,40,40]   → Sum=120, Avg=40
    private static EventBase[] Events() =>
    [
        Stmt(1, 100),
        Stmt(2, 40), Stmt(2, 40), Stmt(2, 40)
    ];

    private static ReportTemplate TemplateSortedBy(string sortByColumn) => new()
    {
        Name = "T",
        SortDescending = false,
        Body = new ReportBody
        {
            GroupByFields = { "TraceId" },
            SortByColumn = sortByColumn,
            VisibleFields =
            {
                new EventField { DisplayName = "Trace", PropertyPath = "TraceId", Kind = ColumnKind.GroupKey, Order = 0 },
                new EventField { DisplayName = "Exec", PropertyPath = "Performance.ExecuteMs", Kind = ColumnKind.Aggregate, Aggregate = AggregateFunction.Sum, Order = 1 },
                new EventField { DisplayName = "Exec", PropertyPath = "Performance.ExecuteMs", Kind = ColumnKind.Aggregate, Aggregate = AggregateFunction.Average, Order = 2 }
            }
        }
    };

    [Fact]
    public void SortsByChosenColumnOrder_NotFirstDuplicateDisplayName()
    {
        // Сортируем по Order=2 (колонка Avg). Avg по возрастанию: Trace 2 (40) впереди Trace 1 (100).
        var table = NewService().BuildTable(TemplateSortedBy("2"), Events());

        Assert.Equal(2, Convert.ToInt32(table.Rows[0][0])); // первая строка — группа TraceId=2
        Assert.Equal(1, Convert.ToInt32(table.Rows[1][0]));
    }

    [Fact]
    public void SortByColumnOrder1_UsesSumColumn()
    {
        // Order=1 (колонка Sum). Sum по возрастанию: Trace 1 (100) впереди Trace 2 (120).
        var table = NewService().BuildTable(TemplateSortedBy("1"), Events());

        Assert.Equal(1, Convert.ToInt32(table.Rows[0][0]));
        Assert.Equal(2, Convert.ToInt32(table.Rows[1][0]));
    }

    [Fact]
    public void LegacyDisplayNameSort_StillFallsBackToFirstMatch()
    {
        // Старый шаблон: SortByColumn = DisplayName "Exec" → откат на первую колонку с этим именем (Sum).
        var table = NewService().BuildTable(TemplateSortedBy("Exec"), Events());

        Assert.Equal(1, Convert.ToInt32(table.Rows[0][0])); // как Sum-порядок
    }
}
