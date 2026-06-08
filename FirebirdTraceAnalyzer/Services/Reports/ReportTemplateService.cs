using System.Text.Json;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.Reports.BuiltInTemplates;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Reports;

/// <summary>
/// Реализация сервиса управления шаблонами
/// </summary>
public class ReportTemplateService : IReportTemplateService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly string _templatesDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public ReportTemplateService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _templatesDirectory = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "Templates", "Custom");

        if (!Directory.Exists(_templatesDirectory))
        {
            Directory.CreateDirectory(_templatesDirectory);
            Logger.Info("Created templates directory: {Path}", _templatesDirectory);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<IReadOnlyList<ReportTemplate>> GetAllTemplatesAsync()
    {
        var builtIn = GetBuiltInTemplates();
        var custom = await GetCustomTemplatesAsync();

        return builtIn.Concat(custom).ToList();
    }

    public IReadOnlyList<ReportTemplate> GetBuiltInTemplates()
    {
        return BuiltInReportTemplates.GetAll();
    }

    public async Task<IReadOnlyList<ReportTemplate>> GetCustomTemplatesAsync()
    {
        var templates = new List<ReportTemplate>();

        try
        {
            var files = Directory.GetFiles(_templatesDirectory, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var template = JsonSerializer.Deserialize<ReportTemplate>(json, _jsonOptions);

                    if (template != null)
                    {
                        templates.Add(template);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error loading template from {File}", file);
                }
            }

            Logger.Info("Loaded {Count} custom templates", templates.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading custom templates");
        }

        return templates;
    }

    public async Task<ReportTemplate?> GetTemplateByIdAsync(string templateId)
    {
        // Сначала ищем среди встроенных
        var builtIn = GetBuiltInTemplates().FirstOrDefault(t => t.Id == templateId);
        if (builtIn != null)
            return builtIn;

        // Затем среди пользовательских
        var custom = await GetCustomTemplatesAsync();
        return custom.FirstOrDefault(t => t.Id == templateId);
    }

    public async Task SaveTemplateAsync(ReportTemplate template)
    {
        if (template.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot modify built-in templates");
        }

        try
        {
            var fileName = $"{SanitizeFileName(template.Name)}_{template.Id}.json";
            var filePath = Path.Combine(_templatesDirectory, fileName);

            var json = JsonSerializer.Serialize(template, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            Logger.Info("Template saved: {Name} ({Id})", template.Name, template.Id);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error saving template {Name}", template.Name);
            throw;
        }
    }

    public async Task DeleteTemplateAsync(string templateId)
    {
        var template = await GetTemplateByIdAsync(templateId);

        if (template == null)
        {
            throw new InvalidOperationException($"Template not found: {templateId}");
        }

        if (template.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot delete built-in templates");
        }

        try
        {
            var files = Directory.GetFiles(_templatesDirectory, $"*{templateId}.json");

            foreach (var file in files)
            {
                File.Delete(file);
                Logger.Info("Deleted template file: {File}", file);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting template {Id}", templateId);
            throw;
        }
    }

    public async Task ExportTemplateAsync(ReportTemplate template, string filePath)
    {
        try
        {
            var json = JsonSerializer.Serialize(template, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            Logger.Info("Template exported to: {Path}", filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting template to {Path}", filePath);
            throw;
        }
    }

    public async Task<ReportTemplate> ImportTemplateAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var template = JsonSerializer.Deserialize<ReportTemplate>(json, _jsonOptions);

            if (template == null)
            {
                throw new InvalidOperationException("Failed to deserialize template");
            }
            
            var importedTemplate = new ReportTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = template.Name,
                Description = template.Description,
                Author = Environment.UserName, // Новый автор - текущий пользователь
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now,
                Version = template.Version,
                Category = template.Category,
                IsBuiltIn = false, // Импортированный шаблон не может быть встроенным
            
                Header = template.Header,
                Body = template.Body,
                Footer = template.Footer,
            
                SupportedFormats = template.SupportedFormats,
                DefaultFormat = template.DefaultFormat,
            
                Filters = template.Filters,
                SortByField = template.SortByField,
                SortDescending = template.SortDescending,
                EventLimit = template.EventLimit,
            
                Tags = template.Tags
            };

            // Сохраняем импортированный шаблон
            await SaveTemplateAsync(importedTemplate);

            Logger.Info("Template imported: {Name}", importedTemplate.Name);

            return importedTemplate;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error importing template from {Path}", filePath);
            throw;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}