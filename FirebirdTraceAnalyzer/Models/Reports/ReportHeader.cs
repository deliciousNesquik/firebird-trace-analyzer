namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Заголовок отчёта с мета-информацией
/// </summary>
public sealed class ReportHeader
{
    /// <summary>Название отчёта (шаблонизируемое)</summary>
    public string Title { get; init; } = "Trace Analysis Report";
    
    /// <summary>Подзаголовок</summary>
    public string? Subtitle { get; init; }
    
    /// <summary>Показывать логотип?</summary>
    public bool ShowLogo { get; init; } = true;
    
    /// <summary>Переменные для отображения в заголовке</summary>
    public List<ReportVariable> Variables { get; init; } = new();
    
    /// <summary>Показывать дату генерации?</summary>
    public bool ShowGeneratedDate { get; init; } = true;
    
    /// <summary>Формат даты</summary>
    public string DateFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";
}