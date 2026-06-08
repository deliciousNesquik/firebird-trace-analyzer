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
    
    /// <summary>Показывать итоговую статистику?</summary>
    public bool ShowSummary { get; init; } = true;
    
    /// <summary>Секции отчёта</summary>
    public List<ReportSection> Sections { get; init; } = new();
}