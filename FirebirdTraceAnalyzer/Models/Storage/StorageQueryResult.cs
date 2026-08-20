namespace FirebirdTraceAnalyzer.Models.Storage;

/// <summary>
/// The result of an arbitrary SELECT over the store: dynamic columns plus rows of values. Decoupled
/// from domain types — suitable for tabular display and export.
/// </summary>
/// <param name="Columns">Column names in selection order.</param>
/// <param name="Rows">The rows; each is an array of values matching the column count (null = NULL).</param>
/// <param name="Truncated">true when there were more rows than the limit and only part is shown.</param>
/// <param name="ElapsedMs">Query execution time, in milliseconds.</param>
public sealed record StorageQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<object?[]> Rows,
    bool Truncated,
    long ElapsedMs);
