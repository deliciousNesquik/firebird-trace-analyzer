using FirebirdTraceAnalyzer.Models.Reports;

namespace FirebirdTraceAnalyzer.Services.Reports;

/// <summary>
/// Сервис управления шаблонами отчётов
/// </summary>
public interface IReportTemplateService
{
    /// <summary>Получить все шаблоны (встроенные + пользовательские)</summary>
    Task<IReadOnlyList<ReportTemplate>> GetAllTemplatesAsync();
    
    /// <summary>Получить встроенные шаблоны</summary>
    IReadOnlyList<ReportTemplate> GetBuiltInTemplates();
    
    /// <summary>Получить пользовательские шаблоны</summary>
    Task<IReadOnlyList<ReportTemplate>> GetCustomTemplatesAsync();
    
    /// <summary>Получить шаблон по ID</summary>
    Task<ReportTemplate?> GetTemplateByIdAsync(string templateId);
    
    /// <summary>Сохранить шаблон</summary>
    Task SaveTemplateAsync(ReportTemplate template);
    
    /// <summary>Удалить шаблон</summary>
    Task DeleteTemplateAsync(string templateId);
    
    /// <summary>Экспортировать шаблон в файл</summary>
    Task ExportTemplateAsync(ReportTemplate template, string filePath);
    
    /// <summary>Импортировать шаблон из файла</summary>
    Task<ReportTemplate> ImportTemplateAsync(string filePath);
}