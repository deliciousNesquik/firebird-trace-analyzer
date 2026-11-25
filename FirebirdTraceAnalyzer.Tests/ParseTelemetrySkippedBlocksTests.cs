using FirebirdTraceAnalyzer.Services.Diagnostics;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Пропущенные блоки (Severity ≥ Warning, см. GLOSSARY.md) в телеметрии и dev-диалоге:
/// счётчик доезжает до снапшота-копии, кэш отображается «—», сводка суммирует только
/// известные значения. Термин закреплён решением по фиче «пропуски в статистике парсера».
/// </summary>
public sealed class ParseTelemetrySkippedBlocksTests
{
    [Fact]
    public void RecordProduce_StoresSkippedBlocks_AndSnapshotIsIndependentCopy()
    {
        var telemetry = new ParseTelemetryService();
        telemetry.RecordProduce("bad.log", ms: 100, eventCount: 10, sizeBytes: 1024,
            ParseSource.LocalParse, fromCache: false, skippedBlocks: 5);

        var snapshot = telemetry.Snapshot();

        var metric = Assert.Single(snapshot);
        Assert.Equal(5, metric.SkippedBlocks);
        Assert.False(metric.FromCache);

        // Снапшот — копия: правка копии не должна протекать в телеметрию.
        metric.SkippedBlocks = 99;
        Assert.Equal(5, telemetry.Snapshot().Single().SkippedBlocks);
    }

    [Fact]
    public void SkippedDisplay_CacheShowsDash_ParsedShowsInvariantNumber()
    {
        var cache = new FileParseMetric { Name = "c.log", FromCache = true, SkippedBlocks = 0 };
        var clean = new FileParseMetric { Name = "ok.log", FromCache = false, SkippedBlocks = 0 };
        var dirty = new FileParseMetric { Name = "bad.log", FromCache = false, SkippedBlocks = 12 };

        // Кэш: счётчик неизвестен (не персистится) — «—», а не ноль.
        Assert.Equal("—", cache.SkippedDisplay);
        Assert.Equal("0", clean.SkippedDisplay);
        Assert.Equal("12", dirty.SkippedDisplay);
    }

    [Fact]
    public void ParserStatisticsViewModel_TotalSkipped_SumsParsedFilesOnly()
    {
        var telemetry = new ParseTelemetryService();
        telemetry.RecordProduce("a.log", 10, 100, 1024, ParseSource.LocalParse, fromCache: false, skippedBlocks: 3);
        telemetry.RecordProduce("cached.log", 10, 100, 1024, ParseSource.StoreCache, fromCache: true, skippedBlocks: 0);
        telemetry.RecordProduce("b.log", 10, 100, 1024, ParseSource.RemoteParse, fromCache: false, skippedBlocks: 4);

        var vm = new ParserStatisticsViewModel(telemetry);
        vm.Load();

        Assert.Equal(3, vm.FileCount);
        Assert.Equal(7, vm.TotalSkipped);
    }
}
