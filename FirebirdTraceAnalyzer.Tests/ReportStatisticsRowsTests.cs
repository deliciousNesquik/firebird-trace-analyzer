using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.Reports;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// A6: общий источник строк статистики для PDF/DOCX/XLSX. Фиксируем состав, порядок и форматирование
/// значений — чтобы дедуп не изменил вывод отчётов (обязательные 3 поля + 2 условных).
/// </summary>
public sealed class ReportStatisticsRowsTests
{
    private static ReportMetadata Meta(int files, int total, int inReport, string? filters, string? sort) => new()
    {
        Events = Enumerable.Range(0, inReport).Select(_ => (EventBase)null!).ToList(),
        Files = Enumerable.Range(0, files).Select(_ => (TraceFileInfoModel)null!).ToList(),
        TotalEventsCount = total,
        ActiveFilters = filters,
        ActiveSort = sort
    };

    [Fact]
    public void WithoutFiltersOrSort_HasThreeRows_WithExpectedValues()
    {
        var rows = ReportStatisticsRows.Build(Meta(files: 2, total: 1234, inReport: 42, filters: null, sort: null));

        Assert.Equal(3, rows.Count);
        Assert.Equal("2", rows[0].Value);
        Assert.Equal(1234.ToString("N0"), rows[1].Value);   // то же форматирование, что в экспортёрах
        Assert.Equal(42.ToString("N0"), rows[2].Value);
    }

    [Fact]
    public void WithFiltersAndSort_AppendsTwoConditionalRows_InOrder()
    {
        var rows = ReportStatisticsRows.Build(Meta(1, 10, 5, "f=x", "ts ASC"));

        Assert.Equal(5, rows.Count);
        Assert.Equal("f=x", rows[3].Value);
        Assert.Equal("ts ASC", rows[4].Value);
    }

    [Theory]
    [InlineData("", null, 3)]
    [InlineData(null, "", 3)]
    [InlineData("f", null, 4)]
    [InlineData(null, "s", 4)]
    [InlineData("  ", "  ", 3)] // whitespace трактуется как пусто
    public void ConditionalRows_IncludedOnlyWhenNonEmpty(string? filters, string? sort, int expectedCount)
    {
        Assert.Equal(expectedCount, ReportStatisticsRows.Build(Meta(1, 1, 1, filters, sort)).Count);
    }
}
