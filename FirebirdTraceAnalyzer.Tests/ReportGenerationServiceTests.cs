using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Reports.Exporters;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Reports;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// T3/A11: ReportGenerationService строит реестр форматов из внедрённой коллекции экспортёров
/// (каждый объявляет свой Format). Проверяем корректный выбор и отказ для незарегистрированного.
/// </summary>
public sealed class ReportGenerationServiceTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "fta_report_" + Guid.NewGuid().ToString("N") + ".bin");

    public void Dispose()
    {
        if (File.Exists(_out)) File.Delete(_out);
    }

    private sealed class FakeExporter(ReportFormat format) : IReportExporter
    {
        public ReportFormat Format { get; } = format;
        public bool Called { get; private set; }

        public Task ExportAsync(ReportTemplate template, ReportMetadata metadata, string outputPath, CancellationToken ct = default)
        {
            Called = true;
            File.WriteAllText(outputPath, "x"); // чтобы FileInfo.Length не бросал
            return Task.CompletedTask;
        }
    }

    private static ReportGenerationService NewService(params IReportExporter[] exporters) =>
        new(exporters, new EventPropertyAccessor(), new ThrowingSettings());

    private static ReportTemplate Template() => new() { Name = "T" };
    private static ReportMetadata Metadata() => new()
    {
        Events = [],
        Files = [],
        TotalEventsCount = 0
    };

    [Fact]
    public async Task DispatchesToExporter_MatchingFormat()
    {
        var pdf = new FakeExporter(ReportFormat.Pdf);
        var csv = new FakeExporter(ReportFormat.Csv);
        var svc = NewService(pdf, csv);

        var report = await svc.GenerateReportAsync(Template(), Metadata(), ReportFormat.Csv, _out);

        Assert.True(csv.Called);
        Assert.False(pdf.Called);
        Assert.Equal(ReportFormat.Csv, report.Format);
    }

    [Fact]
    public async Task UnregisteredFormat_Throws()
    {
        var svc = NewService(new FakeExporter(ReportFormat.Pdf));
        await Assert.ThrowsAsync<NotSupportedException>(
            () => svc.GenerateReportAsync(Template(), Metadata(), ReportFormat.Docx, _out));
    }

    [Fact]
    public void DuplicateFormat_FirstWins_NoThrow()
    {
        var first = new FakeExporter(ReportFormat.Pdf);
        var second = new FakeExporter(ReportFormat.Pdf);
        // Конструктор не должен падать при дубле формата (второй игнорируется с предупреждением).
        var ex = Record.Exception(() => NewService(first, second));
        Assert.Null(ex);
    }

    [Fact]
    public void PrepareEvents_SortByField_EventsWithoutValue_GoLast_InBothDirections()
    {
        // Сортировка отчёта — по тем же правилам, что и главная форма: без значения — всегда в конце,
        // в исходном порядке.
        static EventBase WithDuration(int traceId, int? seconds) => new TraceInitEvent
        {
            Timestamp = new DateTime(2026, 7, 21, 10, 0, 0), TraceId = traceId, HexTraceId = "0x01",
            EventType = EventType.TraceInit, Session = new TraceSessionInfo { SessionId = 1 },
            Duration = seconds is null ? null : TimeSpan.FromSeconds(seconds.Value)
        };

        var events = new[]
        {
            WithDuration(1, null), WithDuration(2, 5), WithDuration(3, null), WithDuration(4, 60), WithDuration(5, 20)
        };

        foreach (var descending in new[] { false, true })
        {
            var template = new ReportTemplate { Name = "T", SortByField = "Duration", SortDescending = descending };
            var ids = NewService().PrepareEventsForReport(events, template).Select(e => e.TraceId).ToArray();

            int[] expected = descending ? [4, 5, 2, 1, 3] : [2, 5, 4, 1, 3];
            Assert.Equal(expected, ids);
        }
    }

    // Настройки не используются, когда outputPath задан явно — стаб только чтобы удовлетворить ctor.
    private sealed class ThrowingSettings : ISettingsService
    {
        public AppSettings App => throw new NotImplementedException();
        public UiSectionSettings Ui => throw new NotImplementedException();
        public WindowSettings Window => throw new NotImplementedException();
        public string GetRemoteDownloadDirectory() => throw new NotImplementedException();
        public string GetReportsDirectory() => throw new NotImplementedException();
        public string GetEventStoreDirectory() => throw new NotImplementedException();
        public void Save() => throw new NotImplementedException();
        public UserSettings GetDefaults() => throw new NotImplementedException();
        public Task ExportAsync(string path, UserSettings settings) => throw new NotImplementedException();
        public Task<UserSettings> ReadFromFileAsync(string path) => throw new NotImplementedException();
    }
}
