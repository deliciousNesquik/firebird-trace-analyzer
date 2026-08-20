using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Reports;

/// <summary>
/// Defines a service for projecting events into a report table (<see cref="ReportTable"/>) based on a specified template.
/// </summary>
public interface IReportProjectionService
{
    /// <summary>
    /// Builds a report table from the provided template and events.
    /// </summary>
    /// <param name="template">The report template.</param>
    /// <param name="events">The list of events to include in the report.</param>
    /// <returns>The generated report table.</returns>
    ReportTable BuildTable(ReportTemplate template, IReadOnlyList<EventBase> events);
}
