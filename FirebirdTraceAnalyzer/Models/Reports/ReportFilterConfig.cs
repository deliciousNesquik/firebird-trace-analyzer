namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Filter configuration for a report.
/// </summary>
public sealed class ReportFilterConfig
{
    /// <summary>Filter ID (e.g. "filter_eventtype").</summary>
    public required string FilterId { get; init; }

    /// <summary>
    /// Path to the event property (e.g. <c>EventType</c>, <c>Attachment.User</c>).
    /// Takes precedence over <see cref="FilterId"/> when the filter is applied in the report.
    /// </summary>
    public string? PropertyPath { get; init; }

    /// <summary>Display name of the filter.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the filter is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Selected values — an event must match one of them (Enum/String filters).</summary>
    public List<object>? SelectedValues { get; init; }

    /// <summary>Excluded values — events with these values are discarded (Enum/String filters).</summary>
    public List<object>? ExcludedValues { get; init; }

    /// <summary>Minimum value (for Range filters).</summary>
    public object? MinValue { get; init; }

    /// <summary>Maximum value (for Range filters).</summary>
    public object? MaxValue { get; init; }
}