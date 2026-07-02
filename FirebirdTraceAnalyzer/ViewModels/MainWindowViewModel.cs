using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.Enums;
using FirebirdTraceAnalyzer.Interfaces.Remote;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.EventLinking;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceAnalyzer.Interfaces.Filtering;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Searching;
using FirebirdTraceAnalyzer.Interfaces.Sorting;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Mocks;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceAnalyzer.Services.Plugins;
using FirebirdTraceAnalyzer.Services.Reports;
using FirebirdTraceAnalyzer.Services.Searching;
using FirebirdTraceAnalyzer.Services.Sorting;
using FirebirdTraceAnalyzer.Views;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Parsing.Engine;
using FirebirdTraceParser.Parsing.Utils;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    #region Dependencies (Injected Services)

    private readonly ISettingsService _settingsService;
    private readonly AppSettings _appSettings;
    private readonly UiSectionSettings _uiSettings;
    private readonly IFileDialogService _fileDialogService;
    private readonly ITraceLogParser _parser;
    private readonly PluginManagerService _pluginManager;
    private readonly ISortingService _sortingService;
    private readonly IFilteringService _filteringService;
    private readonly ISearchService _searchService;
    private readonly IEventPropertyAccessor _propertyAccessor;
    private readonly IEventChainService _eventChainService;

    /// <summary>Сервис модальных диалогов внутри окна (биндится оверлеем DialogHost).</summary>
    public IDialogService Dialogs { get; }

    #endregion

    #region Collections

    /// <summary>Все события из всех файлов (source of truth)</summary>
    private List<EventBase> AllEvents { get; } = [];

    /// <summary>События после применения фильтров и сортировки</summary>
    public RangeObservableCollection<EventBase> VisibleEvents { get; } = [];

    /// <summary>Карточки загруженных файлов</summary>
    public ObservableCollection<FileCardViewModel> FileCards { get; } = [];

    /// <summary>Выделенные карточки загруженных файлов</summary>
    public ObservableCollection<FileCardViewModel> SelectedFileCards { get; } = [];

    /// <summary>Сортировки, сгруппированные по категориям</summary>
    public ObservableCollection<IGrouping<string, SortDescriptor>> AvailableSortsByCategory { get; } = [];
    
    /// <summary>Встроенные шаблоны отчетов</summary>
    public ObservableCollection<ReportTemplate> BuiltInReports { get; } = [];

    /// <summary>Пользовательские шаблоны отчетов</summary>
    public ObservableCollection<ReportTemplate> CustomReports { get; } = [];

    #endregion

    #region State Management

    // События по хешу файла (для быстрого удаления)
    private readonly Dictionary<string, List<EventBase>> _eventsByFileHash = [];

    // Токен отмены загрузки
    private CancellationTokenSource? _loadingCts;

    // ✅ Флаг пакетного обновления (для предотвращения множественных пересчётов)
    private bool _isBatchUpdate;

    // Флаг завершения первичной загрузки настроек: пока false, изменения видимости секций и
    // режима поиска не сохраняются (иначе LoadSettings перезаписывал бы файл при инициализации).
    private bool _settingsLoaded;

    #endregion

    #region Observable Properties - UI State

    [ObservableProperty] private bool _isTraceFilesSectionVisible;
    [ObservableProperty] private bool _isSearchSectionVisible;
    [ObservableProperty] private bool _isEventsSectionVisible;
    [ObservableProperty] private bool _isStatisticsSectionVisible;
    [ObservableProperty] private bool _isLogsSectionVisible;
    
    [ObservableProperty] private bool _isClassicSearch;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isFileLoading;
    [ObservableProperty] private double _loadProgress;

    #endregion

    #region Observable Properties - Sorting & Filtering & Search

    [ObservableProperty] private SortDescriptor? _selectedSort;
    [ObservableProperty] private bool _isSortDescending;

    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private bool _isSearchActive;

    /// <summary>ViewModel панели фильтров</summary>
    public FiltersPanelViewModel FiltersPanelViewModel { get; }

    /// <summary>ViewModel секции статистики</summary>
    public StatisticsInfoSectionViewModel StatisticInfoModels { get; }

    #endregion

    #region Constructors

    /// <summary>Design-time конструктор (для XAML превью)</summary>
    public MainWindowViewModel()
    {
        // Mock-данные для дизайнера
        _settingsService = null!;
        _appSettings = new AppSettingsMock();
        _uiSettings = new UiSectionSettingsMock();
        _parser = null!;
        _pluginManager = null!;
        _fileDialogService = null!;
        _sortingService = null!;
        _filteringService = null!;
        _searchService = null!;

        _sshConnectionService = null!;
        _remoteFileService = null!;
        _propertyAccessor = new EventPropertyAccessor();
        _eventChainService = null!;
        Dialogs = null!;

        // Инициализация ViewModels
        StatisticInfoModels = new StatisticsInfoSectionViewModel();
        FiltersPanelViewModel = new FiltersPanelViewModel(ApplyAllFilters, _propertyAccessor);

        StatisticInfoModels.UpdateStatistics([
            new StatisticInfoModel("Files:", FileCards.Count.ToString()),
            new StatisticInfoModel("All Events:", AllEvents.Count.ToString()),
            new StatisticInfoModel("Visible Events:", VisibleEvents.Count.ToString()),
            new StatisticInfoModel("Filtered Events:", AllEvents.Count.ToString())
        ]);

        LoadSettings();
        StatusMessage = "Ready to go (Design Time).";
        
        // Загрузка шаблонов отчетов
        _ = LoadReportTemplatesAsync();
    }

    /// <summary>Runtime конструктор (DI)</summary>
    public MainWindowViewModel(
        IFileDialogService fileDialogService,
        ITraceLogParser parser,
        ISortingService sortingService,
        IFilteringService filteringService,
        ISearchService searchService,
        ISettingsService settingsService,
        ISshConnectionService sshConnectionService,
        IRemoteFileService remoteFileService,
        IEventPropertyAccessor propertyAccessor,
        IEventChainService eventChainService,
        IDialogService dialogService,
        PluginManagerService pluginManager)
    {
        Logger.Info("Event(s) list(s) are clear");
        VisibleEvents.Clear();
        AllEvents.Clear();
        Logger.Debug($"VisibleEvents count: {VisibleEvents.Count}");
        Logger.Debug($"AllEvents count: {AllEvents.Count}");


        // Dependency Injection
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _pluginManager = pluginManager?? throw new ArgumentNullException(nameof(pluginManager));
        _sortingService = sortingService ?? throw new ArgumentNullException(nameof(sortingService));
        _filteringService = filteringService ?? throw new ArgumentNullException(nameof(filteringService));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _appSettings = _settingsService.App;
        _uiSettings = _settingsService.Ui;

        _sshConnectionService = sshConnectionService ?? throw new ArgumentNullException(nameof(sshConnectionService));
        _remoteFileService = remoteFileService ?? throw new ArgumentNullException(nameof(remoteFileService));
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
        _eventChainService = eventChainService ?? throw new ArgumentNullException(nameof(eventChainService));
        Dialogs = dialogService ?? throw new ArgumentNullException(nameof(dialogService));


        // Инициализация ViewModels
        StatisticInfoModels = new StatisticsInfoSectionViewModel();
        FiltersPanelViewModel = new FiltersPanelViewModel(ApplyAllFilters, _propertyAccessor);

        // Регистрация пользовательских сортировок
        RegisterCustomSorts();

        // Загрузка настроек
        LoadSettings();

        // С этого момента изменения секций/поиска можно сохранять
        _settingsLoaded = true;

        StatusMessage = "Ready to go!";
        Logger.Info("MainWindowViewModel initialized.");
        
        // Загрузка шаблонов отчетов
        _ = LoadReportTemplatesAsync();
    }

    #endregion

    #region Event Inspector

    /// <summary>
    ///     Открывает окно «Инспектор события»: выбранное событие и его цепочка жизненного цикла.
    ///     Цепочка строится из <see cref="AllEvents"/> (не зависит от текущих фильтров).
    /// </summary>
    [RelayCommand]
    private void OpenEventInspector(EventBase? evt)
    {
        if (evt is null)
            return;

        var chain = _eventChainService.BuildChain(evt, AllEvents);
        var viewModel = new EventInspectorViewModel(evt, chain);
        var window = new EventInspectorWindow(viewModel);

        var owner = App.Services?.GetRequiredService<IWindowProvider>().GetCurrent() as Window;
        if (owner is not null)
            window.Show(owner);
        else
            window.Show();
    }

    #endregion

    #region Initialization

    /// <summary>Загружает настройки из конфигурации</summary>
    private void LoadSettings()
    {
        // UI Visibility
        IsTraceFilesSectionVisible = _uiSettings.Files;
        IsSearchSectionVisible = _uiSettings.Search;
        IsEventsSectionVisible = _uiSettings.Events;
        IsStatisticsSectionVisible = _uiSettings.Statistics;
        IsLogsSectionVisible = _uiSettings.Logs;

        // Search Type
        IsClassicSearch = _appSettings.IsClassicSearch;

        Logger.Info("Application settings loaded.");
        StatusMessage = "Application settings loaded.";
    }

    /// <summary>
    ///     Переносит текущее состояние видимости секций в модель настроек и сохраняет её.
    ///     Срабатывает при любом изменении секций (через меню, горячие клавиши или сброс).
    /// </summary>
    private void PersistUiSettings()
    {
        if (!_settingsLoaded || _settingsService == null)
            return;

        _uiSettings.Files = IsTraceFilesSectionVisible;
        _uiSettings.Search = IsSearchSectionVisible;
        _uiSettings.Events = IsEventsSectionVisible;
        _uiSettings.Statistics = IsStatisticsSectionVisible;
        _uiSettings.Logs = IsLogsSectionVisible;

        _settingsService.Save();
    }

    /// <summary>Сохраняет основные настройки приложения (режим поиска и т.п.).</summary>
    private void PersistAppSettings()
    {
        if (!_settingsLoaded || _settingsService == null)
            return;

        _appSettings.IsClassicSearch = IsClassicSearch;

        _settingsService.Save();
    }

    partial void OnIsTraceFilesSectionVisibleChanged(bool value) => PersistUiSettings();
    partial void OnIsSearchSectionVisibleChanged(bool value) => PersistUiSettings();
    partial void OnIsEventsSectionVisibleChanged(bool value) => PersistUiSettings();
    partial void OnIsStatisticsSectionVisibleChanged(bool value) => PersistUiSettings();
    partial void OnIsLogsSectionVisibleChanged(bool value) => PersistUiSettings();

    partial void OnIsClassicSearchChanged(bool value) => PersistAppSettings();

    /// <summary>Регистрирует сортировки из загруженных плагинов</summary>
    private void RegisterCustomSorts()
    {
        // 1. Загружаем все плагины с диска
        _pluginManager.LoadAllPlugins();

        // 2. Получаем только те плагины, которые поддерживают сортировку
        var sortPlugins = _pluginManager.GetSortPlugins();

        int loadedSortsCount = 0;

        foreach (var plugin in sortPlugins)
        {
            foreach (var sortDescriptor in plugin.GetSorts())
            {
                _sortingService.RegisterCustomSort(sortDescriptor);
                loadedSortsCount++;
            }
            
            Logger.Info($"Loaded sorts from plugin: {plugin.Name} (v{plugin.Version})");
        }

        Logger.Info($"Total custom sorts registered from plugins: {loadedSortsCount}");
    }

    #endregion

    #region Sorting

    /// <summary>Обновляет список доступных сортировок</summary>
    private void UpdateAvailableSorts()
    {
        var previousSelectedId = SelectedSort?.Id;

        AvailableSortsByCategory.Clear();

        // Передаём ВИДИМЫЕ события (после фильтрации)
        var sorts = _sortingService.GetAvailableSorts(VisibleEvents);

        var grouped = sorts
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
            AvailableSortsByCategory.Add(group);

        // Восстанавливаем выбор
        SortDescriptor? toSelect = null;

        if (previousSelectedId != null)
            toSelect = sorts.FirstOrDefault(s => s.Id == previousSelectedId);

        toSelect ??= sorts.FirstOrDefault(s => s.IsDefault) ?? sorts.FirstOrDefault();

        if (toSelect != null)
        {
            toSelect.IsSelected = true;
            SelectedSort = toSelect;
        }

        Logger.Info("Available sorts updated: {Count}", sorts.Count);
    }

    /// <summary>Применяет текущую сортировку</summary>
    private void ApplyCurrentSort()
    {
        if (SelectedSort == null)
        {
            Logger.Warn("No sort selected, skipping sorting.");
            return;
        }

        var sorted = _sortingService.ApplySort(
            VisibleEvents,
            SelectedSort.Id,
            IsSortDescending);

        VisibleEvents.ReplaceRange(sorted);

        StatusMessage =
            $"Sorted by: {SelectedSort.DisplayName} ({(IsSortDescending ? "desc" : "asc")})";

        Logger.Info(
            "Applied sort: {SortName}, descending={Descending}",
            SelectedSort.DisplayName,
            IsSortDescending);
    }

    [RelayCommand]
    private void SelectSort(SortDescriptor? descriptor)
    {
        if (descriptor == null || descriptor == SelectedSort)
            return;

        if (SelectedSort != null)
            SelectedSort.IsSelected = false;

        SelectedSort = descriptor;
        descriptor.IsSelected = true;

        Logger.Info("Sort selected: {DisplayName}", descriptor.DisplayName);
    }

    partial void OnSelectedSortChanged(SortDescriptor? value)
    {
        if (value != null && !_isBatchUpdate)
            ApplyCurrentSort();
    }

    partial void OnIsSortDescendingChanged(bool value)
    {
        if (!_isBatchUpdate)
            ApplyCurrentSort();
    }

    #region Custom Sort Comparers

    private int CustomUserActivityComparer(EventBase a, EventBase b, bool descending)
    {
        var userA = GetUserFromEvent(a);
        var userB = GetUserFromEvent(b);

        if (userA == null && userB == null) return 0;
        if (userA == null) return 1;
        if (userB == null) return -1;

        var result = string.Compare(userA, userB, StringComparison.OrdinalIgnoreCase);

        if (result == 0)
            result = a.Timestamp.CompareTo(b.Timestamp);

        return descending ? -result : result;
    }

    

    private static string? GetUserFromEvent(EventBase evt)
    {
        return evt switch
        {
            AttachDatabaseEvent e => e.Attachment.User,
            DetachDatabaseEvent e => e.Attachment.User,
            StatementEventBase e => e.Attachment.User,
            ProcedureEventBase e => e.Attachment.User,
            TriggerEventBase e => e.Attachment.User,
            _ => null
        };
    }

    #endregion

    #endregion

    #region Filtering

    /// <summary>
    ///     Обновляет доступные фильтры на основе ТЕКУЩИХ (отфильтрованных) событий
    /// </summary>
    private void UpdateAvailableFilters()
    {
        // Передаём VisibleEvents вместо AllEvents!
        // Это позволяет показывать фильтры только для видимого типа события
        var filters = _filteringService.GetAvailableFilters(VisibleEvents);

        FiltersPanelViewModel.LoadFilters(filters);

        StatusMessage = $"Available filters: {filters.Count}";
        Logger.Info("Available filters updated: {Count}", filters.Count);
    }

    /// <summary>
    ///     Применяет все активные фильтры и обновляет UI
    /// </summary>
    private void ApplyAllFilters()
    {
        try
        {
            _isBatchUpdate = true;

            Logger.Info("Starting to use filters and search...");
            var sw = Stopwatch.StartNew();

            IEnumerable<EventBase> query = AllEvents;

            // СНАЧАЛА поиск (если активен)
            if (IsSearchActive && !string.IsNullOrWhiteSpace(SearchText))
            {
                var searchMode = IsClassicSearch ? SearchType.Classic : SearchType.Regex;
                query = _searchService.Search(query, SearchText, searchMode);

                var searchResults = query.ToList();
                Logger.Info("Search completed in {Elapsed}ms, found: {Count}",
                    sw.ElapsedMilliseconds, searchResults.Count);
                query = searchResults;
                sw.Restart();
            }

            // Применяем фильтры
            query = _filteringService.ApplyFilters(
                query,
                FiltersPanelViewModel.AvailableFilters);

            var filteredList = query.ToList();

            Logger.Info("Filtering completed in {Elapsed}ms, resulting in: {Count} events",
                sw.ElapsedMilliseconds, filteredList.Count);

            // Применяем сортировку (если есть)
            if (SelectedSort != null)
            {
                sw.Restart();
                filteredList = _sortingService.ApplySort(
                    filteredList,
                    SelectedSort.Id,
                    IsSortDescending).ToList();

                Logger.Info("Sorting completed in {Elapsed}ms", sw.ElapsedMilliseconds);
            }

            // Обновляем UI (одним батчем)
            sw.Restart();
            VisibleEvents.ReplaceRange(filteredList);
            Logger.Info("UI updated in {Elapsed}ms", sw.ElapsedMilliseconds);

            // СНАЧАЛА обновляем сортировки (для видимых типов)
            sw.Restart();
            UpdateAvailableSorts();
            Logger.Info("Sortings updated in {Elapsed}ms", sw.ElapsedMilliseconds);

            // ПОТОМ обновляем фильтры (для видимых типов)
            sw.Restart();
            UpdateAvailableFilters();
            Logger.Info("Filters updated in {Elapsed}ms", sw.ElapsedMilliseconds);

            // Обновляем счётчики фильтров
            sw.Restart();
            FiltersPanelViewModel.UpdateFilterCounts(filteredList);
            Logger.Info("Filter counters updated in {Elapsed}ms", sw.ElapsedMilliseconds);

            // Обновляем статистику
            UpdateStatistics();

            var statusParts = new List<string>();

            if (IsSearchActive)
                statusParts.Add($"Search: '{SearchText}'");

            statusParts.Add($"{filteredList.Count:N0} of {AllEvents.Count:N0}");

            StatusMessage = string.Join(" • ", statusParts);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error applying filters");
            StatusMessage = $"Filtering error: {ex.Message}";
        }
        finally
        {
            _isBatchUpdate = false;
        }
    }

    #endregion

    #region Report Generation
    
    /// <summary>
    ///     Создаёт метаданные для генерации отчёта
    /// </summary>
    /// <param name="preparedEvents">События, подготовленные для отчёта</param>
    /// <returns>Метаданные отчёта</returns>
    public ReportMetadata CreateReportMetadata(IReadOnlyList<EventBase> preparedEvents)
    {
        return new ReportMetadata
        {
            Events = preparedEvents,
            Files = FileCards.Select(c => c.FileInfo).ToList(),
            TotalEventsCount = AllEvents.Count,
            ActiveFilters = GetActiveFiltersDescription(),
            ActiveSort = GetActiveSortDescription(),
            GeneratedAt = DateTime.Now,
            ApplicationVersion = GetApplicationVersion()
        };
    }

    /// <summary>
    ///     Получает описание активных фильтров
    /// </summary>
    private string? GetActiveFiltersDescription()
    {
        var activeFilters = FiltersPanelViewModel.AvailableFilters
            .Where(f => f.IsActive)
            .Select(f => f.DisplayName)
            .ToList();

        if (activeFilters.Count == 0)
            return null;

        return string.Join(", ", activeFilters);
    }

    /// <summary>
    ///     Получает описание активной сортировки
    /// </summary>
    private string? GetActiveSortDescription()
    {
        if (SelectedSort == null)
            return null;

        var direction = IsSortDescending ? "DESC" : "ASC";
        return $"{SelectedSort.DisplayName} ({direction})";
    }

    /// <summary>
    ///     Получает версию приложения
    /// </summary>
    private static string GetApplicationVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }
    
    /// <summary>
    /// Загружает списки шаблонов отчетов из сервиса в UI
    /// </summary>
    private async Task LoadReportTemplatesAsync()
    {
        try
        {
            var templateService = App.Services?.GetService<IReportTemplateService>();
            if (templateService == null) return;

            // 1. Загрузка встроенных отчетов
            var builtIn = templateService.GetBuiltInTemplates();
            BuiltInReports.Clear();
            foreach (var template in builtIn)
            {
                BuiltInReports.Add(template);
            }

            // 2. Загрузка пользовательских отчетов
            await RefreshCustomReportsAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load report templates for the menu.");
        }
    }

    /// <summary>
    /// Обновляет только пользовательские отчеты (удобно вызывать после создания/импорта)
    /// </summary>
    private async Task RefreshCustomReportsAsync()
    {
        var templateService = App.Services?.GetService<IReportTemplateService>();
        if (templateService == null) return;

        var custom = await templateService.GetCustomTemplatesAsync();
        
        await Dispatcher.UIThread.InvokeAsync(() => 
        {
            CustomReports.Clear();
            foreach (var template in custom)
            {
                CustomReports.Add(template);
            }
        });
    }
    

    [RelayCommand]
    private async Task GenerateQuickReportAsync(string templateId, CancellationToken cancellationToken)
    {
        try
        {
            IsFileLoading = true;
            StatusMessage = "Generating report...";
            Logger.Info("Quick report requested: {TemplateId}", templateId);

            // Получаем сервисы
            var templateService = App.Services?.GetRequiredService<IReportTemplateService>();
            var generationService = App.Services?.GetRequiredService<IReportGenerationService>();

            if (templateService == null || generationService == null)
            {
                StatusMessage = "Report services not available";
                Logger.Error("Report services not registered in DI");
                return;
            }

            // Загружаем шаблон
            var template = await templateService.GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                StatusMessage = $"Template not found: {templateId}";
                Logger.Warn("Template not found: {TemplateId}", templateId);
                return;
            }

            // Подготавливаем события для отчёта (сортировку применит сам сервис по шаблону)
            var preparedEvents = generationService.PrepareEventsForReport(
                VisibleEvents,
                template);

            if (preparedEvents.Count == 0)
            {
                StatusMessage = "No events to include in report";
                Logger.Warn("No events match report criteria");
                return;
            }

            // Создаём метаданные
            var metadata = CreateReportMetadata(preparedEvents);

            // Генерируем отчёт
            var generatedReport = await generationService.GenerateReportAsync(
                template,
                metadata,
                template.DefaultFormat,
                null,
                cancellationToken);

            StatusMessage = $"Report generated: {generatedReport.FilePath}";
            Logger.Info("Report generated successfully: {Path}", generatedReport.FilePath);

            // Показываем уведомление с предложением открыть
            await ShowReportGeneratedNotificationAsync(generatedReport);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Report generation cancelled";
            Logger.Info("Report generation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Report generation error: {ex.Message}";
            Logger.Error(ex, "Error generating report");
        }
        finally
        {
            IsFileLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateReportTemplateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var designerViewModel = App.Services?.GetRequiredService<ReportDesignerViewModel>();

            if (designerViewModel == null)
            {
                StatusMessage = "Report services not available";
                return;
            }

            designerViewModel.SetSessionContext(new ReportDesignSessionContext
            {
                SourceEvents = VisibleEvents.ToList(),
                Files = FileCards.Select(c => c.FileInfo).ToList(),
                TotalEventsCount = AllEvents.Count
            });

            if (VisibleEvents.Count > 0)
            {
                designerViewModel.LoadAvailableFields(VisibleEvents);
                designerViewModel.LoadAvailableFilters(VisibleEvents);
                designerViewModel.LoadAvailableSorts(VisibleEvents);
            }

            // Открываем окно дизайнера
            var window = new ReportDesignerWindow(designerViewModel);
            var result = await window.ShowDialog<ReportTemplate?>(
                App.Services?.GetRequiredService<IWindowProvider>().GetCurrent() as Window);

            if (result != null)
            {
                StatusMessage = $"Template created: {result.Name}";
                Logger.Info("Report template created: {Name}", result.Name);
                
                await RefreshCustomReportsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating template");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task EditReportTemplateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var templateService = App.Services?.GetRequiredService<IReportTemplateService>();

            if (templateService == null)
            {
                StatusMessage = "Template service not available";
                return;
            }

            // Получаем список всех шаблонов
            var allTemplates = await templateService.GetAllTemplatesAsync();
            
            Logger.Info($"All template(s) count: {allTemplates.Count}");

            // TODO: Показать диалог выбора шаблона для редактирования
            // После выбора открыть ReportDesignerWindow с загруженным шаблоном

            StatusMessage = "Template editing coming soon...";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error editing template");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportReportTemplateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var templateService = App.Services?.GetRequiredService<IReportTemplateService>();

            if (templateService == null)
            {
                StatusMessage = "Template service not available";
                return;
            }

            // Открываем диалог выбора файла
            var topLevel = App.Services?.GetRequiredService<IWindowProvider>().GetCurrent();
            if (topLevel?.StorageProvider == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Import Report Template",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Report Template")
                        {
                            Patterns = new[] { "*.json" }
                        }
                    }
                });

            if (files.Count == 0)
                return;

            var filePath = files[0].Path.LocalPath;
            var importedTemplate = await templateService.ImportTemplateAsync(filePath);

            StatusMessage = $"Template imported: {importedTemplate.Name}";
            Logger.Info("Template imported: {Name}", importedTemplate.Name);
            
            await RefreshCustomReportsAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error importing template");
            StatusMessage = $"Import error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportReportTemplateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var templateService = App.Services?.GetRequiredService<IReportTemplateService>();

            if (templateService == null)
            {
                StatusMessage = "Template service not available";
                return;
            }

            // TODO: Показать диалог выбора шаблона для экспорта
            // После выбора открыть диалог сохранения файла

            StatusMessage = "Template export coming soon...";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting template");
            StatusMessage = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenRecentReportsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var historyViewModel = new ReportHistoryViewModel(_fileDialogService, _settingsService);
            await historyViewModel.LoadReportsCommand.ExecuteAsync(null);

            var window = new Window
            {
                Title = "Recent Reports",
                Width = 800,
                Height = 600,
                Content = new UserControls.ReportHistoryView { DataContext = historyViewModel },
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            await window.ShowDialog(
                App.Services?.GetRequiredService<IWindowProvider>().GetCurrent() as Window);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening recent reports");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task OpenReportDesignerAsync(CancellationToken cancellationToken)
    {
        // TODO: Открыть окно дизайнера отчётов
        StatusMessage = "Report designer coming soon...";
        Logger.Info("Report designer requested");
        return Task.CompletedTask;
    }

    private async Task ShowReportGeneratedNotificationAsync(GeneratedReport report)
    {
        // Здесь можно показать диалог с кнопками "Open" и "Open Folder"
        // Пока просто логируем
        Logger.Info("Report ready: {Path} ({Size} bytes)", report.FilePath, report.FileSize);

        // Можно автоматически открыть файл
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = report.FilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open report file");
        }
    }

    #endregion

    #region Search

    /// <summary>
    ///     Выполняет поиск (вызывается кнопкой)
    /// </summary>
    [RelayCommand]
    private void ExecuteSearch()
    {
        IsSearchActive = !string.IsNullOrWhiteSpace(SearchText);

        if (!IsSearchActive)
        {
            StatusMessage = "Search query is empty";
            Logger.Warn("Attempted search with empty query");
        }

        // Применяем фильтры + поиск
        ApplyAllFilters();
    }

    [RelayCommand]
    private void ChangeSearchType()
    {
        IsClassicSearch = !IsClassicSearch;
    }

    /// <summary>
    ///     Сбрасывает поиск
    /// </summary>
    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        IsSearchActive = false;
        ApplyAllFilters();

        StatusMessage = "Search reset";
        Logger.Info("Search reset");
    }

    #endregion

    #region File Operations

    private bool CanOpenFile()
    {
        return !IsFileLoading;
    }

    /// <summary>Открывает диалог выбора файлов</summary>
    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private async Task OpenLocalFileAsync(CancellationToken cancellationToken)
    {
        IsFileLoading = true;
        OpenLocalFileCommand.NotifyCanExecuteChanged();

        CancellationTokenSource? cts = null;

        try
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loadingCts = cts;

            var files = await _fileDialogService.PickTraceFilesAsync();

            if (files.Count == 0)
            {
                StatusMessage = "No files selected.";
                return;
            }

            await ProcessSelectedFilesAsync(files, cts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "File loading cancelled.";
            Logger.Info("File loading cancelled by user.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading files");
            StatusMessage = $"Loading error: {ex.Message}";
        }
        finally
        {
            if (cts != null)
            {
                _loadingCts = null;
                cts.Dispose();
            }

            IsFileLoading = false;
            LoadProgress = 0;
            OpenLocalFileCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanCancelLoading()
    {
        return IsFileLoading && _loadingCts != null;
    }

    /// <summary>Отменяет текущую загрузку</summary>
    [RelayCommand(CanExecute = nameof(CanCancelLoading))]
    private void CancelLoading()
    {
        _loadingCts?.Cancel();
        Logger.Info("Loading cancellation requested.");
    }

    /// <summary>Обрабатывает выбранные файлы</summary>
    private async Task ProcessSelectedFilesAsync(
        IReadOnlyList<IStorageFile> files,
        CancellationToken cancellationToken)
    {
        var addedCount = 0;
        var duplicateCount = 0;
        LoadProgress = 0;

        try
        {
            _isBatchUpdate = true;

            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var file = files[i];
                var path = file.Path.LocalPath;

                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    Logger.Warn("File not found: {Path}", path);
                    continue;
                }

                StatusMessage = $"Processing {i + 1}/{files.Count}: {Path.GetFileName(path)}";
                LoadProgress = (double)(i + 1) / files.Count * 100;

                var fileHash = await CalculateFileHashAsync(path, cancellationToken);

                if (IsDuplicate(fileHash))
                {
                    duplicateCount++;
                    Logger.Warn("Duplicate file skipped: {FilePath}", path);
                    continue;
                }

                var fileInfo = new FileInfo(path);
                var traceModel = await ParseFileAsync(fileInfo, fileHash, cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                    FileCards.Add(CreateFileCardViewModel(traceModel)));

                addedCount++;
            }
        }
        finally
        {
            _isBatchUpdate = false;
        }

        // После загрузки всех файлов — ОДНО обновление
        if (addedCount > 0) ApplyAllFilters(); // ← Применяет фильтры + обновляет сортировки + статистику

        StatusMessage = BuildFileAddingStatusMessage(addedCount, duplicateCount);
    }

    /// <summary>Парсит один файл</summary>
    private async Task<TraceFileInfoModel> ParseFileAsync(
        FileInfo fileInfo,
        string fileHash,
        CancellationToken cancellationToken)
    {
        StatusMessage = $"Parsing: {fileInfo.Name}";

        Logger.Info("Streaming parse started: {FileName}", fileInfo.Name);

        var events = new List<EventBase>(8192);

        var startTrace = DateTime.MinValue;
        var endTrace = DateTime.MinValue;

        await using var stream = new FileStream(
            fileInfo.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            true);

        await foreach (var evt in _parser.ParseStreamAsync(
                           stream,
                           cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (startTrace == DateTime.MinValue)
                startTrace = evt.Timestamp;

            endTrace = evt.Timestamp;

            events.Add(evt);
        }

        _eventsByFileHash[fileHash] = events;
        AllEvents.AddRange(events);
        
        Logger.Info(
            "Streaming parse completed: {FileName}, events: {Count}",
            fileInfo.Name,
            events.Count);

        return new TraceFileInfoModel(
            fileInfo.Name,
            fileInfo.FullName,
            fileInfo.Length,
            startTrace,
            endTrace,
            events.Count,
            fileHash);
    }

    private async Task<bool> OpenFileInStorageAsync(FileCardViewModel card)
    {
        return await _fileDialogService.RevealInFileManagerAsync(card.FileInfo.FilePath);
    }

    /// <summary>Удаляет файл и его события</summary>
    private Task RemoveTraceFileAsync(FileCardViewModel card)
    {
        try
        {
            _isBatchUpdate = true;

            RemoveFileEvents(card.FileInfo.FileHash);
            FileCards.Remove(card);

            StatusMessage = $"File removed: {card.FileInfo.FileName}";
            Logger.Info("File removed: {FileName}", card.FileInfo.FileName);
        }
        finally
        {
            _isBatchUpdate = false;
        }

        ApplyAllFilters();

        return Task.CompletedTask;
    }

    /// <summary>
    ///     ⚡ ОПТИМИЗИРОВАННОЕ удаление событий файла БЕЗ утечек памяти
    /// </summary>
    private void RemoveFileEvents(string fileHash)
    {
        if (!_eventsByFileHash.TryGetValue(fileHash, out var eventsToRemove))
            return;

        var sw = Stopwatch.StartNew();

        // ✅ Создаём HashSet для O(1) поиска
        var eventsSet = new HashSet<EventBase>(eventsToRemove);

        // ✅ ИСПРАВЛЕНИЕ УТЕЧКИ: Используем RemoveAll вместо создания нового списка
        // RemoveAll модифицирует существующий список, не создавая копию
        var removedCount = AllEvents.RemoveAll(e => eventsSet.Contains(e));

        Logger.Info(
            "Removed {Count} events from AllEvents in {Elapsed}ms (optimized, no memory leak)",
            removedCount,
            sw.ElapsedMilliseconds);

        // ✅ Очищаем словарь И список событий для GC
        _eventsByFileHash.Remove(fileHash);
        eventsToRemove.Clear(); // Освобождаем память
        eventsSet.Clear(); // Освобождаем HashSet

        Logger.Info("Total removal time: {Elapsed}ms", sw.ElapsedMilliseconds);

        // ✅ Принудительная сборка мусора для больших объёмов (опционально)
        if (removedCount > 50000)
        {
            GC.Collect(2, GCCollectionMode.Optimized, false);
            Logger.Info("GC forced for {Count} removed events", removedCount);
        }
    }

    /// <summary>
    ///     ⚡ ОПТИМИЗИРОВАННОЕ удаление НЕСКОЛЬКИХ файлов БЕЗ утечек памяти
    /// </summary>
    private void RemoveMultipleFileEvents(IEnumerable<string> fileHashes)
    {
        var hashList = fileHashes.ToList();

        if (hashList.Count == 0)
            return;

        var sw = Stopwatch.StartNew();

        // ✅ Собираем ВСЕ события для удаления в один HashSet
        var allEventsToRemove = new HashSet<EventBase>();

        foreach (var hash in hashList)
            if (_eventsByFileHash.TryGetValue(hash, out var events))
            {
                foreach (var evt in events)
                    allEventsToRemove.Add(evt);

                // ✅ Очищаем список перед удалением из словаря
                events.Clear();
                _eventsByFileHash.Remove(hash);
            }

        // ✅ ИСПРАВЛЕНИЕ УТЕЧКИ: Используем RemoveAll
        var removedCount = AllEvents.RemoveAll(e => allEventsToRemove.Contains(e));

        Logger.Info(
            "Removed {Count} events from {FileCount} files in {Elapsed}ms (batch optimized, no leak)",
            removedCount,
            hashList.Count,
            sw.ElapsedMilliseconds);

        // ✅ Очищаем HashSet
        allEventsToRemove.Clear();

        // ✅ Принудительная сборка мусора для больших объёмов
        if (removedCount > 50000)
        {
            GC.Collect(2, GCCollectionMode.Optimized, false);
            Logger.Info("GC forced for {Count} removed events (batch)", removedCount);
        }
    }

    private bool IsDuplicate(string fileHash)
    {
        return FileCards.Any(f =>
            string.Equals(f.FileInfo.FileHash, fileHash, StringComparison.OrdinalIgnoreCase));
    }

    private FileCardViewModel CreateFileCardViewModel(TraceFileInfoModel fileInfo)
    {
        return new FileCardViewModel(fileInfo, RemoveTraceFileAsync, OpenFileInStorageAsync);
    }

    #endregion

    #region Remote File Operation

    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private async Task OpenRemoteFileAsync(CancellationToken cancellationToken)
    {
        IsFileLoading = true;
        OpenRemoteFileCommand.NotifyCanExecuteChanged();
        OpenLocalFileCommand.NotifyCanExecuteChanged();

        CancellationTokenSource? cts = null;
        string? downloadDirectory = null;
        var deleteLocalFilesAfterProcessing = false;

        try
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loadingCts = cts;

            // Показываем диалог подключения как встроенный оверлей внутри главного окна
            var connectionViewModel = App.Services!.GetRequiredService<RemoteConnectionDialogViewModel>();

            var connectionResult = await Dialogs.ShowDialogAsync<bool>(connectionViewModel);

            if (!connectionResult)
            {
                StatusMessage = "Connection cancelled.";
                return;
            }

            var settings = _sshConnectionService.CurrentSettings;
            if (settings == null)
            {
                StatusMessage = "No connection settings available.";
                return;
            }

            StatusMessage = "Fetching file list from server...";
            Logger.Info("Fetching files from {Directory}", settings.RemoteDirectory);

            // Получаем список файлов
            var remoteFiles = await _remoteFileService.GetFilesAsync(
                settings.RemoteDirectory,
                cts.Token);

            if (remoteFiles.Count == 0)
            {
                StatusMessage = "No trace files found on server.";
                Logger.Warn("No files found in {Directory}", settings.RemoteDirectory);
                return;
            }

            // Папка для скачивания зависит от флага удаления локальных файлов:
            //  • удаляем после обработки → временный каталог (целиком удаляется в finally);
            //  • оставляем файлы → стабильная папка приложения, чтобы файлы не пропали.
            // Путь вычисляем ДО окна выбора, чтобы окно могло проверить свободное место,
            // и используем тот же путь для фактического скачивания.
            deleteLocalFilesAfterProcessing = settings.DeleteAfterProcessingOnLocaleMachine;

            downloadDirectory = deleteLocalFilesAfterProcessing
                ? Path.Combine(Path.GetTempPath(), "FirebirdTraceAnalyzer", Guid.NewGuid().ToString())
                : _settingsService.GetRemoteDownloadDirectory();

            // Показываем диалог выбора файлов как встроенный оверлей (передаём целевую папку для проверки места)
            var selectionViewModel = CreateFileSelectionViewModel(settings, remoteFiles, downloadDirectory);

            var selectedFiles = await Dialogs.ShowDialogAsync<IReadOnlyList<RemoteFileInfo>>(selectionViewModel);

            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                StatusMessage = "No files selected.";
                _sshConnectionService.Disconnect();
                return;
            }

            // Создаём каталог только после подтверждения выбора.
            Directory.CreateDirectory(downloadDirectory);

            // Загружаем файлы с прогрессом
            var downloadedPaths = await DownloadFilesWithProgressAsync(
                selectedFiles,
                downloadDirectory,
                settings.DeleteAfterProcessingFromServer,
                cts.Token);

            // Обрабатываем загруженные файлы
            await ProcessDownloadedFilesAsync(
                downloadedPaths,
                deleteLocalFilesAfterProcessing,
                cts.Token);

            StatusMessage = $"Successfully processed {downloadedPaths.Count} remote file(s).";
            Logger.Info("Remote files processed: {Count}", downloadedPaths.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Remote file loading cancelled.";
            Logger.Info("Remote file loading cancelled by user.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading remote files");
            StatusMessage = $"Remote loading error: {ex.Message}";
        }
        finally
        {
            _sshConnectionService.Disconnect();

            if (cts != null)
            {
                _loadingCts = null;
                cts.Dispose();
            }

            // Удаляем каталог скачивания только если пользователь включил удаление локальных
            // файлов после обработки. Иначе файлы остаются в стабильной папке приложения.
            if (deleteLocalFilesAfterProcessing && !string.IsNullOrEmpty(downloadDirectory))
            {
                try
                {
                    if (Directory.Exists(downloadDirectory))
                        Directory.Delete(downloadDirectory, true);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to delete download directory: {Path}", downloadDirectory);
                }
            }

            IsFileLoading = false;
            LoadProgress = 0;
            OpenRemoteFileCommand.NotifyCanExecuteChanged();
            OpenLocalFileCommand.NotifyCanExecuteChanged();
        }
    }

    private RemoteFileSelectionViewModel CreateFileSelectionViewModel(
        SshConnectionSettings settings,
        IReadOnlyList<RemoteFileInfo> files,
        string downloadDirectory)
    {
        var viewModel = new RemoteFileSelectionViewModel();
        viewModel.Initialize(
            settings.Hostname,
            settings.Port,
            settings.RemoteDirectory,
            files);

        viewModel.DeleteAfterProcessing = settings.DeleteAfterProcessingFromServer;

        // Целевая папка скачивания — для проверки свободного места перед подтверждением.
        viewModel.TargetDownloadDirectory = downloadDirectory;

        // Источник обновления списка: команда RefreshAsync во ViewModel сама асинхронна,
        // отменяема и сама маршалит обновление списка (выполняется на UI-потоке).
        viewModel.SetRefreshCallback(token =>
            _remoteFileService.GetFilesAsync(settings.RemoteDirectory, token));

        return viewModel;
    }

    private async Task<IReadOnlyList<string>> DownloadFilesWithProgressAsync(
        IReadOnlyList<RemoteFileInfo> files,
        string downloadDirectory,
        bool deleteAfterDownload,
        CancellationToken cancellationToken)
    {
        var progressViewModel = new DownloadProgressViewModel();
        progressViewModel.Initialize(files);

        var progressWindow = new DownloadProgressWindow(progressViewModel);

        var downloadedPaths = new List<string>();

        // Показываем окно прогресса (неблокирующее)
        progressWindow.Show();

        try
        {
            IProgress<(int FileIndex, int TotalFiles, long BytesTransferred, long TotalBytes)> progress =
                new Progress<(int FileIndex, int TotalFiles, long BytesTransferred, long TotalBytes)>(p =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        progressViewModel.UpdateProgress(p.FileIndex, p.TotalFiles, p.BytesTransferred,
                            p.TotalBytes);
                    });
                });

            // Подписываемся на отмену
            progressViewModel.CancelRequested += (_, _) => { _loadingCts?.Cancel(); };

            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var file = files[i];

                await Dispatcher.UIThread.InvokeAsync(() => { progressViewModel.FileStarted(file.FileName); });

                var fileProgress = new Progress<(long BytesTransferred, long TotalBytes)>(p =>
                {
                    progress.Report((i, files.Count, p.BytesTransferred, p.TotalBytes));
                });

                var localPath = await _remoteFileService.DownloadFileAsync(
                    file,
                    downloadDirectory,
                    fileProgress,
                    cancellationToken);

                downloadedPaths.Add(localPath);

                await Dispatcher.UIThread.InvokeAsync(() => { progressViewModel.FileCompleted(file.FileName); });
            }

            // Удаляем с сервера если нужно
            if (deleteAfterDownload)
            {
                StatusMessage = "Deleting files from server...";
                var remotePaths = files.Select(f => f.FullPath).ToList();
                await _remoteFileService.DeleteFilesAsync(remotePaths, cancellationToken);
                Logger.Info("Deleted {Count} files from server", remotePaths.Count);
            }

            await Dispatcher.UIThread.InvokeAsync(() => { progressViewModel.DownloadCompleted(); });

            // Ждём 2 секунды, чтобы пользователь увидел завершение
            await Task.Delay(2000, cancellationToken);

            return downloadedPaths;
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { progressViewModel.DownloadFailed(ex.Message); });

            throw;
        }
        finally
        {
            // Гарантированно закрываем окно прогресса при любом исходе.
            // Снимаем IsDownloading, иначе обработчик Closing заблокирует закрытие (например, при отмене).
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                progressViewModel.IsDownloading = false;
                progressWindow.Close();
            });
        }
    }

    private async Task ProcessDownloadedFilesAsync(
        IReadOnlyList<string> downloadedPaths,
        bool deleteAfterProcessing,
        CancellationToken cancellationToken)
    {
        var addedCount = 0;

        try
        {
            _isBatchUpdate = true;

            foreach (var path in downloadedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(path);

                if (!fileInfo.Exists)
                {
                    Logger.Warn("Downloaded file not found: {Path}", path);
                    continue;
                }

                StatusMessage = $"Processing: {fileInfo.Name}";

                var fileHash = await CalculateFileHashAsync(path, cancellationToken);

                if (IsDuplicate(fileHash))
                {
                    Logger.Warn("Duplicate file skipped: {FilePath}", path);

                    // Удаляем дубликат с диска только если включено удаление локальных файлов
                    if (deleteAfterProcessing)
                    {
                        try
                        {
                            File.Delete(path);
                        }
                        catch
                        {
                            /* ignore */
                        }
                    }

                    continue;
                }

                var traceModel = await ParseFileAsync(fileInfo, fileHash, cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                    FileCards.Add(CreateFileCardViewModel(traceModel)));

                addedCount++;

                // Удаляем локальный файл после обработки только если пользователь это выбрал
                if (deleteAfterProcessing)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to delete local file: {Path}", path);
                    }
                }
            }
        }
        finally
        {
            _isBatchUpdate = false;
            // Каталог скачивания удаляется в OpenRemoteFileAsync (finally) только если пользователь
            // включил удаление локальных файлов после обработки.
        }

        if (addedCount > 0) ApplyAllFilters();

        StatusMessage = $"Processed {addedCount} remote file(s).";
    }

    #endregion

    #region SSH Dependencies

    private readonly ISshConnectionService _sshConnectionService;
    private readonly IRemoteFileService _remoteFileService;

    #endregion

    #region Reparse Operations

    [RelayCommand(CanExecute = nameof(CanReparseFiles))]
    private async Task ReparseAllFilesAsync(CancellationToken cancellationToken)
    {
        if (FileCards.Count == 0)
        {
            StatusMessage = "No files to reprocess.";
            return;
        }

        IsFileLoading = true;
        NotifyCommandsCanExecuteChanged();

        try
        {
            _isBatchUpdate = true;

            var allCards = FileCards.ToList();
            StatusMessage = $"Reprocessing all files: 0/{allCards.Count}";

            for (var i = 0; i < allCards.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var card = allCards[i];
                StatusMessage = $"Reprocessing {i + 1}/{allCards.Count}: {card.FileInfo.FileName}";

                await ReparseTraceFileAsync(card, cancellationToken);
            }

            StatusMessage = $"All files reprocessed: {allCards.Count} file(s).";
            Logger.Info("Reprocessing completed: {Count} files", allCards.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Reprocessing cancelled.";
            Logger.Info("Reprocessing cancelled.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during reprocessing");
            StatusMessage = $"Reprocessing error: {ex.Message}";
        }
        finally
        {
            _isBatchUpdate = false;
            IsFileLoading = false;

            UpdateAvailableFilters();
            ApplyAllFilters();

            NotifyCommandsCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanReparseSelectedFiles))]
    private async Task ReparseSelectedFilesAsync(CancellationToken cancellationToken)
    {
        if (SelectedFileCards.Count == 0)
        {
            StatusMessage = "No files selected for reprocessing.";
            return;
        }

        IsFileLoading = true;
        NotifyCommandsCanExecuteChanged();

        try
        {
            _isBatchUpdate = true;

            var selectedCards = SelectedFileCards.ToList();
            StatusMessage = $"Reprocessing selected files: 0/{selectedCards.Count}";

            for (var i = 0; i < selectedCards.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var card = selectedCards[i];
                StatusMessage = $"Reprocessing {i + 1}/{selectedCards.Count}: {card.FileInfo.FileName}";

                await ReparseTraceFileAsync(card, cancellationToken);
            }

            StatusMessage = $"Selected files reprocessed: {selectedCards.Count} file(s).";
            Logger.Info("Selected files reprocessed: {Count}", selectedCards.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Reprocessing cancelled.";
            Logger.Info("Selected files reprocessing cancelled.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error reprocessing selected files");
            StatusMessage = $"Reprocessing error: {ex.Message}";
        }
        finally
        {
            _isBatchUpdate = false;
            IsFileLoading = false;

            UpdateAvailableFilters();
            ApplyAllFilters();

            NotifyCommandsCanExecuteChanged();
        }
    }

    private async Task ReparseTraceFileAsync(FileCardViewModel card,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fileInfo = new FileInfo(card.FileInfo.FilePath);

            if (!fileInfo.Exists)
            {
                StatusMessage = $"File not found: {card.FileInfo.FileName}";
                Logger.Warn("File not found for reparse: {Path}", card.FileInfo.FilePath);
                return;
            }

            RemoveFileEvents(card.FileInfo.FileHash);

            var updatedModel = await ParseFileAsync(fileInfo, card.FileInfo.FileHash, cancellationToken);

            await Dispatcher.UIThread.InvokeAsync(() => card.FileInfo = updatedModel);

            Logger.Info("File reparsed: {FileName}", card.FileInfo.FileName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error reparsing file: {FileName}", card.FileInfo.FileName);
            StatusMessage = $"Reparse error: {card.FileInfo.FileName}: {ex.Message}";
        }
    }

    private bool CanReparseFiles()
    {
        return !IsFileLoading && FileCards.Count > 0;
    }

    private bool CanReparseSelectedFiles()
    {
        return !IsFileLoading && SelectedFileCards.Count > 0;
    }

    #endregion

    #region Close File Operations

    /// <summary>Закрывает все файлы</summary>
    [RelayCommand(CanExecute = nameof(CanCloseAllFiles))]
    private void CloseAllFiles()
    {
        if (FileCards.Count == 0)
        {
            StatusMessage = "No files to close.";
            return;
        }

        try
        {
            _isBatchUpdate = true;

            var count = FileCards.Count;

            // ✅ Очищаем списки в _eventsByFileHash перед очисткой словаря
            foreach (var eventList in _eventsByFileHash.Values) eventList.Clear();

            // Очищаем все коллекции
            FileCards.Clear();
            AllEvents.Clear();
            VisibleEvents.Clear();
            _eventsByFileHash.Clear();

            StatusMessage = $"Closed all files: {count} file(s).";
            Logger.Info("All files closed: {Count}", count);

            // ✅ Принудительная сборка мусора
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            Logger.Info("GC forced after closing all files");
        }
        finally
        {
            _isBatchUpdate = false;
        }

        // Обновляем UI после очистки
        UpdateAvailableFilters();
        UpdateAvailableSorts();
        UpdateStatistics();
    }

    /// <summary>Закрытие выбранных файлов</summary>
    [RelayCommand(CanExecute = nameof(CanCloseSelectedFiles))]
    private void CloseSelectedFiles()
    {
        if (SelectedFileCards.Count == 0)
        {
            StatusMessage = "No files selected to close.";
            return;
        }

        try
        {
            _isBatchUpdate = true;

            var selectedCards = SelectedFileCards.ToList();

            // Удаляем события всех файлов ОДНОЙ операцией
            var hashesToRemove = selectedCards.Select(c => c.FileInfo.FileHash).ToList();
            RemoveMultipleFileEvents(hashesToRemove);

            // Удаляем карточки
            foreach (var card in selectedCards) FileCards.Remove(card);

            StatusMessage = $"Closed selected files: {selectedCards.Count} file(s).";
            Logger.Info("Selected files closed: {Count}", selectedCards.Count);
        }
        finally
        {
            _isBatchUpdate = false;
        }

        // Обновляем UI после удаления
        ApplyAllFilters();
    }

    private bool CanCloseAllFiles()
    {
        return !IsFileLoading && FileCards.Count > 0;
    }

    private bool CanCloseSelectedFiles()
    {
        return !IsFileLoading && SelectedFileCards.Count > 0;
    }

    #endregion

    #region UI Commands

    [RelayCommand]
    private void SwitchVisibleTraceFilesSection()
    {
        IsTraceFilesSectionVisible = !IsTraceFilesSectionVisible;
    }

    [RelayCommand]
    private void SwitchVisibleSearchSection()
    {
        IsSearchSectionVisible = !IsSearchSectionVisible;
    }

    [RelayCommand]
    private void SwitchEventsSectionVisible()
    {
        IsEventsSectionVisible = !IsEventsSectionVisible;
    }

    [RelayCommand]
    private void SwitchStatisticsSectionVisible()
    {
        IsStatisticsSectionVisible = !IsStatisticsSectionVisible;
    }

    [RelayCommand]
    private void SwitchLogsSectionVisible()
    {
        IsLogsSectionVisible = !IsLogsSectionVisible;
    }

    /// <summary>Открывает окно настроек приложения.</summary>
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        try
        {
            var viewModel = App.Services?.GetRequiredService<SettingsWindowViewModel>();
            if (viewModel == null)
            {
                StatusMessage = "Settings service not available";
                return;
            }

            var changed = await Dialogs.ShowDialogAsync<bool>(viewModel);

            if (!changed)
                return;

            // Подтягиваем сохранённые значения в живые свойства окна. _appSettings/_uiSettings —
            // те же экземпляры, что и в SettingsService, поэтому уже содержат новые значения.
            IsTraceFilesSectionVisible = _uiSettings.Files;
            IsSearchSectionVisible = _uiSettings.Search;
            IsEventsSectionVisible = _uiSettings.Events;
            IsStatisticsSectionVisible = _uiSettings.Statistics;
            IsLogsSectionVisible = _uiSettings.Logs;
            IsClassicSearch = _appSettings.IsClassicSearch;

            StatusMessage = "Settings updated";
            Logger.Info("Settings updated from settings window");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening settings window");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void GoToFactorySettingsSection()
    {
        IsTraceFilesSectionVisible = _uiSettings.Files;
        IsSearchSectionVisible = _uiSettings.Search;
        IsEventsSectionVisible = _uiSettings.Events;
        IsStatisticsSectionVisible = _uiSettings.Statistics;
        IsLogsSectionVisible = _uiSettings.Logs;

        Logger.Info("Factory settings restored.");
        StatusMessage = "Factory settings restored.";
    }

    #endregion

    #region Utilities

    partial void OnIsFileLoadingChanged(bool value)
    {
        NotifyCommandsCanExecuteChanged();
    }

    private void NotifyCommandsCanExecuteChanged()
    {
        OpenLocalFileCommand.NotifyCanExecuteChanged();
        CancelLoadingCommand.NotifyCanExecuteChanged();
        ReparseAllFilesCommand.NotifyCanExecuteChanged();
        ReparseSelectedFilesCommand.NotifyCanExecuteChanged();
        CloseAllFilesCommand.NotifyCanExecuteChanged();
        CloseSelectedFilesCommand.NotifyCanExecuteChanged();
    }

    private void UpdateStatistics()
    {
        var totalEvents = FileCards.Sum(f => f.FileInfo.EventCount);

        StatisticInfoModels.UpdateStatistics([
            new StatisticInfoModel("Files:", FileCards.Count.ToString()),
            new StatisticInfoModel("All Events:", totalEvents.ToString("N0")),
            new StatisticInfoModel("Visible Events:", VisibleEvents.Count.ToString("N0"))
        ]);
    }

    private static async Task<string> CalculateFileHashAsync(string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            true);

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes);
    }

    private static string BuildFileAddingStatusMessage(int addedCount, int duplicateCount)
    {
        return (addedCount, duplicateCount) switch
        {
            (> 0, > 0) => $"Loaded: {addedCount} file(s). Skipped duplicates: {duplicateCount}.",
            (> 0, 0) => $"Loaded: {addedCount} file(s).",
            (0, > 0) => "No files loaded: all files are duplicates.",
            _ => "No files selected."
        };
    }

    #endregion
}