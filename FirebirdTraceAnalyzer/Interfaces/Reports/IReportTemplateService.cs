using FirebirdTraceAnalyzer.Models.Reports;

namespace FirebirdTraceAnalyzer.Interfaces.Reports;

/// <summary>
/// Interface for managing report templates, including built-in and custom templates.
/// Provides methods for retrieving, saving, deleting, exporting, and importing report templates.
/// </summary>
public interface IReportTemplateService
{
    /// <summary>
    /// Get all report templates (both built-in and custom).
    /// </summary>
    /// <returns>List of all report templates.</returns>
    Task<IReadOnlyList<ReportTemplate>> GetAllTemplatesAsync();
    
    /// <summary>
    /// Get built-in report templates.
    /// </summary>
    /// <returns>List of built-in report templates.</returns>
    IReadOnlyList<ReportTemplate> GetBuiltInTemplates();
    
    /// <summary>
    /// Get custom report templates (user-defined).
    /// </summary>
    /// <returns>List of custom report templates.</returns>
    Task<IReadOnlyList<ReportTemplate>> GetCustomTemplatesAsync();
    
    /// <summary>
    /// Get a report template by its ID (either built-in or custom).
    /// </summary>
    /// <param name="templateId">The ID of the template to retrieve.</param>
    /// <returns>The report template, or null if not found.</returns>
    Task<ReportTemplate?> GetTemplateByIdAsync(string templateId);
    
    /// <summary>
    /// Save a custom report template. If a template with the same ID already exists, it will be overwritten.
    /// </summary>
    /// <param name="template">The report template to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveTemplateAsync(ReportTemplate template);
    
    /// <summary>
    /// Delete a report template by its ID.
    /// </summary>
    /// <param name="templateId">The ID of the template to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteTemplateAsync(string templateId);

    /// <summary>
    /// Get the file path of a custom report template by its ID. 
    /// </summary>
    /// <param name="templateId">The ID of the template to retrieve the path for.</param>
    /// <returns>Returns null if the template is not found or is a built-in template.</returns>
    Task<string?> GetCustomTemplatePathAsync(string templateId);
    
    /// <summary>
    /// Export a report template to a file.
    /// </summary>
    /// <param name="template">The report template to export.</param>
    /// <param name="filePath">The path to the file where the template will be saved.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportTemplateAsync(ReportTemplate template, string filePath);
    
    /// <summary>
    /// Import a report template from a file. If a template with the same ID already exists, it will be overwritten.
    /// </summary>
    /// <param name="filePath">The path to the file from which to import the template.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<ReportTemplate> ImportTemplateAsync(string filePath);
}