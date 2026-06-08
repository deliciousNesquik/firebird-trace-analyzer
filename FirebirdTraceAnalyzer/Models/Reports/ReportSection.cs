using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Секция отчёта
/// </summary>
public sealed class ReportSection
{
    /// <summary>Название секции</summary>
    public string Title { get; init; } = string.Empty;
    
    /// <summary>Описание секции</summary>
    public string? Description { get; init; }
    
    /// <summary>Тип содержимого</summary>
    public SectionContentType ContentType { get; init; }
    
    /// <summary>Показывать заголовок?</summary>
    public bool ShowTitle { get; init; } = true;
    
    /// <summary>Порядок секции</summary>
    public int Order { get; init; }
}