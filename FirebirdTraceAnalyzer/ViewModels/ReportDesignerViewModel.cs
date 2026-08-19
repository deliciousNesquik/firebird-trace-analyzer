

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceAnalyzer.Interfaces.Filtering;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Sorting;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel для дизайнера отчётов
/// </summary>
public partial class ReportDesignerViewModel : ViewModelBase, IDialogViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IReportTemplateService _templateService;
    private readonly IReportGenerationService _generationService;
    private readonly IFilteringService _filteringService;
    private readonly ISortingService _sortingService;
    private readonly IEventPropertyAccessor _propertyAccessor;
    private readonly IFieldDiscoveryService _fieldDiscovery;

    private ReportDesignSessionContext? _sessionContext;

    // Живое превью (левая панель редактора). Рендерит ту же проекцию, что и экспорт (WYSIWYG).
    public ReportPreviewViewModel Preview { get; }

    // Debounce перерисовки превью: любое изменение настроек «пачкает» превью, а фактический
    // пересчёт откладывается на PreviewDebounceMs, чтобы серии правок (набор текста, ползунки)
    // не пересчитывали отчёт на каждый чих.
    private const int PreviewDebounceMs = 300;
    private CancellationTokenSource? _previewDebounceCts;

    #region Observable Properties - Template Info

    [ObservableProperty]
    private string _templateName = "New Report";

    [ObservableProperty]
    private string _templateDescription = string.Empty;


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

    /// <summary>Палитра доступных полей событий — источник для добавления колонок кнопкой «+».</summary>
    public ObservableCollection<FieldPaletteItem> AvailableFields { get; } = new();

    /// <summary>
    ///     Колонки отчёта. В отличие от палитры, здесь дубли РАЗРЕШЕНЫ: одно поле можно добавить
    ///     несколько раз (напр. Execute Time × Sum/Avg/Min/Max), у каждой колонки — своя роль,
    ///     агрегат, имя, формат, ширина и порядок.
    /// </summary>
    public ObservableCollection<EventFieldItem> ReportColumns { get; } = new();

    [ObservableProperty]
    private bool _showSummary = true;

    #endregion

    #region Observable Properties - Filters & Sort

    /// <summary>
    ///     Панель фильтров — тот же VM, что и на главной форме (категории, выбор значений,
    ///     диапазоны). Заполняется независимыми копиями дескрипторов в <see cref="LoadAvailableFilters"/>.
    /// </summary>
    public FiltersPanelViewModel FiltersPanel { get; }

    public ObservableCollection<SortOptionItem> AvailableSorts { get; } = new();

    [ObservableProperty]
    private SortOptionItem? _selectedSort;

    [ObservableProperty]
    private bool _sortDescending = true;

    /// <summary>
    ///     Видимые колонки, доступные как цель сортировки для сгруппированного отчёта
    ///     (ключи группировки и агрегаты). Синхронизируется с AvailableFields.
    /// </summary>
    public ObservableCollection<EventFieldItem> SortableColumns { get; } = new();

    /// <summary>Колонка, по которой сортируется сгруппированный результат (по её DisplayName).</summary>
    [ObservableProperty]
    private EventFieldItem? _selectedSortColumn;

    /// <summary>Активна ли группировка (есть хотя бы одна видимая колонка-ключ группировки).</summary>
    public bool IsGrouped => ReportColumns.Any(c => c.Kind == ColumnKind.GroupKey);

    /// <summary>
    /// Есть агрегатные колонки, но НЕТ ключа группировки → агрегаты не вычислятся (агрегация работает
    /// только с GROUP BY). Управляет видимостью подсказки в дизайнере.
    /// </summary>
    public bool ShowAggregationHint =>
        !IsGrouped && ReportColumns.Any(c => c.Kind == ColumnKind.Aggregate);

    [ObservableProperty]
    private int? _eventLimit;

    #endregion

    #region Observable Properties - Export

    public ObservableCollection<ReportFormatItem> SupportedFormats { get; } = new();

    [ObservableProperty]
    private ReportFormat _defaultFormat = ReportFormat.Pdf;

    #endregion

    #region Observable Properties - State

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = Loc.Tr("Status.ReportDesigner.Ready");

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    #endregion

    /// <summary>Событие успешного сохранения шаблона</summary>
    public event EventHandler<ReportTemplate>? TemplateSaved;

    /// <summary>Диалог просит закрыться (результат — сохранённый шаблон или null при отмене).</summary>
    public event EventHandler<object?>? CloseRequested;

    /// <summary>Закрыть редактор без сохранения.</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, null);

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
        IFieldDiscoveryService fieldDiscovery,
        ReportPreviewViewModel preview)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        _filteringService = filteringService ?? throw new ArgumentNullException(nameof(filteringService));
        _sortingService = sortingService ?? throw new ArgumentNullException(nameof(sortingService));
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
        _fieldDiscovery = fieldDiscovery ?? throw new ArgumentNullException(nameof(fieldDiscovery));
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));

        FiltersPanel = CreateFiltersPanel();

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
        Preview = new ReportPreviewViewModel();

        FiltersPanel = CreateFiltersPanel();

        InitializeAvailableOptions();
    }

    /// <summary>
    ///     Создаёт панель фильтров и помечает шаблон изменённым при любой правке фильтров
    ///     (включение/выключение, выбор значений, диапазоны).
    /// </summary>
    private FiltersPanelViewModel CreateFiltersPanel()
    {
        var panel = new FiltersPanelViewModel(ApplyDesignerFilters, _propertyAccessor);

        panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FiltersPanelViewModel.HasUnappliedChanges) &&
                panel.HasUnappliedChanges)
                HasUnsavedChanges = true;
        };

        return panel;
    }

    /// <summary>
    ///     Реакция на кнопку «Apply» в панели фильтров. НЕ открывает предпросмотр: применяет
    ///     активные фильтры к событиям сессии (предикатами, как на главной форме) и пересобирает
    ///     доступные сортировки и счётчики значений под отфильтрованный набор типов событий —
    ///     чтобы пользователь мог настроить фильтры и сортировку целиком внутри дизайнера.
    /// </summary>
    private void ApplyDesignerFilters()
    {
        HasUnsavedChanges = true;

        if (_sessionContext == null || _sessionContext.SourceEvents.Count == 0)
            return;

        var filtered = _filteringService
            .ApplyFilters(_sessionContext.SourceEvents, FiltersPanel.AvailableFilters)
            .ToList();

        // Пересобираем палитру полей под отфильтрованные типы: при фильтре по типу события
        // пересечение схлопывается и появляются его специфичные поля (Sql, Plan и т.п.).
        LoadAvailableFields(filtered);

        // Пересобираем сортировки под отфильтрованные типы событий (с сохранением выбора).
        LoadAvailableSorts(filtered);

        // Пересобираем список фильтров под отфильтрованные типы событий — например, при фильтре
        // по типу "statement start" появляются поля, специфичные для этого события.
        // FiltersPanel.LoadFilters сохраняет активность и выбранные значения по Id фильтра.
        LoadAvailableFilters(filtered);

        // Обновляем счётчики значений фильтров под отфильтрованный набор.
        FiltersPanel.UpdateFilterCounts(filtered);

        StatusMessage = string.Format(Loc.Tr("Status.ReportDesigner.FiltersApplied"), filtered.Count, _sessionContext.SourceEvents.Count);

        // Фильтры изменили выборку — перерисовываем превью.
        MarkPreviewDirty();
    }

    /// <summary>
    /// Инициализирует доступные опции (переменные, поля, форматы)
    /// </summary>
    private void InitializeAvailableOptions()
    {
        // Переменные заголовка. Агрегаты времени исполнения (среднее/макс/мин) убраны из шапки —
        // им место в колонках-агрегатах таблицы, а не в оглавлении отчёта.
        var excludedVariables = new HashSet<ReportVariableType>
        {
            ReportVariableType.AverageExecutionTime,
            ReportVariableType.MaxExecutionTime,
            ReportVariableType.MinExecutionTime
        };

        foreach (ReportVariableType varType in Enum.GetValues(typeof(ReportVariableType)))
        {
            if (excludedVariables.Contains(varType))
                continue;

            var variable = new ReportVariableItem
            {
                Type = varType,
                DisplayName = GetVariableDisplayName(varType),
                IsVisible = false,
                DisplayOrder = (int)varType
            };

            // Видимость/порядок переменной шапки отражаются в превью.
            variable.PropertyChanged += (_, _) =>
            {
                HasUnsavedChanges = true;
                MarkPreviewDirty();
            };

            AvailableVariables.Add(variable);
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

        // Палитра: ПЕРЕСЕЧЕНИЕ полей всех присутствующих типов событий. Без фильтрации (смешанные
        // типы) остаются только общие поля — специфичные поля одного типа пусты в остальных, поэтому
        // их не предлагаем. После фильтрации по типу пересечение схлопывается до полей этого типа,
        // и специфичные поля появляются (см. ApplyDesignerFilters, где палитра пересобирается).
        foreach (var field in _fieldDiscovery.GetCommonFields(eventList))
        {
            AvailableFields.Add(new FieldPaletteItem
            {
                PropertyPath = field.PropertyPath,
                DisplayName = field.DisplayName,
                Format = field.Format
            });
        }

        Logger.Info("Loaded {Count} available fields for reporting", AvailableFields.Count);
    }

    /// <summary>Добавляет колонку отчёта из поля палитры (можно несколько раз — дубли разрешены).</summary>
    [RelayCommand]
    private void AddColumn(FieldPaletteItem? field)
    {
        if (field is null)
            return;

        var column = new EventFieldItem
        {
            PropertyPath = field.PropertyPath,
            DisplayName = field.DisplayName,
            Order = ReportColumns.Count == 0 ? 1 : ReportColumns.Max(c => c.Order) + 1,
            Alignment = TextAlignment.Left,
            Format = field.Format,
            Kind = ColumnKind.Field
        };

        column.PropertyChanged += OnColumnChanged;
        ReportColumns.Add(column);

        RefreshSortableColumns();
        OnPropertyChanged(nameof(IsGrouped));
        OnPropertyChanged(nameof(ShowAggregationHint));
        HasUnsavedChanges = true;
        MarkPreviewDirty();
    }

    /// <summary>Удаляет колонку отчёта.</summary>
    [RelayCommand]
    private void RemoveColumn(EventFieldItem? column)
    {
        if (column is null)
            return;

        column.PropertyChanged -= OnColumnChanged;
        ReportColumns.Remove(column);

        RefreshSortableColumns();
        OnPropertyChanged(nameof(IsGrouped));
        OnPropertyChanged(nameof(ShowAggregationHint));
        HasUnsavedChanges = true;
        MarkPreviewDirty();
    }

    private void OnColumnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EventFieldItem.Kind)
            or nameof(EventFieldItem.DisplayName))
        {
            RefreshSortableColumns();
            OnPropertyChanged(nameof(IsGrouped));
            OnPropertyChanged(nameof(ShowAggregationHint));
        }

        // Любое изменение колонки влияет на таблицу отчёта — перерисовываем превью.
        HasUnsavedChanges = true;
        MarkPreviewDirty();
    }

    /// <summary>Пересобирает список колонок-целей сортировки из колонок отчёта, сохраняя выбор.</summary>
    private void RefreshSortableColumns()
    {
        // Восстанавливаем выбор по Order (уникален), а не по DisplayName — при дублирующихся именах
        // колонок иначе выбралась бы не та колонка.
        var previousOrder = SelectedSortColumn?.Order;

        SortableColumns.Clear();
        foreach (var column in ReportColumns.OrderBy(c => c.Order))
            SortableColumns.Add(column);

        SelectedSortColumn = previousOrder is { } order
            ? SortableColumns.FirstOrDefault(c => c.Order == order)
            : null;
    }

    /// <summary>
    /// Загружает доступные фильтры на основе текущих событий
    /// </summary>
    public void LoadAvailableFilters(IEnumerable<EventBase> sampleEvents)
    {
        var eventList = sampleEvents.ToList();
        if (eventList.Count == 0)
        {
            Logger.Warn("No events provided for filter extraction");
            FiltersPanel.LoadFilters([]);
            return;
        }

        // Берём те же фильтры, что и главная форма, но независимыми копиями (CreateConfigurableClone),
        // и отдаём их той же панели фильтров, что используется на главной форме.
        var filters = _filteringService.GetAvailableFilters(eventList)
            .Select(f => _filteringService.CreateConfigurableClone(f))
            .ToList();

        FiltersPanel.LoadFilters(filters);

        Logger.Info("Loaded {Count} available filters", FiltersPanel.AvailableFilters.Count);
    }

    /// <summary>
    /// Загружает доступные сортировки на основе текущих событий
    /// </summary>
    public void LoadAvailableSorts(IEnumerable<EventBase> sampleEvents)
    {
        // Запоминаем выбранную сортировку, чтобы восстановить её, если поле всё ещё доступно
        // после пересбора (например, при повторном применении фильтров).
        var previousPath = SelectedSort?.PropertyPath;

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

        // Восстанавливаем выбор, если поле всё ещё доступно (иначе сортировка сбрасывается).
        if (!string.IsNullOrWhiteSpace(previousPath))
            SelectedSort = AvailableSorts.FirstOrDefault(s => s.PropertyPath == previousPath);

        Logger.Info("Loaded {Count} available sorts", AvailableSorts.Count);
    }

    /// <summary>
    /// Загружает существующий шаблон для редактирования
    /// </summary>
    /// <summary>Исходная дата создания редактируемого шаблона — сохраняется при пересохранении.</summary>
    private DateTime? _editingCreatedAt;

    public async Task LoadTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusMessage = Loc.Tr("Status.ReportDesigner.LoadingTemplate");

            var template = await _templateService.GetTemplateByIdAsync(templateId);

            if (template == null)
            {
                StatusMessage = Loc.Tr("Status.ReportDesigner.TemplateNotFound");
                Logger.Error("Template not found: {TemplateId}", templateId);
                return;
            }

            // Заполняем поля из шаблона
            IsEditingExisting = true;
            EditingTemplateId = template.Id;
            _editingCreatedAt = template.CreatedAt;

            TemplateName = template.Name;
            TemplateDescription = template.Description;

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

            // Колонки отчёта: строим из полей шаблона напрямую (порядок — по Order). Дубли поддержаны.
            foreach (var column in ReportColumns)
                column.PropertyChanged -= OnColumnChanged;
            ReportColumns.Clear();

            foreach (var field in template.Body.VisibleFields.OrderBy(f => f.Order))
            {
                var column = new EventFieldItem
                {
                    PropertyPath = field.PropertyPath,
                    DisplayName = field.DisplayName,
                    Order = field.Order,
                    Format = field.Format,
                    Alignment = field.Alignment,
                    WidthPercent = field.WidthPercent,
                    Kind = field.Kind,
                    Aggregate = field.Aggregate ?? AggregateFunction.Count
                };

                column.PropertyChanged += OnColumnChanged;
                ReportColumns.Add(column);
            }

            RefreshSortableColumns();
            OnPropertyChanged(nameof(IsGrouped));
            OnPropertyChanged(nameof(ShowAggregationHint));

            // Восстанавливаем колонку сортировки сгруппированного результата.
            if (!string.IsNullOrWhiteSpace(template.Body.SortByColumn))
            {
                SelectedSortColumn = SortableColumns
                    .FirstOrDefault(c => string.Equals(c.DisplayName, template.Body.SortByColumn, StringComparison.Ordinal));
            }

            // Фильтры — восстанавливаем состояние на дескрипторах панели фильтров
            if (template.Filters != null)
            {
                foreach (var filterConfig in template.Filters)
                {
                    var filter = FiltersPanel.AvailableFilters.FirstOrDefault(f =>
                        (!string.IsNullOrWhiteSpace(filterConfig.PropertyPath) &&
                         string.Equals(f.PropertyPath, filterConfig.PropertyPath, StringComparison.Ordinal)) ||
                        string.Equals(f.Id, filterConfig.FilterId, StringComparison.OrdinalIgnoreCase));
                    if (filter == null)
                        continue;

                    filter.IsActive = filterConfig.IsActive;

                    if (filterConfig.SelectedValues is { Count: > 0 } || filterConfig.ExcludedValues is { Count: > 0 })
                    {
                        // Значения из шаблона после JSON — строки/JsonElement, поэтому сопоставляем
                        // по строковому представлению значения (enum → имя).
                        var selected = (filterConfig.SelectedValues ?? [])
                            .Select(v => v?.ToString())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToHashSet();

                        var excluded = (filterConfig.ExcludedValues ?? [])
                            .Select(v => v?.ToString())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToHashSet();

                        foreach (var value in filter.AvailableValues)
                        {
                            var text = value.Value?.ToString();
                            if (text != null && selected.Contains(text))
                                value.IsSelected = true;
                            else if (text != null && excluded.Contains(text))
                                value.IsExcluded = true;
                        }
                    }

                    if (filterConfig.MinValue != null)
                        filter.CurrentMinValue = filterConfig.MinValue;

                    if (filterConfig.MaxValue != null)
                        filter.CurrentMaxValue = filterConfig.MaxValue;
                }
            }

            // Сортировка
            if (!string.IsNullOrWhiteSpace(template.SortByField))
            {
                SelectedSort = AvailableSorts.FirstOrDefault(s => s.PropertyPath == template.SortByField);
                SortDescending = template.SortDescending;
            }

            EventLimit = template.EventLimit;
            ShowSummary = template.Body.ShowSummary;
            DefaultFormat = template.DefaultFormat;

            // Форматы: сначала сбрасываем все, затем включаем только объявленные в шаблоне — иначе
            // прежде включённые форматы оставались бы активными, и список только «расширялся».
            foreach (var f in SupportedFormats)
                f.IsSupported = false;

            foreach (var format in template.SupportedFormats)
            {
                var item = SupportedFormats.FirstOrDefault(f => f.Format == format);
                if (item != null)
                {
                    item.IsSupported = true;
                }
            }

            HasUnsavedChanges = false;
            StatusMessage = string.Format(Loc.Tr("Status.ReportDesigner.TemplateLoaded"), template.Name);
            Logger.Info("Template loaded for editing: {Name}", template.Name);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading template");
            StatusMessage = string.Format(Loc.Tr("Status.ReportDesigner.LoadError"), ex.Message);
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
            StatusMessage = Loc.Tr("Status.ReportDesigner.SavingTemplate");

            // Валидация
            if (string.IsNullOrWhiteSpace(TemplateName))
            {
                StatusMessage = Loc.Tr("Status.ReportDesigner.NameRequired");
                return;
            }

            if (ReportColumns.Count == 0)
            {
                StatusMessage = Loc.Tr("Status.ReportDesigner.AddColumn");
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
                IsBuiltIn = false,
                // При редактировании сохраняем исходную дату создания, для нового — текущую.
                CreatedAt = IsEditingExisting && _editingCreatedAt is { } created ? created : DateTime.Now,
                ModifiedAt = DateTime.Now,
                Version = "1.0",

                Header = BuildHeader(),

                Body = BuildReportBodyFromCurrentSettings(),

                Footer = BuildProductionFooter(),

                Filters = BuildFilterConfigs(),

                // Для сгруппированного отчёта сортировка событий не применяется — сортируем результат по колонке.
                SortByField = IsGrouped ? null : SelectedSort?.PropertyPath,
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
            StatusMessage = string.Format(Loc.Tr("Status.ReportDesigner.TemplateSaved"), template.Name);
            Logger.Info("Template saved: {Name} ({Id})", template.Name, template.Id);

            // Уведомляем об успешном сохранении и закрываем диалог с результатом-шаблоном.
            TemplateSaved?.Invoke(this, template);
            CloseRequested?.Invoke(this, template);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error saving template");
            StatusMessage = string.Format(Loc.Tr("Status.ReportDesigner.SaveError"), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Помечает превью «грязным»: планирует отложенную (debounce) перерисовку. Вызывается из всех
    /// точек изменения настроек. Серия быстрых правок схлопывается в один пересчёт.
    /// </summary>
    public void MarkPreviewDirty()
    {
        // Отменяем и ОСВОБОЖДАЕМ прежний источник: без Dispose каждая правка в дизайнере копила
        // неосвобождённые CancellationTokenSource (утечка таймер-регистраций).
        var previous = _previewDebounceCts;
        _previewDebounceCts = new CancellationTokenSource();
        var token = _previewDebounceCts.Token;

        previous?.Cancel();
        previous?.Dispose();

        _ = DebouncedRefreshAsync(token);
    }

    private async Task DebouncedRefreshAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(PreviewDebounceMs, token);
            await RefreshPreviewAsync(token);
        }
        catch (OperationCanceledException)
        {
            // Отменено следующей правкой — это норма.
        }
    }

    /// <summary>
    /// Немедленная перерисовка живого превью из текущих настроек: тот же конвейер, что и экспорт
    /// (шаблон → PrepareEventsForReport → метаданные → проекция в ReportPreviewViewModel).
    /// </summary>
    [RelayCommand]
    private async Task RefreshPreviewAsync(CancellationToken cancellationToken)
    {
        if (_sessionContext == null || _sessionContext.SourceEvents.Count == 0)
        {
            StatusMessage = Loc.Tr("Status.ReportDesigner.LoadTraceFirst");
            return;
        }

        try
        {
            var template = CreateTemplateFromCurrentSettings();
            var source = _sessionContext.SourceEvents;

            // Фильтр + O(n log n) сортировка всех событий — CPU-работа. Уводим в фон, чтобы серия
            // правок в дизайнере не морозила UI-поток на трассах в сотни тысяч событий.
            // Продолжение (установка UI-bound свойств) возобновляется на UI-потоке.
            var preparedEvents = await Task.Run(
                () => _generationService.PrepareEventsForReport(source, template),
                cancellationToken);

            var metadata = CreateReportMetadata(preparedEvents);

            await Preview.InitializeAsync(template, metadata, cancellationToken);

            StatusMessage = preparedEvents.Count == 0
                ? Loc.Tr("Status.ReportDesigner.NoEventsMatch")
                : string.Format(Loc.Tr("Status.ReportDesigner.PreviewCount"), preparedEvents.Count, _sessionContext.SourceEvents.Count);
        }
        catch (OperationCanceledException)
        {
            // Заменено следующей правкой.
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to refresh preview");
            StatusMessage = Loc.Tr("Status.ReportDesigner.PreviewFailed");
        }
    }
    
  private ReportBody BuildReportBodyFromCurrentSettings()
    {
        var columns = ReportColumns
            .OrderBy(c => c.Order)
            .ToList();

        return new ReportBody
        {
            ShowSummary = ShowSummary,
            // Группируем по колонкам, помеченным как GroupKey (порядок — по Order).
            GroupByFields = columns
                .Where(f => f.Kind == ColumnKind.GroupKey)
                .Select(f => f.PropertyPath)
                .ToList(),
            // Сортировка сгруппированного результата по выбранной колонке (только при группировке).
            // Идентифицируем колонку по её Order (уникален), а не по DisplayName — иначе при дублирующихся
            // именах колонок (одно поле как Sum и как Avg) сортировка уходила бы в первую по имени.
            SortByColumn = IsGrouped
                ? SelectedSortColumn?.Order.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null,
            VisibleFields = columns
                .Select(f => new EventField
                {
                    Name = f.PropertyPath.Replace(".", "_"),
                    DisplayName = f.DisplayName,
                    PropertyPath = f.PropertyPath,
                    Kind = f.Kind,
                    Aggregate = f.Kind == ColumnKind.Aggregate ? f.Aggregate : null,
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
    /// <summary>Заголовок отчёта из текущих настроек — общий для сохранения и превью/экспорта.</summary>
    private ReportHeader BuildHeader() => new()
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
    };

    /// <summary>Продакшн-футер — общий для сохранения и превью/экспорта (превью WYSIWYG,
    /// без «Preview Mode», иначе экспорт из дизайнера попадал бы в отчёт с этим футером).</summary>
    private static ReportFooter BuildProductionFooter() => new()
    {
        Show = true,
        Text = "Generated by Flytic - Firebird Trace Analyzer",
        ShowPageNumbers = true
    };

    private ReportTemplate CreateTemplateFromCurrentSettings()
    {
        return new ReportTemplate
        {
            Name = TemplateName,
            Header = BuildHeader(),
            Body = BuildReportBodyFromCurrentSettings(),
            Footer = BuildProductionFooter(),
            Filters = BuildFilterConfigs(),
            // Для сгруппированного отчёта сортировка событий не применяется — сортируем результат по колонке.
            SortByField = IsGrouped ? null : SelectedSort?.PropertyPath,
            SortDescending = SortDescending,
            EventLimit = EventLimit,
            DefaultFormat = DefaultFormat
        };
    }

    /// <summary>
    ///     Собирает конфигурацию активных фильтров из панели фильтров для сохранения в шаблон.
    ///     Для enum/string берём выбранные значения, для диапазонов — текущие границы.
    /// </summary>
    private List<ReportFilterConfig> BuildFilterConfigs()
    {
        return FiltersPanel.AvailableFilters
            .Where(f => f.IsActive)
            .Select(f => new ReportFilterConfig
            {
                FilterId = f.Id,
                PropertyPath = f.PropertyPath,
                DisplayName = f.DisplayName,
                IsActive = true,
                // Сохраняем строковое представление (enum → имя), чтобы значение переживало
                // JSON-сериализацию и совпадало с value.ToString() при применении фильтра отчёта.
                SelectedValues = f.AvailableValues
                    .Where(v => v.IsSelected)
                    .Select(v => (object)(v.Value?.ToString() ?? string.Empty))
                    .ToList(),
                ExcludedValues = f.AvailableValues
                    .Where(v => v.IsExcluded)
                    .Select(v => (object)(v.Value?.ToString() ?? string.Empty))
                    .ToList(),
                MinValue = f.CurrentMinValue,
                MaxValue = f.CurrentMaxValue
            })
            .ToList();
    }
    
    private ReportMetadata CreateReportMetadata(IReadOnlyList<EventBase> preparedEvents)
    {
        var sortDescription = SelectedSort == null
            ? null
            : $"{SelectedSort.DisplayName} ({(SortDescending ? "DESC" : "ASC")})";

        return new ReportMetadata
        {
            GeneratedAt = DateTime.Now,
            ApplicationVersion = Core.AppVersion.Current,
            Events = preparedEvents,
            TotalEventsCount = _sessionContext?.TotalEventsCount ?? preparedEvents.Count,
            Files = _sessionContext?.Files ?? [],
            ActiveFilters = string.Join(", ", FiltersPanel.AvailableFilters.Where(f => f.IsActive).Select(f => f.DisplayName)),
            ActiveSort = sortDescription
        };
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

    // Правки, не влияющие на содержимое превью (имя/описание шаблона) — только помечаем изменения.
    partial void OnTemplateNameChanged(string value) => HasUnsavedChanges = true;
    partial void OnTemplateDescriptionChanged(string value) => HasUnsavedChanges = true;

    // Правки, влияющие на превью — помечаем изменения и перерисовываем.
    partial void OnReportTitleChanged(string value) => MarkChangedAndPreview();
    partial void OnReportSubtitleChanged(string value) => MarkChangedAndPreview();
    partial void OnShowLogoChanged(bool value) => MarkChangedAndPreview();
    partial void OnShowGeneratedDateChanged(bool value) => MarkChangedAndPreview();
    partial void OnShowSummaryChanged(bool value) => MarkChangedAndPreview();
    partial void OnSelectedSortChanged(SortOptionItem? value) => MarkChangedAndPreview();
    partial void OnSortDescendingChanged(bool value) => MarkChangedAndPreview();
    partial void OnSelectedSortColumnChanged(EventFieldItem? value) => MarkChangedAndPreview();
    partial void OnEventLimitChanged(int? value) => MarkChangedAndPreview();

    // Формат по умолчанию не меняет содержимое превью — только синхронизируем формат экспорта.
    partial void OnDefaultFormatChanged(ReportFormat value)
    {
        HasUnsavedChanges = true;
        Preview.SelectedFormat = value;
    }

    private void MarkChangedAndPreview()
    {
        HasUnsavedChanges = true;
        MarkPreviewDirty();
    }

    #endregion
}

#region Helper Classes

/// <summary>Поле из палитры доступных полей (источник для добавления колонки отчёта кнопкой «+»).</summary>
public sealed class FieldPaletteItem
{
    public required string PropertyPath { get; init; }
    public required string DisplayName { get; init; }
    public string? Format { get; init; }
}

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
    private static readonly ColumnKind[] KindValues = Enum.GetValues<ColumnKind>();
    private static readonly AggregateFunction[] AggregateValues = Enum.GetValues<AggregateFunction>();

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

    /// <summary>Роль колонки: обычное поле, ключ группировки или агрегат.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAggregate))]
    private ColumnKind _kind = ColumnKind.Field;

    /// <summary>Агрегатная функция (используется только при Kind == Aggregate).</summary>
    [ObservableProperty]
    private AggregateFunction _aggregate = AggregateFunction.Count;

    /// <summary>Показывать ли выбор функции (колонка-агрегат).</summary>
    public bool IsAggregate => Kind == ColumnKind.Aggregate;

    public IReadOnlyList<ColumnKind> KindOptions => KindValues;
    public IReadOnlyList<AggregateFunction> AggregateOptions => AggregateValues;
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