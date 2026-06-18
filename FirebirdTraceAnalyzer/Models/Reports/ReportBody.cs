using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Тело отчёта - основное содержимое
/// </summary>
public sealed class ReportBody
{
    /// <summary>Стиль отображения событий</summary>
    public EventDisplayStyle DisplayStyle { get; init; } = EventDisplayStyle.Table;
    
    /// <summary>Поля событий для отображения</summary>
    public List<EventField> VisibleFields { get; init; } = new();

    /// <summary>
    /// Пути свойств, по которым группируются события (как GROUP BY). Пусто — без группировки,
    /// отчёт строится «строка на событие» (текущее поведение). Если задано — таблица строится
    /// «строка на группу»: колонки <see cref="ColumnKind.GroupKey"/> и <see cref="ColumnKind.Aggregate"/>.
    /// </summary>
    public List<string> GroupByFields { get; init; } = new();

    /// <summary>
    /// Для агрегированного (сгруппированного) отчёта — DisplayName видимой колонки, по которой
    /// сортируются строки-группы (может быть ключом группировки ИЛИ агрегатом). Направление —
    /// в <see cref="ReportTemplate.SortDescending"/>. Пусто — порядок групп по первому появлению.
    /// Не влияет на негруппированные отчёты (там сортируются события через ISortingService).
    /// </summary>
    public string? SortByColumn { get; init; }

    /// <summary>Показывать итоговую статистику?</summary>
    public bool ShowSummary { get; init; } = true;
    
    /// <summary>Секции отчёта</summary>
    public List<ReportSection> Sections { get; init; } = new();
}