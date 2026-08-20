using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Reports;

/// <summary>
/// Defines the interface for a report generation service that generates reports based on templates, metadata, and formats.
/// </summary>
public interface IReportGenerationService
{
    /// <summary>
    /// Generates a report based on the provided template, metadata, and format.
    /// The report can be saved to the specified output path if provided.
    /// </summary>
    /// <param name="template">The report template.</param>
    /// <param name="metadata">The report metadata.</param>
    /// <param name="format">The format of the report.</param>
    /// <param name="outputPath">The path to save the generated report.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<GeneratedReport> GenerateReportAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        ReportFormat format,
        string? outputPath = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Prepares the events for report generation based on the provided visible events and report template.
    /// </summary>
    /// <param name="visibleEvents">The visible events.</param>
    /// <param name="template">The report template.</param>
    /// <returns>The prepared events.</returns>
    IReadOnlyList<EventBase> PrepareEventsForReport(
        IEnumerable<EventBase> visibleEvents,
        ReportTemplate template);
}