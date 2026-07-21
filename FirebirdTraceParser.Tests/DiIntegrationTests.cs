using FirebirdTraceParser.Infrastructure.DependencyInjection;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Parsing.Engine;
using FirebirdTraceParser.Parsing.Handlers;
using Microsoft.Extensions.DependencyInjection;
using static FirebirdTraceParser.Tests.TestSupport;

namespace FirebirdTraceParser.Tests;

/// <summary>
/// Проверяет, что весь граф DI собирается и, главное, что настроенный ParseOptions реально доходит
/// до DefaultEventHandler (критический баг C1 — раньше настройка молча игнорировалась).
/// </summary>
public sealed class DiIntegrationTests
{
    private static List<string> StatementFinishBodyWithPerfTable() =>
    [
        AttachmentLine, ProcessLine, TransactionLine,
        "Statement 1:", SqlDashes, "SELECT 1",
        "1 records fetched", PerformanceLine,
        "Table                              Natural     Index    Update    Insert    Delete   Backout     Purge   Expunge",
        "RDB$INDICES                                       25",
        ""
    ];

    [Fact]
    public void Di_ResolvesFullGraph()
    {
        var services = new ServiceCollection();
        services.AddFirebirdTraceParser(RulesPath);
        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<ITraceLogParser>());
        Assert.NotNull(sp.GetRequiredService<IEventHandler>());
    }

    [Fact]
    public void Di_ParsePerformanceTables_DefaultTrue_TableParsed()
    {
        var services = new ServiceCollection();
        services.AddFirebirdTraceParser(RulesPath);
        using var sp = services.BuildServiceProvider();
        var handler = sp.GetRequiredService<IEventHandler>();

        var evt = handler.Handle(Header(HeaderLine("EXECUTE_STATEMENT_FINISH")),
            StatementFinishBodyWithPerfTable(), Rules, NewContext());
        var f = Assert.IsType<StatementFinishEvent>(evt);
        Assert.NotNull(f.PerformanceTable);
    }

    [Fact]
    public void Di_ParsePerformanceTablesFalse_IsHonored_ThroughDi()
    {
        // Ключевая проверка C1: with-трансформ опций должен дойти до обработчика.
        var services = new ServiceCollection();
        services.AddFirebirdTraceParser(RulesPath, o => o with { ParsePerformanceTables = false });
        using var sp = services.BuildServiceProvider();
        var handler = sp.GetRequiredService<IEventHandler>();

        var evt = handler.Handle(Header(HeaderLine("EXECUTE_STATEMENT_FINISH")),
            StatementFinishBodyWithPerfTable(), Rules, NewContext());
        var f = Assert.IsType<StatementFinishEvent>(evt);
        Assert.Null(f.PerformanceTable); // опция применилась → таблица не выставлена
    }
}
