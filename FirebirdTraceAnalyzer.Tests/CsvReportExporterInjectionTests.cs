using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.Reports.Exporters;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// M10 (security): Csv-экспорт должен экранировать ячейки-формулы (=,+,-,@,TAB,CR). Данные приходят
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

    // Все опасные префиксы формул/DDE, а не только '=': если будущая правка сузит набор, тест это поймает.
    [Theory]
    [InlineData("=cmd|'/c calc.exe'!A1")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1:A9)")]
    public async Task Export_EscapesFormulaCell_SoItDoesNotRenderAsLiveFormula(string payload)
    {
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

        // Данные на месте (часть после опасного префикса).
        Assert.Contains(lines, l => l.Contains(payload[1..]));
        // Но ни одно поле (после снятия возможных кавычек) не начинается с формульного символа —
        // нейтрализовано. (\t/\r не проверяем: сам escape-символ CsvHelper может быть табом.)
        Assert.DoesNotContain(lines, l =>
        {
            var field = l.TrimStart('"');
            return field.Length > 0 && "=+-@".Contains(field[0]);
        });
    }
}
