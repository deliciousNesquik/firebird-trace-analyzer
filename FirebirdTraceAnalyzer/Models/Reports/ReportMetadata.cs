using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Runtime metadata used to generate a report.
/// </summary>
public sealed class ReportMetadata
{
    /// <summary>Events included in the report.</summary>
    public required IReadOnlyList<EventBase> Events { get; init; }

    /// <summary>Information about the source files.</summary>
    public required IReadOnlyList<TraceFileInfoModel> Files { get; init; }

    /// <summary>Total number of events before filtering.</summary>
    public required long TotalEventsCount { get; init; }

    /// <summary>Active filters as a textual description.</summary>
    public string? ActiveFilters { get; init; }

    /// <summary>Active sort as a textual description.</summary>
    public string? ActiveSort { get; init; }

    /// <summary>When the report was generated.</summary>
    public DateTime GeneratedAt { get; init; } = DateTime.Now;

    /// <summary>Application version.</summary>
    public string ApplicationVersion { get; init; } = "1.0.0";
}