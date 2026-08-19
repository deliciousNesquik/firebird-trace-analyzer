namespace FirebirdTraceAnalyzer.Enums.Reports;

/// <summary>
/// Enumeration representing the available aggregate functions for report generation.
/// </summary>
public enum AggregateFunction
{
    /// <summary>Count aggregation function</summary>
    Count,

    /// <summary>Unique count aggregation function</summary>
    CountDistinct,

    /// <summary>Sum aggregation function</summary>
    Sum,

    /// <summary>Average aggregation function</summary>
    Average,

    /// <summary>Minimum aggregation function</summary>
    Min,

    /// <summary>Maximum aggregation function</summary>
    Max
}
