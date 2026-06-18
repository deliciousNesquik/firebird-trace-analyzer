using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports.Exporters;
using FirebirdTraceAnalyzer.Interfaces.Sorting;
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
    private readonly ISortingService _sortingService;
    private readonly ISettingsService _settingsService;

    public ReportGenerationService(
        PdfReportExporter pdfExporter,
        DocxReportExporter docxExporter,
        XlsxReportExporter xlsxExporter,
        CsvReportExporter csvExporter,
        IEventPropertyAccessor propertyAccessor,
        ISortingService sortingService,
        ISettingsService settingsService)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
        _sortingService = sortingService ?? throw new ArgumentNullException(nameof(sortingService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _exporters = new Dictionary<ReportFormat, IReportExporter>
        {
            [ReportFormat.PDF] = pdfExporter,
            [ReportFormat.DOCX] = docxExporter,
            [ReportFormat.XLSX] = xlsxExporter,
            [ReportFormat.CSV] = csvExporter
        };
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
        ReportTemplate template)
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

        // ✅ ШАГ 2: Применяем сортировку шаблона ТЕМ ЖЕ сервисом, что и главная форма.
        // Сортируем всегда, когда поле задано: раньше здесь была оптимизация "пропустить,
        // если совпадает с текущей сортировкой", но вызывающий код (предпросмотр дизайнера)
        // передавал в качестве текущей сортировку самого шаблона — из-за чего сортировка
        // никогда не применялась к кастомным отчётам.
        if (!string.IsNullOrWhiteSpace(template.SortByField))
        {
            // Идемпотентно регистрируем сортировки по полям для типов событий этого отчёта,
            // чтобы ApplySort нашёл дескриптор по полю (как это делает главная форма).
            _sortingService.GetAvailableSorts(events);

            var sortId = _propertyAccessor.ToSortId(template.SortByField);
            events = _sortingService
                .ApplySort(events, sortId, template.SortDescending)
                .ToList();

            Logger.Debug("Applied sorting via SortingService: {Field} ({Direction})",
                template.SortByField,
                template.SortDescending ? "DESC" : "ASC");
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
        // Для фильтров с выбранными значениями (Enum/String).
        // Значения из шаблона после JSON приходят как JsonElement/число/строка, поэтому
        // сравниваем по строковому представлению (enum → имя), а не по ссылке/типу.
        if (filter.SelectedValues != null && filter.SelectedValues.Count > 0)
        {
            if (!TryResolveFilterPropertyPath(filter, out var propertyPath))
                return false;

            var value = _propertyAccessor.GetValue(evt, propertyPath);

            if (value == null)
                return false;

            return filter.SelectedValues.Any(selected => SelectedValueMatches(selected, value));
        }

        // Для Range фильтров (Numeric/DateTime)
        if (filter.MinValue != null || filter.MaxValue != null)
        {
            if (!TryResolveFilterPropertyPath(filter, out var propertyPath))
                return false;

            var value = _propertyAccessor.GetValue(evt, propertyPath);

            if (value is not IComparable comparable)
                return false;

            if (CompareToBound(comparable, filter.MinValue) is < 0)
                return false;

            if (CompareToBound(comparable, filter.MaxValue) is > 0)
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
    ///     Сравнивает выбранное в шаблоне значение (после JSON это строка/число/JsonElement)
    ///     с фактическим значением события. Сравнение по строке: enum совпадает по имени,
    ///     строки — как есть. Запасной путь — для старых шаблонов, где enum был сохранён числом.
    /// </summary>
    private static bool SelectedValueMatches(object? selected, object actual)
    {
        var selectedText = selected?.ToString();
        if (string.IsNullOrEmpty(selectedText))
            return false;

        if (string.Equals(selectedText, actual.ToString(), StringComparison.Ordinal))
            return true;

        // legacy: enum сохранён числовым значением (например 6 вместо "StatementFinish")
        return actual is Enum enumValue
               && long.TryParse(selectedText, out var numeric)
               && Convert.ToInt64(enumValue) == numeric;
    }

    /// <summary>
    ///     Сравнивает фактическое значение с границей диапазона из шаблона, приводя границу
    ///     (возможно JsonElement после десериализации) к типу значения. Возвращает null,
    ///     если граница не задана или её не удалось привести (тогда она не ограничивает).
    /// </summary>
    private static int? CompareToBound(IComparable actual, object? bound)
    {
        var boundText = bound?.ToString();
        if (string.IsNullOrEmpty(boundText))
            return null;

        try
        {
            object converted = actual is DateTime
                ? DateTime.Parse(boundText, System.Globalization.CultureInfo.InvariantCulture)
                : Convert.ChangeType(boundText, actual.GetType(), System.Globalization.CultureInfo.InvariantCulture);

            return actual.CompareTo(converted);
        }
        catch
        {
            return null;
        }
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

        // Папка резолвится из настроек на момент генерации, чтобы смена пути в настройках
        // вступала в силу без перезапуска. Создаём, если её ещё нет.
        var reportsDirectory = _settingsService.GetReportsDirectory();
        if (!Directory.Exists(reportsDirectory))
        {
            Directory.CreateDirectory(reportsDirectory);
            Logger.Info("Created reports directory: {Path}", reportsDirectory);
        }

        return Path.Combine(reportsDirectory, fileName);
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