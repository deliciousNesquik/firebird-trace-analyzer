

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceAnalyzer.Services.Reports;
using FirebirdTraceAnalyzer.Services.Sorting;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.Events;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel для дизайнера отчётов
/// </summary>
public partial class ReportDesignerViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IReportTemplateService _templateService;
    private readonly IReportGenerationService _generationService;
    private readonly IFilteringService _filteringService;
    private readonly ISortingService _sortingService;
    private readonly IEventPropertyAccessor _propertyAccessor;
    private readonly IFieldDiscoveryService _fieldDiscovery;

    private ReportDesignSessionContext? _sessionContext;

    #region Observable Properties - Template Info

    [ObservableProperty]
    private string _templateName = "New Report";

    [ObservableProperty]
    private string _templateDescription = string.Empty;

    [ObservableProperty]
    private ReportCategory _category = ReportCategory.Custom;

    [ObservableProperty]
    private bool _isEditingExisting;

    [ObservableProperty]
    private string? _editingTemplateId;

    #endregion

    #region Observable Properties - Header

    [ObservableProperty]
    private string _reportTitle = "Analysis Report";

    [ObservableProperty]
    private string _reportSubtitle = string.Empty;

    [ObservableProperty]
    private bool _showLogo = true;

    [ObservableProperty]
    private bool _showGeneratedDate = true;

    public ObservableCollection<ReportVariableItem> AvailableVariables { get; } = new();

    #endregion

    #region Observable Properties - Body

    [ObservableProperty]
    private EventDisplayStyle _displayStyle = EventDisplayStyle.Table;

    public ObservableCollection<EventFieldItem> AvailableFields { get; } = new();

    [ObservableProperty]
    private bool _showSummary = true;

    #endregion

    #region Observable Properties - Filters & Sort

    public ObservableCollection<FilterConfigItem> AvailableFilters { get; } = new();

    public ObservableCollection<SortOptionItem> AvailableSorts { get; } = new();

    [ObservableProperty]
    private SortOptionItem? _selectedSort;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private int? _eventLimit = 5;

    #endregion

    #region Observable Properties - Export

    public ObservableCollection<ReportFormatItem> SupportedFormats { get; } = new();

    [ObservableProperty]
    private ReportFormat _defaultFormat = ReportFormat.PDF;

    #endregion

    #region Observable Properties - State

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    #endregion

    /// <summary>Событие успешного сохранения шаблона</summary>
    public event EventHandler<ReportTemplate>? TemplateSaved;

    /// <summary>
    /// Передаёт события и файлы из главного окна для превью и экспорта.
    /// </summary>
    public void SetSessionContext(ReportDesignSessionContext context)
    {
        _sessionContext = context ?? throw new ArgumentNullException(nameof(context));
        Logger.Info(
            "Report session context set: {EventCount} source event(s), {FileCount} file(s)",
            context.SourceEvents.Count,
            context.Files.Count);
    }

    public ReportDesignerViewModel(
        IReportTemplateService templateService,
        IReportGenerationService generationService,
        IFilteringService filteringService,
        ISortingService sortingService,
        IEventPropertyAccessor propertyAccessor,
        IFieldDiscoveryService fieldDiscovery)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        _filteringService = filteringService ?? throw new ArgumentNullException(nameof(filteringService));
        _sortingService = sortingService ?? throw new ArgumentNullException(nameof(sortingService));
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
        _fieldDiscovery = fieldDiscovery ?? throw new ArgumentNullException(nameof(fieldDiscovery));

        InitializeAvailableOptions();
    }

    public ReportDesignerViewModel()
    {
        _templateService = null!;
        _generationService = null!;
        _filteringService = null!;
        _sortingService = null!;
        _propertyAccessor = new EventPropertyAccessor();
        _fieldDiscovery = null!;

        InitializeAvailableOptions();
    }

    /// <summary>
    /// Инициализирует доступные опции (переменные, поля, форматы)
    /// </summary>
    private void InitializeAvailableOptions()
    {
        // Переменные заголовка
        foreach (ReportVariableType varType in Enum.GetValues(typeof(ReportVariableType)))
        {
            AvailableVariables.Add(new ReportVariableItem
            {
                Type = varType,
                DisplayName = GetVariableDisplayName(varType),
                IsVisible = false,
                DisplayOrder = (int)varType
            });
        }

        // Форматы экспорта
        foreach (ReportFormat format in Enum.GetValues(typeof(ReportFormat)))
        {
            SupportedFormats.Add(new ReportFormatItem
            {
                Format = format,
                IsSupported = true
            });
        }

        Logger.Info("Available options initialized");
    }

    /// <summary>
    /// Загружает доступные поля событий на основе текущих событий
    /// </summary>
    public void LoadAvailableFields(IEnumerable<EventBase> sampleEvents)
    {
        AvailableFields.Clear();

        var eventList = sampleEvents.ToList();
        if (eventList.Count == 0)
        {
            Logger.Warn("No events provided for field extraction");
            return;
        }

        // Получаем ВСЕ доступные поля (объединение всех типов)
        var allFields = _fieldDiscovery.GetAllAvailableFields(eventList);

        var order = 1;
        foreach (var field in allFields)
        {
            AvailableFields.Add(new EventFieldItem
            {
                PropertyPath = field.PropertyPath,
                DisplayName = field.DisplayName,
                IsVisible = false,
                Order = order++,
                Alignment = TextAlignment.Left,
                Format = field.Format
            });
        }

        Logger.Info("Loaded {Count} available fields for reporting", AvailableFields.Count);
    }

    /// <summary>
    /// Загружает доступные фильтры на основе текущих событий
    /// </summary>
    public void LoadAvailableFilters(IEnumerable<EventBase> sampleEvents)
    {
        AvailableFilters.Clear();

        var eventList = sampleEvents.ToList();
        if (eventList.Count == 0)
        {
            Logger.Warn("No events provided for filter extraction");
            return;
        }

        // Получаем доступные фильтры через сервис фильтрации
        var filters = _filteringService.GetAvailableFilters(eventList);

        foreach (var filter in filters)
        {
            AvailableFilters.Add(new FilterConfigItem
            {
                FilterId = filter.Id,
                PropertyPath = filter.PropertyPath,
                DisplayName = filter.DisplayName,
                FilterType = filter.FilterType,
                IsActive = false,
                SelectedValues = new ObservableCollection<object>(),
                MinValue = filter.MinValue,
                MaxValue = filter.MaxValue,
                AvailableValues = new ObservableCollection<object>(
                    filter.AvailableValues.Select(v => v.Value))
            });
        }

        Logger.Info("Loaded {Count} available filters", AvailableFilters.Count);
    }

    /// <summary>
    /// Загружает доступные сортировки на основе текущих событий
    /// </summary>
    public void LoadAvailableSorts(IEnumerable<EventBase> sampleEvents)
    {
        AvailableSorts.Clear();

        var eventList = sampleEvents.ToList();
        if (eventList.Count == 0)
        {
            Logger.Warn("No events provided for sort extraction");
            return;
        }

        // Получаем доступные сортировки
        var sorts = _sortingService.GetAvailableSorts(eventList);

        foreach (var sort in sorts.Where(s => s.Id.StartsWith("field_")))
        {
            var propertyPath = ExtractPropertyPath(sort.Id);

            AvailableSorts.Add(new SortOptionItem
            {
                SortId = sort.Id,
                DisplayName = sort.DisplayName,
                PropertyPath = propertyPath,
                Category = sort.Category
            });
        }

        Logger.Info("Loaded {Count} available sorts", AvailableSorts.Count);
    }

    /// <summary>
    /// Загружает существующий шаблон для редактирования
    /// </summary>
    public async Task LoadTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading template...";

            var template = await _templateService.GetTemplateByIdAsync(templateId);

            if (template == null)
            {
                StatusMessage = "Template not found";
                Logger.Error("Template not found: {TemplateId}", templateId);
                return;
            }

            // Заполняем поля из шаблона
            IsEditingExisting = true;
            EditingTemplateId = template.Id;

            TemplateName = template.Name;
            TemplateDescription = template.Description;
            Category = template.Category;

            ReportTitle = template.Header.Title;
            ReportSubtitle = template.Header.Subtitle ?? string.Empty;
            ShowLogo = template.Header.ShowLogo;
            ShowGeneratedDate = template.Header.ShowGeneratedDate;

            // Переменные
            foreach (var variable in template.Header.Variables)
            {
                var item = AvailableVariables.FirstOrDefault(v => v.Type == variable.Type);
                if (item != null)
                {
                    item.IsVisible = variable.IsVisible;
                    item.DisplayOrder = variable.DisplayOrder;
                }
            }

            // Поля событий
            foreach (var field in template.Body.VisibleFields)
            {
                var item = AvailableFields.FirstOrDefault(f => f.PropertyPath == field.PropertyPath);
                if (item != null)
                {
                    item.IsVisible = true;
                    item.Order = field.Order;
                    item.Format = field.Format;
                    item.Alignment = field.Alignment;
                    item.WidthPercent = field.WidthPercent;
                }
            }

            // Фильтры
            if (template.Filters != null)
            {
                foreach (var filterConfig in template.Filters)
                {
                    var item = AvailableFilters.FirstOrDefault(f =>
                        (!string.IsNullOrWhiteSpace(filterConfig.PropertyPath) &&
                         string.Equals(f.PropertyPath, filterConfig.PropertyPath, StringComparison.Ordinal)) ||
                        string.Equals(f.FilterId, filterConfig.FilterId, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        item.IsActive = filterConfig.IsActive;
                        
                        if (filterConfig.SelectedValues != null)
                        {
                            item.SelectedValues = new ObservableCollection<object>(filterConfig.SelectedValues);
                        }

                        item.MinValue = filterConfig.MinValue;
                        item.MaxValue = filterConfig.MaxValue;
                    }
                }
            }

            // Сортировка
            if (!string.IsNullOrWhiteSpace(template.SortByField))
            {
                SelectedSort = AvailableSorts.FirstOrDefault(s => s.PropertyPath == template.SortByField);
                SortDescending = template.SortDescending;
            }

            EventLimit = template.EventLimit;
            DisplayStyle = template.Body.DisplayStyle;
            ShowSummary = template.Body.ShowSummary;
            DefaultFormat = template.DefaultFormat;

            // Форматы
            foreach (var format in template.SupportedFormats)
            {
                var item = SupportedFormats.FirstOrDefault(f => f.Format == format);
                if (item != null)
                {
                    item.IsSupported = true;
                }
            }

            HasUnsavedChanges = false;
            StatusMessage = $"Template loaded: {template.Name}";
            Logger.Info("Template loaded for editing: {Name}", template.Name);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading template");
            StatusMessage = $"Error loading template: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Сохраняет шаблон
    /// </summary>
    [RelayCommand]
    private async Task SaveTemplateAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving template...";

            // Валидация
            if (string.IsNullOrWhiteSpace(TemplateName))
            {
                StatusMessage = "Template name is required";
                return;
            }

            if (!AvailableFields.Any(f => f.IsVisible))
            {
                StatusMessage = "At least one field must be visible";
                return;
            }

            // Создаём шаблон
            var template = new ReportTemplate
            {
                Id = IsEditingExisting && !string.IsNullOrWhiteSpace(EditingTemplateId) 
                    ? EditingTemplateId 
                    : Guid.NewGuid().ToString(),
                Name = TemplateName,
                Description = TemplateDescription,
                Category = Category,
                IsBuiltIn = false,
                CreatedAt = IsEditingExisting ? DateTime.Now : DateTime.Now,
                ModifiedAt = DateTime.Now,
                Version = "1.0",

                Header = new ReportHeader
                {
                    Title = ReportTitle,
                    Subtitle = string.IsNullOrWhiteSpace(ReportSubtitle) ? null : ReportSubtitle,
                    ShowLogo = ShowLogo,
                    ShowGeneratedDate = ShowGeneratedDate,
                    Variables = AvailableVariables
                        .Where(v => v.IsVisible)
                        .Select(v => new ReportVariable
                        {
                            Type = v.Type,
                            DisplayName = v.DisplayName,
                            TemplateKey = GetTemplateKey(v.Type),
                            IsVisible = true,
                            DisplayOrder = v.DisplayOrder
                        })
                        .ToList()
                },

                Body = BuildReportBodyFromCurrentSettings(),

                Footer = new ReportFooter
                {
                    Show = true,
                    Text = "Generated by Flytic - Firebird Trace Analyzer",
                    ShowPageNumbers = true
                },

                Filters = AvailableFilters
                    .Where(f => f.IsActive)
                    .Select(f => new ReportFilterConfig
                    {
                        FilterId = f.FilterId,
                        PropertyPath = f.PropertyPath,
                        DisplayName = f.DisplayName,
                        IsActive = true,
                        SelectedValues = f.SelectedValues?.ToList(),
                        MinValue = f.MinValue,
                        MaxValue = f.MaxValue
                    })
                    .ToList(),

                SortByField = SelectedSort?.PropertyPath,
                SortDescending = SortDescending,
                EventLimit = EventLimit,

                SupportedFormats = SupportedFormats
                    .Where(f => f.IsSupported)
                    .Select(f => f.Format)
                    .ToList(),
                DefaultFormat = DefaultFormat,

                Tags = new List<string>()
            };

            // Сохраняем
            await _templateService.SaveTemplateAsync(template);

            HasUnsavedChanges = false;
            StatusMessage = $"Template saved: {template.Name}";
            Logger.Info("Template saved: {Name} ({Id})", template.Name, template.Id);

            // Уведомляем об успешном сохранении
            TemplateSaved?.Invoke(this, template);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error saving template");
            StatusMessage = $"Error saving template: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Генерирует превью отчёта
    /// </summary>
    [RelayCommand]
    private async Task GeneratePreviewAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Preparing preview...";

            if (_sessionContext == null || _sessionContext.SourceEvents.Count == 0)
            {
                StatusMessage = "Load trace files in the main window before preview";
                Logger.Warn("Preview requested without session context or events");
                return;
            }

            var currentTemplate = CreateTemplateFromCurrentSettings();

            var preparedEvents = _generationService.PrepareEventsForReport(
                _sessionContext.SourceEvents,
                currentTemplate,
                currentTemplate.SortByField,
                currentTemplate.SortDescending);

            if (preparedEvents.Count == 0)
            {
                StatusMessage = "No events match the template filters";
                Logger.Warn("Preview: no events after PrepareEventsForReport");
                return;
            }

            var metadata = CreateReportMetadata(preparedEvents);

            var previewVm = App.Services?.GetRequiredService<ReportPreviewViewModel>()
                ?? throw new InvalidOperationException("Report preview service is not available");

            await previewVm.InitializeAsync(currentTemplate, metadata, cancellationToken);

            var previewWindow = new Views.ReportPreviewWindow(previewVm);
        
            // Получаем родительское окно для CenterOwner
            var visualRoot = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            StatusMessage = "Displaying preview";
            await previewWindow.ShowDialog(visualRoot!);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to generate preview");
            StatusMessage = "Preview failed";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
  private ReportBody BuildReportBodyFromCurrentSettings()
    {
        return new ReportBody
        {
            DisplayStyle = DisplayStyle,
            ShowSummary = ShowSummary,
            VisibleFields = AvailableFields
                .Where(f => f.IsVisible)
                .Select(f => new EventField
                {
                    Name = f.PropertyPath.Replace(".", "_"),
                    DisplayName = f.DisplayName,
                    PropertyPath = f.PropertyPath,
                    Format = f.Format,
                    WidthPercent = f.WidthPercent,
                    Order = f.Order,
                    Alignment = f.Alignment
                })
                .ToList(),
            Sections =
            [
                new ReportSection
                {
                    Title = "Events",
                    ContentType = SectionContentType.Events,
                    ShowTitle = true,
                    Order = 1
                },
                new ReportSection
                {
                    Title = "Summary Statistics",
                    ContentType = SectionContentType.Statistics,
                    ShowTitle = ShowSummary,
                    Order = 2
                }
            ]
        };
    }

    /// <summary>
    /// Создает временный объект шаблона на основе текущих настроек UI без сохранения в БД
    /// </summary>
    private ReportTemplate CreateTemplateFromCurrentSettings()
    {
        return new ReportTemplate
        {
            Name = TemplateName,
            Header = new ReportHeader
            {
                Title = ReportTitle,
                Subtitle = ReportSubtitle,
                ShowLogo = ShowLogo,
                ShowGeneratedDate = ShowGeneratedDate,
                Variables = AvailableVariables
                    .Where(v => v.IsVisible)
                    .Select(v => new ReportVariable 
                    { 
                        Type = v.Type, 
                        DisplayName = v.DisplayName, 
                        IsVisible = true, 
                        DisplayOrder = v.DisplayOrder 
                    }).ToList()
            },
            Body = BuildReportBodyFromCurrentSettings(),
            Footer = new ReportFooter
            {
                Show = true,
                Text = "Preview Mode",
                ShowPageNumbers = true
            },
            Filters = AvailableFilters
                .Where(f => f.IsActive)
                .Select(f => new ReportFilterConfig
                {
                    FilterId = f.FilterId,
                    PropertyPath = f.PropertyPath,
                    DisplayName = f.DisplayName,
                    IsActive = true,
                    SelectedValues = f.SelectedValues?.ToList(),
                    MinValue = f.MinValue,
                    MaxValue = f.MaxValue
                })
                .ToList(),
            SortByField = SelectedSort?.PropertyPath,
            SortDescending = SortDescending,
            EventLimit = EventLimit,
            DefaultFormat = DefaultFormat
        };
    }
    
    private ReportMetadata CreateReportMetadata(IReadOnlyList<EventBase> preparedEvents)
    {
        var sortDescription = SelectedSort == null
            ? null
            : $"{SelectedSort.DisplayName} ({(SortDescending ? "DESC" : "ASC")})";

        return new ReportMetadata
        {
            GeneratedAt = DateTime.Now,
            ApplicationVersion = GetApplicationVersion(),
            Events = preparedEvents,
            TotalEventsCount = _sessionContext?.TotalEventsCount ?? preparedEvents.Count,
            Files = _sessionContext?.Files ?? [],
            ActiveFilters = string.Join(", ", AvailableFilters.Where(f => f.IsActive).Select(f => f.DisplayName)),
            ActiveSort = sortDescription
        };
    }

    private static string GetApplicationVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }

    #region Helper Methods

    private string GetVariableDisplayName(ReportVariableType type)
    {
        return type switch
        {
            ReportVariableType.FileNames => "File Names",
            ReportVariableType.FilePaths => "File Paths",
            ReportVariableType.FileCount => "File Count",
            ReportVariableType.FileSizeTotal => "Total File Size",
            ReportVariableType.TotalEventsCount => "Total Events Count",
            ReportVariableType.FilteredEventsCount => "Filtered Events Count",
            ReportVariableType.VisibleEventsCount => "Visible Events Count",
            ReportVariableType.TraceStartTime => "Trace Start Time",
            ReportVariableType.TraceEndTime => "Trace End Time",
            ReportVariableType.TraceDuration => "Trace Duration",
            ReportVariableType.ActiveFilters => "Active Filters",
            ReportVariableType.ActiveSort => "Active Sort",
            ReportVariableType.AverageExecutionTime => "Average Execution Time",
            ReportVariableType.MaxExecutionTime => "Max Execution Time",
            ReportVariableType.MinExecutionTime => "Min Execution Time",
            ReportVariableType.GeneratedDate => "Generated Date",
            ReportVariableType.GeneratedBy => "Generated By",
            ReportVariableType.ApplicationVersion => "Application Version",
            _ => type.ToString()
        };
    }

    private string GetTemplateKey(ReportVariableType type)
    {
        return $"{{{type.ToString().ToUpper()}}}";
    }

    private string ExtractPropertyPath(string sortId)
    {
        if (_propertyAccessor.TryResolveSortId(sortId, out var propertyPath))
            return propertyPath;

        return sortId;
    }

    #endregion

    #region Property Changed Handlers

    partial void OnTemplateNameChanged(string value) => HasUnsavedChanges = true;
    partial void OnTemplateDescriptionChanged(string value) => HasUnsavedChanges = true;
    partial void OnReportTitleChanged(string value) => HasUnsavedChanges = true;
    partial void OnReportSubtitleChanged(string value) => HasUnsavedChanges = true;
    partial void OnDisplayStyleChanged(EventDisplayStyle value) => HasUnsavedChanges = true;
    partial void OnSelectedSortChanged(SortOptionItem? value) => HasUnsavedChanges = true;
    partial void OnSortDescendingChanged(bool value) => HasUnsavedChanges = true;
    partial void OnEventLimitChanged(int? value) => HasUnsavedChanges = true;

    #endregion
}

#region Helper Classes

public partial class ReportVariableItem : ObservableObject
{
    public ReportVariableType Type { get; init; }
    
    [ObservableProperty]
    private string _displayName = string.Empty;
    
    [ObservableProperty]
    private bool _isVisible;
    
    [ObservableProperty]
    private int _displayOrder;
}

public partial class EventFieldItem : ObservableObject
{
    [ObservableProperty]
    private string _propertyPath = string.Empty;
    
    [ObservableProperty]
    private string _displayName = string.Empty;
    
    [ObservableProperty]
    private bool _isVisible;
    
    [ObservableProperty]
    private int _order;
    
    [ObservableProperty]
    private string? _format;
    
    [ObservableProperty]
    private int? _widthPercent;
    
    [ObservableProperty]
    private TextAlignment _alignment;
}

public partial class FilterConfigItem : ObservableObject
{
    [ObservableProperty]
    private string _filterId = string.Empty;

    [ObservableProperty]
    private string _propertyPath = string.Empty;
    
    [ObservableProperty]
    private string _displayName = string.Empty;
    
    public FilterType FilterType { get; init; }
    
    [ObservableProperty]
    private bool _isActive;
    
    [ObservableProperty]
    private ObservableCollection<object>? _selectedValues;
    
    [ObservableProperty]
    private object? _minValue;
    
    [ObservableProperty]
    private object? _maxValue;
    
    public ObservableCollection<object> AvailableValues { get; init; } = new();
}

public partial class SortOptionItem : ObservableObject
{
    public string SortId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string PropertyPath { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
}

public partial class ReportFormatItem : ObservableObject
{
    public ReportFormat Format { get; init; }
    
    [ObservableProperty]
    private bool _isSupported;
}

#endregion