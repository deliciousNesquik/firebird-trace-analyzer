using FirebirdTraceAnalyzer.Models.Storage;
using FirebirdTraceAnalyzer.Services.Storage;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>Проверяет генерацию SQL конструктором «Анализ хранилища».</summary>
public sealed class StorageQueryBuilderTests
{
    private static readonly DateTime Now = new(2026, 07, 15, 12, 00, 00, DateTimeKind.Utc);

    [Fact]
    public void UserActivity_BuildsGroupedAggregateWithJoinAndPeriod()
    {
        var sql = StorageQueryBuilder.Build(
            dimensions: [new QueryDimensionOption("user", "Пользователь")],
            measures: [new QueryMeasureOption("count", "Событий"), new QueryMeasureOption("files", "Файлов")],
            period: QueryPeriod.Week,
            user: null,
            limit: 1000,
            now: Now);

        Assert.Contains("a.user AS \"Пользователь\"", sql);
        Assert.Contains("COUNT(*) AS \"Событий\"", sql);
        Assert.Contains("COUNT(DISTINCT e.file_id) AS \"Файлов\"", sql);
        Assert.Contains("FROM event e", sql);
        Assert.Contains("LEFT JOIN attachment a ON a.id = e.attachment_ref", sql);
        Assert.Contains("GROUP BY a.user", sql);
        Assert.Contains("ORDER BY \"Событий\" DESC", sql); // сортировка по первому показателю
        Assert.Contains("LIMIT 1000", sql);
        // Период «7 дней» → фильтр по тикам от начала (now.Date - 6).
        Assert.Contains($"e.ts >= {Now.Date.AddDays(-6).Ticks}", sql);
    }

    [Fact]
    public void EmptySelection_FallsBackToCountStar()
    {
        var sql = StorageQueryBuilder.Build([], [], QueryPeriod.AllTime, null, 500, Now);

        Assert.Contains("COUNT(*) AS \"count\"", sql);
        Assert.Contains("FROM event e", sql);
        Assert.DoesNotContain("GROUP BY", sql);
        Assert.DoesNotContain("WHERE", sql); // AllTime → без фильтра периода
        Assert.Contains("LIMIT 500", sql);
    }

    [Fact]
    public void UserFilter_ForcesAttachmentJoinAndEscapesQuotes()
    {
        var sql = StorageQueryBuilder.Build(
            dimensions: [],
            measures: [new QueryMeasureOption("count", "Событий")],
            period: QueryPeriod.AllTime,
            user: "O'Brien",
            limit: 100,
            now: Now);

        Assert.Contains("LEFT JOIN attachment a ON a.id = e.attachment_ref", sql);
        Assert.Contains("a.user = 'O''Brien'", sql); // одинарная кавычка экранирована удвоением
    }
}
