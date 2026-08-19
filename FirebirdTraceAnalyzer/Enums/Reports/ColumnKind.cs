namespace FirebirdTraceAnalyzer.Enums.Reports;

/// <summary>
/// Enumeration representing the kind of column in a report.
/// </summary>
public enum ColumnKind
{
    /// <summary>
    /// Represents a field column in the report, which corresponds to a specific data field from the source data.
    /// </summary>
    Field,

    /// <summary>
    /// Represents a group key column in the report, which corresponds to a specific data field from the source data.
    /// </summary>
    GroupKey,

    /// <summary>
    /// Represents an aggregate column in the report, which corresponds to an aggregated value over a group of events.
    /// </summary>
    Aggregate
}
