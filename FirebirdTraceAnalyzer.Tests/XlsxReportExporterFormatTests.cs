using ClosedXML.Excel;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.Reports.Exporters;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// M11: XLSX-экспорт должен переводить .NET-формат колонки ("N0") в код формата Excel ("#,##0").
/// Иначе Excel показывает "N0" буквально — число 1234 рисуется как "N1234".
/// </summary>
public sealed class XlsxReportExporterFormatTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "fta_xlsxfmt_" + Guid.NewGuid().ToString("N") + ".xlsx");

    public void Dispose()
    {
        if (File.Exists(_out)) File.Delete(_out);
    }

    private sealed class FakeProjection(ReportTable table) : IReportProjectionService
    {
        public ReportTable BuildTable(ReportTemplate template, IReadOnlyList<EventBase> events) => table;
    }

    [Fact]
    public async Task Export_TranslatesDotNetNumberFormat_ToExcelCode()
    {
        var table = new ReportTable(
            new[] { new ReportColumn("Reads", "N0", null, TextAlignment.Right) },
            new IReadOnlyList<object?>[] { new object?[] { 1234 } });

        var exporter = new XlsxReportExporter(new FakeProjection(table));

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

        using var wb = new XLWorkbook(_out);
        var ws = wb.Worksheets.First();
        var cell = ws.CellsUsed().First(c => c.Value.IsNumber && Math.Abs(c.GetDouble() - 1234) < 0.001);

        // Значение хранится как число, а формат — валидный код Excel (группировка тысяч), а не ".NET N0".
        Assert.Equal("#,##0", cell.Style.NumberFormat.Format);
    }
}
