using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Reports.Exporters;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Reports;

/// <summary>
///     Реализация сервиса генерации отчётов
/// </summary>
public class ReportGenerationService : IReportGenerationService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Dictionary<ReportFormat, IReportExporter> _exporters;
    private readonly IEventPropertyAccessor _propertyAccessor;
    private readonly string _reportsDirectory;

    public ReportGenerationService(
        PdfReportExporter pdfExporter,
        DocxReportExporter docxExporter,
        XlsxReportExporter xlsxExporter,
        CsvReportExporter csvExporter,
        IEventPropertyAccessor propertyAccessor)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
        _exporters = new Dictionary<ReportFormat, IReportExporter>
        {
            [ReportFormat.PDF] = pdfExporter,
            [ReportFormat.DOCX] = docxExporter,
            [ReportFormat.XLSX] = xlsxExporter,
            [ReportFormat.CSV] = csvExporter
        };

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _reportsDirectory = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "Reports", "History");

        if (!Directory.Exists(_reportsDirectory))
        {
            Directory.CreateDirectory(_reportsDirectory);
            Logger.Info("Created reports directory: {Path}", _reportsDirectory);
        }
    }

    public async Task<GeneratedReport> GenerateReportAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        ReportFormat format,
        string? outputPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Info("Generating report: {TemplateName} ({Format})", template.Name, format);

            // Получаем экспортер
            if (!_exporters.TryGetValue(format, out var exporter))
                throw new NotSupportedException($"Format not supported: {format}");

            // Создаём путь для сохранения, если не указан
            if (string.IsNullOrWhiteSpace(outputPath)) outputPath = GenerateOutputPath(template, format);

            // Генерируем отчёт
            await exporter.ExportAsync(template, metadata, outputPath, cancellationToken);

            // Получаем информацию о файле
            var fileInfo = new FileInfo(outputPath);

            var generatedReport = new GeneratedReport
            {
                Template = template,
                Metadata = metadata,
                Format = format,
                FilePath = outputPath,
                FileSize = fileInfo.Length,
                GeneratedAt = DateTime.Now
            };

            Logger.Info("Report generated successfully: {Path} ({Size} bytes)",
                outputPath, fileInfo.Length);

            return generatedReport;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error generating report: {TemplateName}", template.Name);
            throw;
        }
    }

    public IReadOnlyList<EventBase> PrepareEventsForReport(
        IEnumerable<EventBase> visibleEvents,
        ReportTemplate template,
        string? currentSortField,
        bool currentSortDescending)
    {
        var events = visibleEvents.ToList();

        Logger.Info("Preparing events for report: {TemplateName}", template.Name);
        Logger.Debug("Input events count: {Count}", events.Count);

        // ✅ ШАГ 1: Применяем фильтры из шаблона (если указаны)
        if (template.Filters != null && template.Filters.Count > 0)
        {
            events = ApplyTemplateFilters(events, template.Filters);

            Logger.Debug("After template filters: {Count} events", events.Count);
        }

        // ✅ ШАГ 2: Проверяем и применяем сортировку (если отличается от текущей)
        if (!string.IsNullOrWhiteSpace(template.SortByField))
        {
            var needsResorting = currentSortField != template.SortByField ||
                                 currentSortDescending != template.SortDescending;

            if (needsResorting)
            {
                events = SortEvents(events, template.SortByField, template.SortDescending);

                Logger.Debug("Applied sorting: {Field} ({Direction})",
                    template.SortByField,
                    template.SortDescending ? "DESC" : "ASC");
            }
            else
            {
                Logger.Debug("Sorting already matches template, skipping");
            }
        }

        // ✅ ШАГ 3: Применяем лимит (если указан)
        if (template.EventLimit.HasValue && template.EventLimit.Value > 0)
        {
            events = events.Take(template.EventLimit.Value).ToList();

            Logger.Debug("Applied limit: {Limit} events", template.EventLimit.Value);
        }

        Logger.Info("Events prepared: {Count} events ready for report", events.Count);

        return events;
    }

    /// <summary>
    ///     Применяет фильтры из шаблона к событиям
    /// </summary>
    private List<EventBase> ApplyTemplateFilters(List<EventBase> events, List<ReportFilterConfig> filters)
    {
        var activeFilters = filters.Where(f => f.IsActive).ToList();

        if (activeFilters.Count == 0)
            return events;

        Logger.Info("Applying {Count} template filter(s)", activeFilters.Count);

        var filteredEvents = events.Where(evt =>
        {
            // Событие должно пройти ВСЕ активные фильтры
            return activeFilters.All(filter => CheckFilter(evt, filter));
        }).ToList();

        return filteredEvents;
    }

    /// <summary>
    ///     Проверяет, проходит ли событие через фильтр
    /// </summary>
    private bool CheckFilter(EventBase evt, ReportFilterConfig filter)
    {
        // Для фильтров с выбранными значениями (Enum/String)
        if (filter.SelectedValues != null && filter.SelectedValues.Count > 0)
        {
            if (!TryResolveFilterPropertyPath(filter, out var propertyPath))
                return false;

            var value = _propertyAccessor.GetValue(evt, propertyPath);

            if (value == null)
                return false;

            return filter.SelectedValues.Contains(value);
        }

        // Для Range фильтров (Numeric/DateTime)
        if (filter.MinValue != null || filter.MaxValue != null)
        {
            if (!TryResolveFilterPropertyPath(filter, out var propertyPath))
                return false;

            var value = _propertyAccessor.GetValue(evt, propertyPath);

            if (value == null)
                return false;

            if (value is not IComparable comparable)
                return false;

            if (filter.MinValue != null && comparable.CompareTo(filter.MinValue) < 0)
                return false;

            if (filter.MaxValue != null && comparable.CompareTo(filter.MaxValue) > 0)
                return false;

            return true;
        }

        // Если фильтр не имеет условий, пропускаем событие
        return true;
    }

    private bool TryResolveFilterPropertyPath(ReportFilterConfig filter, out string propertyPath)
    {
        if (!string.IsNullOrWhiteSpace(filter.PropertyPath))
        {
            propertyPath = filter.PropertyPath.Trim();
            return true;
        }

        if (_propertyAccessor.TryResolveFilterId(filter.FilterId, out propertyPath))
            return true;

        Logger.Warn(
            "Cannot resolve property path for filter: FilterId={FilterId}, DisplayName={DisplayName}",
            filter.FilterId,
            filter.DisplayName);
        propertyPath = string.Empty;
        return false;
    }

    /// <summary>
    ///     Сортирует события по указанному полю
    /// </summary>
    private List<EventBase> SortEvents(List<EventBase> events, string fieldPath, bool descending)
    {
        var list = events.ToList();

        list.Sort((a, b) =>
        {
            var valueA = _propertyAccessor.GetValue(a, fieldPath);
            var valueB = _propertyAccessor.GetValue(b, fieldPath);
            var result = _propertyAccessor.Compare(valueA, valueB);
            return descending ? -result : result;
        });

        return list;
    }

    /// <summary>
    ///     Генерирует путь для сохранения отчёта
    /// </summary>
    private string GenerateOutputPath(ReportTemplate template, ReportFormat format)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var sanitizedName = SanitizeFileName(template.Name);
        var extension = GetFileExtension(format);

        var fileName = $"{timestamp}_{sanitizedName}{extension}";

        return Path.Combine(_reportsDirectory, fileName);
    }

    private static string GetFileExtension(ReportFormat format)
    {
        return format switch
        {
            ReportFormat.PDF => ".pdf",
            ReportFormat.DOCX => ".docx",
            ReportFormat.XLSX => ".xlsx",
            ReportFormat.CSV => ".csv",
            _ => ".txt"
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}