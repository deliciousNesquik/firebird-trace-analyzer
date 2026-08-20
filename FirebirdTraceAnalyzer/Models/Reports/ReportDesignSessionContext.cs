using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Represents the context of a report design session, including source events, files, and total event count.
/// </summary>
public sealed class ReportDesignSessionContext
{
    /// <summary>
    /// Source events for the current session. These events are used to generate reports and previews in the report designer.
    /// </summary>
    public required IReadOnlyList<EventBase> SourceEvents { get; init; }

    /// <summary>
    /// Files that were used to generate the current session. These files are used to generate reports and previews in the report designer.
    /// </summary>
    public required IReadOnlyList<TraceFileInfoModel> Files { get; init; }

    /// <summary>
    /// Total number of events in the current session. This value is used to generate reports and previews in the report designer.
    /// </summary>
    public required long TotalEventsCount { get; init; }
}
