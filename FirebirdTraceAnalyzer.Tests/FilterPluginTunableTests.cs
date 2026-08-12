using System.Globalization;
using FirebirdTraceAnalyzer.Services;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Доказывает, что плагин-фильтр может выразить СОСТАВНОЕ условие с параметром, настраиваемым
/// пользователем в рантайме: «все StatementFinish на WAIT-транзакции, у которых время исполнения
/// >= порога». Порог берётся из живого <see cref="FilterDescriptor.CurrentMinValue"/> (как его правит
/// пользователь в редакторе диапазона). Кручость: фильтр РЕГИСТРИРУЕТСЯ через RegisterCustomFilter,
/// поэтому FilteringService.CompilePredicate возвращает предикат плагина как есть — это и есть
/// реальный путь плагина (в отличие от FilteringServiceApplyTests, где фильтры не регистрируются и
/// идут по встроенной ветке).
/// </summary>
public sealed class FilterPluginTunableTests
{
    private static FilteringService NewService() =>
        new(new EventPropertyAccessor(), new FieldDiscoveryService());

    // --- Фикстуры событий ------------------------------------------------------------------

    private static AttachmentInfo Att() => new()
    {
        DatabasePath = "/db.fdb", AttachmentId = 1, User = "SYSDBA", Role = "NONE",
        Charset = "UTF8", Protocol = "TCPv4", Address = "127.0.0.1", Port = 3050,
    };

    private static PerformanceInfo Perf(int executeMs) => new()
    {
        ExecuteMs = executeMs, FetchCount = 0, ReadCount = 0, WriteCount = 0, MarkCount = 0,
    };

    private static StatementFinishEvent Stmt(string? lockMode, int executeMs) => new()
    {
        Timestamp = new DateTime(2026, 7, 21, 10, 0, 0),
        TraceId = 1, HexTraceId = "0x01", EventType = EventType.ExecuteStatementFinish,
        Attachment = Att(),
        Transaction = lockMode is null ? null : new TransactionInfo { LockMode = lockMode },
        StatementId = 1,
        Sql = "SELECT 1 FROM RDB$DATABASE",
        Parameters = Array.Empty<SqlParameters>(),
        Performance = Perf(executeMs),
    };

    private static ProcedureFinishEvent Proc(string lockMode, int executeMs) => new()
    {
        Timestamp = new DateTime(2026, 7, 21, 10, 0, 0),
        TraceId = 1, HexTraceId = "0x01", EventType = EventType.ExecuteProcedureFinish,
        Attachment = Att(),
        Transaction = new TransactionInfo { LockMode = lockMode },
        ProcedureName = "SP_X",
        Parameters = Array.Empty<SqlParameters>(),
        Performance = Perf(executeMs),
    };

    // --- Фильтр (ровно как в TemplatePlugin.TemplateTunableFilterPlugin) --------------------

    private static FilterDescriptor BuildWaitSlowFilter()
    {
        var descriptor = new FilterDescriptor(
            "acme.filter.slow_wait_statements",
            "Slow WAIT statements",
            FilterType.NumericRange,          // рисует редактор диапазона From/To
            "Performance.ExecuteMs",           // подпись + авто-подбор границ; фильтрует ПРЕДИКАТ
            _ => true,
            "Analytics")
        {
            MinValue = 0,
            MaxValue = 100_000,               // MinValue != null → редактор виден
        };

        // Предикат читает ЖИВЫЕ границы дескриптора при каждом применении.
        descriptor.UpdatePredicate(e => Match(e, descriptor));
        return descriptor;
    }

    private static bool Match(EventBase e, FilterDescriptor d)
    {
        if (e is not StatementFinishEvent s)
            return false;
        if (!string.Equals(s.Transaction?.LockMode, "WAIT", StringComparison.OrdinalIgnoreCase))
            return false;

        var ms = s.Performance.ExecuteMs;
        if (AsInt(d.CurrentMinValue) is { } lo && ms < lo)
            return false;
        if (AsInt(d.CurrentMaxValue) is { } hi && ms > hi)
            return false;
        return true;
    }

    // Границы приходят из TwoWay-биндинга TextBox — могут быть int ЛИБО строкой; непарсимое = «нет границы».
    private static int? AsInt(object? v) => v switch
    {
        null => null,
        int i => i,
        _ => int.TryParse(v.ToString(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var n) ? n : null,
    };

    // --- Тесты -----------------------------------------------------------------------------

    private static EventBase[] Mixed() =>
    [
        Stmt("WAIT", 500),     // WAIT, но быстрый
        Stmt("WAIT", 1500),    // WAIT + медленный  ← подходит при пороге 1000
        Stmt("NOWAIT", 2000),  // медленный, но NOWAIT
        Proc("WAIT", 3000),    // WAIT + медленный, но это процедура, не statement
        Stmt("WAIT", 5000),    // WAIT + очень медленный ← подходит при любом пороге <= 5000
        Stmt(null, 4000),      // без транзакции
    ];

    [Fact]
    public void Threshold1000_KeepsOnlyWaitStatementsAtOrAboveThreshold()
    {
        var svc = NewService();
        var filter = BuildWaitSlowFilter();
        svc.RegisterCustomFilter(filter);   // ← реальный путь плагина

        filter.IsActive = true;
        filter.CurrentMinValue = 1000;

        var result = svc.ApplyFilters(Mixed(), new[] { filter })
            .Cast<StatementFinishEvent>()
            .Select(s => s.Performance.ExecuteMs)
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(new[] { 1500, 5000 }, result); // NOWAIT/процедура/null и быстрый WAIT отсеяны
    }

    [Fact]
    public void RaisingThreshold_ShrinksResult_ProvesRuntimeTunable()
    {
        var svc = NewService();
        var filter = BuildWaitSlowFilter();
        svc.RegisterCustomFilter(filter);
        filter.IsActive = true;

        filter.CurrentMinValue = 1000;
        var atThousand = svc.ApplyFilters(Mixed(), new[] { filter }).Count();

        filter.CurrentMinValue = 5000; // пользователь поднял порог
        var atFiveThousand = svc.ApplyFilters(Mixed(), new[] { filter }).Count();

        Assert.Equal(2, atThousand);
        Assert.Equal(1, atFiveThousand); // остаётся только WAIT-statement на 5000 мс
    }

    [Fact]
    public void StringBound_FromTextBox_IsCoerced()
    {
        var svc = NewService();
        var filter = BuildWaitSlowFilter();
        svc.RegisterCustomFilter(filter);
        filter.IsActive = true;
        filter.CurrentMinValue = "1000"; // строка, как её кладёт TwoWay TextBox

        Assert.Equal(2, svc.ApplyFilters(Mixed(), new[] { filter }).Count());
    }

    [Fact]
    public void NoThreshold_ActiveFilter_KeepsAllWaitStatements()
    {
        var svc = NewService();
        var filter = BuildWaitSlowFilter();
        svc.RegisterCustomFilter(filter);
        filter.IsActive = true;
        // CurrentMinValue/CurrentMaxValue не заданы → порога нет: составное условие всё равно работает.

        var kinds = svc.ApplyFilters(Mixed(), new[] { filter }).ToList();

        Assert.All(kinds, e => Assert.IsType<StatementFinishEvent>(e));
        Assert.All(kinds, e => Assert.Equal("WAIT", ((StatementFinishEvent)e).Transaction?.LockMode));
        Assert.Equal(3, kinds.Count); // 500 / 1500 / 5000 — все WAIT-statement
    }
}
