using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.Reports.Exporters;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// M10 (security): CSV-экспорт должен экранировать ячейки-формулы (=,+,-,@,TAB,CR). Данные приходят
/// из trace (текст SQL, параметры, имена приложений) и подконтрольны удалённому клиенту БД —
/// без экранирования Excel/LibreOffice исполнит их как формулы/DDE (CWE-1236).
/// </summary>
public sealed class CsvReportExporterInjectionTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "fta_csvinj_" + Guid.NewGuid().ToString("N") + ".csv");

    public void Dispose()
    {
        if (File.Exists(_out)) File.Delete(_out);
    }

    private sealed class FakeProjection(ReportTable table) : IReportProjectionService
    {
        public ReportTable BuildTable(ReportTemplate template, IReadOnlyList<EventBase> events) => table;
    }

    [Fact]
    public async Task Export_EscapesFormulaCell_SoItDoesNotRenderAsLiveFormula()
    {
        const string payload = "=cmd|'/c calc.exe'!A1";
        var table = new ReportTable(
            new[] { new ReportColumn("SQL", null, null, TextAlignment.Left) },
            new IReadOnlyList<object?>[] { new object?[] { payload } });

        var exporter = new CsvReportExporter(new FakeProjection(table));

        var template = new ReportTemplate
        {
            Name = "T",
            Body = new ReportBody
            {
                ShowSummary = false,
                Sections = { new ReportSection { ContentType = SectionContentType.Events, Order = 1 } }
            }
        };
        var metadata = new ReportMetadata { Events = [], Files = [], TotalEventsCount = 0 };

        await exporter.ExportAsync(template, metadata, _out);

        var lines = (await File.ReadAllTextAsync(_out)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Данные на месте.
        Assert.Contains(lines, l => l.Contains("cmd|'/c calc.exe'!A1"));
        // Но ни одно поле не начинается с '=' (после снятия возможных кавычек) — формула нейтрализована.
        Assert.DoesNotContain(lines, l => l.TrimStart('"').StartsWith('='));
    }
}
