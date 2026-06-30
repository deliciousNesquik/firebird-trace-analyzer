using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports.Exporters;
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
    private readonly ISettingsService _settingsService;

    public ReportGenerationService(
        PdfReportExporter pdfExporter,
        DocxReportExporter docxExporter,
        XlsxReportExporter xlsxExporter,
        CsvReportExporter csvExporter,
        IEventPropertyAccessor propertyAccessor,
        ISettingsService settingsService)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
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
            events = SortBySchwartzian(events, template.SortByField, template.SortDescending);

            Logger.Debug("Applied sorting: {Field} ({Direction})",
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
    ///     Сортировка отчёта преобразованием Шварца: ключ сортировки извлекается один раз на
    ///     событие, далее сортируем по готовым ключам. Использует тот же <see cref="IEventPropertyAccessor.Compare"/>
    ///     и тот же нестабильный <see cref="List{T}.Sort"/>, что и поле-сортировка в SortingService,
    ///     поэтому итоговый порядок (включая равные элементы) идентичен, но обращений к свойству
    ///     на порядок меньше.
    /// </summary>
    private List<EventBase> SortBySchwartzian(List<EventBase> events, string propertyPath, bool descending)
    {
        var keyed = new List<(EventBase Event, object? Key)>(events.Count);

        foreach (var evt in events)
            keyed.Add((evt, _propertyAccessor.GetValue(evt, propertyPath)));

        keyed.Sort((x, y) =>
        {
            var result = _propertyAccessor.Compare(x.Key, y.Key);
            return descending ? -result : result;
        });

        var sorted = new List<EventBase>(keyed.Count);
        foreach (var item in keyed)
            sorted.Add(item.Event);

        return sorted;
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

        // Компилируем каждый фильтр ОДИН раз (резолв пути, построение множеств совпадений),
        // чтобы в горячем цикле по событиям не делать строковую работу на каждое событие.
        var predicates = activeFilters
            .Select(CompileTemplateFilter)
            .ToArray();

        var filteredEvents = events
            .Where(evt =>
            {
                foreach (var predicate in predicates)
                {
                    if (!predicate(evt))
                        return false;
                }

                return true;
            })
            .ToList();

        return filteredEvents;
    }

    /// <summary>
    ///     Компилирует один фильтр шаблона в предикат. Тяжёлая подготовка (резолв пути,
    ///     множества значений) выполняется здесь однократно; возвращаемый делегат в цикле
    ///     делает только дешёвую проверку. Нерезолвящийся активный фильтр исключает все
    ///     события (сохранение прежней семантики All()).
    /// </summary>
    private Func<EventBase, bool> CompileTemplateFilter(ReportFilterConfig filter)
    {
        // Фильтр по выбранным значениям (Enum/String).
        if (filter.SelectedValues is { Count: > 0 } selectedValues)
        {
            if (!TryResolveFilterPropertyPath(filter, out var propertyPath))
                return static _ => false;

            // Быстрый путь — сравнение по значению (boxed enum/строка/число) без аллокаций.
            var valueSet = new HashSet<object>(selectedValues.Where(v => v != null)!);

            // Запасной путь — по строковому представлению (значения из JSON: enum как имя/число).
            var textSet = new HashSet<string>(StringComparer.Ordinal);
            var numericSet = new HashSet<long>();
            foreach (var selected in selectedValues)
            {
                var text = selected?.ToString();
                if (string.IsNullOrEmpty(text))
                    continue;

                textSet.Add(text);
                if (long.TryParse(text, out var numeric))
                    numericSet.Add(numeric);
            }

            return evt =>
            {
                var value = _propertyAccessor.GetValue(evt, propertyPath);
                if (value is null)
                    return false;

                if (valueSet.Contains(value))
                    return true;

                var text = value.ToString();
                if (text != null && textSet.Contains(text))
                    return true;

                return value is Enum enumValue && numericSet.Contains(Convert.ToInt64(enumValue));
            };
        }

        // Range-фильтр (Numeric/DateTime).
        if (filter.MinValue != null || filter.MaxValue != null)
        {
            if (!TryResolveFilterPropertyPath(filter, out var propertyPath))
                return static _ => false;

            var min = filter.MinValue;
            var max = filter.MaxValue;

            return evt =>
            {
                var value = _propertyAccessor.GetValue(evt, propertyPath);
                if (value is not IComparable comparable)
                    return false;

                if (CompareToBound(comparable, min) is < 0)
                    return false;

                if (CompareToBound(comparable, max) is > 0)
                    return false;

                return true;
            };
        }

        // Фильтр без условий — пропускает всё.
        return static _ => true;
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