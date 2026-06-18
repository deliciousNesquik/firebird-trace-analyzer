using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Колонка таблицы отчёта — то, что экспортёру нужно для отрисовки заголовка и ячеек,
/// без привязки к тому, как колонка получена (поле события, ключ группировки или агрегат).
/// </summary>
public sealed record ReportColumn(
    string DisplayName,
    string? Format,
    int? WidthPercent,
    TextAlignment Alignment);

/// <summary>
/// Готовая к отрисовке таблица отчёта: набор колонок и строки с «сырыми» значениями ячеек
/// (форматирование по-прежнему делают экспортёры через ReportValueFormatter и Column.Format).
/// Промежуточный слой между данными и экспортёрами: per-event и (в будущем) агрегированные
/// отчёты дают одинаковую форму, поэтому экспортёрам всё равно, как таблица построена.
/// </summary>
public sealed record ReportTable(
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows);
