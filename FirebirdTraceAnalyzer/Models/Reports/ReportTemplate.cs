using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Шаблон отчёта
/// </summary>
public sealed class ReportTemplate
{
    /// <summary>Уникальный ID шаблона</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>Название шаблона</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Описание шаблона</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>Автор шаблона</summary>
    public string Author { get; init; } = Environment.UserName;
    
    /// <summary>Дата создания</summary>
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    
    /// <summary>Дата последнего изменения</summary>
    public DateTime ModifiedAt { get; init; } = DateTime.Now;
    
    /// <summary>Версия шаблона</summary>
    public string Version { get; init; } = "1.0";
    
    /// <summary>Категория (Quick/Custom)</summary>
    public ReportCategory Category { get; init; } = ReportCategory.Custom;
    
    /// <summary>Настройки заголовка отчёта</summary>
    public ReportHeader Header { get; init; } = new();
    
    /// <summary>Настройки тела отчёта</summary>
    public ReportBody Body { get; init; } = new();
    
    /// <summary>Настройки футера отчёта</summary>
    public ReportFooter Footer { get; init; } = new();
    
    /// <summary>Форматы экспорта (PDF, DOCX, XLSX, CSV)</summary>
    public List<ReportFormat> SupportedFormats { get; init; } = new();
    
    /// <summary>Формат по умолчанию</summary>
    public ReportFormat DefaultFormat { get; init; } = ReportFormat.PDF;
    
    /// <summary>Фильтры для применения к событиям (опционально)</summary>
    public List<ReportFilterConfig>? Filters { get; init; }
    
    /// <summary>Поле для сортировки</summary>
    public string? SortByField { get; init; }
    
    /// <summary>Сортировка по убыванию?</summary>
    public bool SortDescending { get; init; } = true;
    
    /// <summary>Лимит событий (для Top N отчётов)</summary>
    public int? EventLimit { get; init; }
    
    /// <summary>Теги для поиска</summary>
    public List<string> Tags { get; init; } = new();
    
    /// <summary>Это встроенный шаблон?</summary>
    public bool IsBuiltIn { get; init; }
}