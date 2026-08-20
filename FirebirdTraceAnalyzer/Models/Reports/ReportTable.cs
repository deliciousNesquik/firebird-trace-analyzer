using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// A report table column — everything an exporter needs to render the header and cells, independent of
/// how the column was produced (an event field, a grouping key, or an aggregate).
/// </summary>
/// <param name="DisplayName">The column header text.</param>
/// <param name="Format">Optional format string applied by the exporter when rendering cell values.</param>
/// <param name="WidthPercent">Optional column width as a percentage of the table width.</param>
/// <param name="Alignment">Horizontal text alignment for the column.</param>
public sealed record ReportColumn(
    string DisplayName,
    string? Format,
    int? WidthPercent,
    TextAlignment Alignment);

/// <summary>
/// A ready-to-render report table: a set of columns and rows of raw cell values (formatting is still
/// applied by the exporters via ReportValueFormatter and Column.Format). An intermediate layer between
/// the data and the exporters: per-event and (in the future) aggregated reports produce the same shape,
/// so exporters do not care how the table was built.
/// </summary>
/// <param name="Columns">The table columns.</param>
/// <param name="Rows">The rows, each a list of raw cell values aligned to <paramref name="Columns"/>.</param>
public sealed record ReportTable(
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows);
