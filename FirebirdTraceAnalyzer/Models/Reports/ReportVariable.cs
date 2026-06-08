using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Переменная для использования в отчёте
/// </summary>
public sealed class ReportVariable
{
    /// <summary>Тип переменной</summary>
    public ReportVariableType Type { get; init; }
    
    /// <summary>Название переменной (отображаемое)</summary>
    public string DisplayName { get; init; } = string.Empty;
    
    /// <summary>Ключ для шаблонизации (например, {FILE_NAMES})</summary>
    public string TemplateKey { get; init; } = string.Empty;
    
    /// <summary>Форматирование значения</summary>
    public string? Format { get; init; }
    
    /// <summary>Показывать в отчёте?</summary>
    public bool IsVisible { get; init; } = true;
    
    /// <summary>Порядок отображения</summary>
    public int DisplayOrder { get; init; }
}