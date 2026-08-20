using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;

namespace FirebirdTraceAnalyzer.Interfaces.Reports.Exporters;

/// <summary>
/// Defines the interface for a report exporter that can export reports in a specific format.
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Gets the format of the report that this exporter supports.
    /// </summary>
    ReportFormat Format { get; }

    /// <summary>
    /// Exports the report to the specified output path asynchronously.
    /// </summary>
    /// <param name="template">The report template.</param>
    /// <param name="metadata">The report metadata.</param>
    /// <param name="outputPath">The path to save the exported report.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        string outputPath,
        CancellationToken cancellationToken = default);
}