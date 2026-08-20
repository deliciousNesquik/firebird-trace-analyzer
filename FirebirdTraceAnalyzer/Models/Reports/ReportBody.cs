using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Represents the body of a report, including visible fields, grouping, sorting, summary statistics, and sections.
/// </summary>
public sealed class ReportBody
{
    /// <summary>
    /// Fields of events that are visible in the report.
    /// </summary>
    public List<EventField> VisibleFields { get; init; } = new();

    /// <summary>
    /// Paths to properties by which events are grouped (like GROUP BY). Empty — without grouping,
    /// the report is built "row per event" (current behavior). If specified — the table is built
    /// "row per group": columns <see cref="ColumnKind.GroupKey"/> and <see cref="ColumnKind.Aggregate"/>.
    /// </summary>
    public List<string> GroupByFields { get; init; } = new();

    /// <summary>
    /// Column by which the report is sorted. If empty — no sorting is applied.
    /// </summary>
    public string? SortByColumn { get; init; }

    /// <summary>
    /// Indicates whether to show summary statistics.
    /// </summary>
    public bool ShowSummary { get; init; } = true;

    /// <summary>
    /// Sections of the report.
    /// </summary>
    public List<ReportSection> Sections { get; init; } = new();
}