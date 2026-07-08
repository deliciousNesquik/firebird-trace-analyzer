using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Channels;
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
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Mocks;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceAnalyzer.Services.Persistence;
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

    // --- Презентация загрузки: док-панель снизу-справа / вынесенное окно ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloadDockVisible))]
    private DownloadProgressViewModel? _activeDownload;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloadDockVisible))]
    private bool _isDownloadPoppedOut;

    /// <summary>Мини-панель загрузки видна, пока идёт загрузка и она не вынесена в отдельное окно.</summary>
    public bool IsDownloadDockVisible => ActiveDownload is not null && !IsDownloadPoppedOut;

    // Экземпляр вынесенного окна прогресса (когда IsDownloadPoppedOut == true).
    private DownloadProgressWindow? _downloadWindow;

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
            new StatisticInfoModel(Loc.Tr("Status.Main.StatFiles"), FileCards.Count.ToString()),
            new StatisticInfoModel(Loc.Tr("Status.Main.StatAllEvents"), AllEvents.Count.ToString()),
            new StatisticInfoModel(Loc.Tr("Status.Main.StatVisibleEvents"), VisibleEvents.Count.ToString()),
            new StatisticInfoModel(Loc.Tr("Status.Main.StatFilteredEvents"), AllEvents.Count.ToString())
        ]);

        LoadSettings();
        StatusMessage = Loc.Tr("Status.Main.ReadyDesignTime");
        
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

        // Регистрация пользовательских сортировок и фильтров из плагинов
        RegisterCustomSorts();
        RegisterCustomFilters();

        // Загрузка настроек
        LoadSettings();

        // С этого момента изменения секций/поиска можно сохранять
        _settingsLoaded = true;

        StatusMessage = Loc.Tr("Status.Main.Ready");
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
        StatusMessage = Loc.Tr("Status.Main.SettingsLoaded");
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
            try
            {
                // GetSorts() — код плагина; изолируем его, чтобы исключение не роняло запуск приложения.
                foreach (var sortDescriptor in plugin.GetSorts())
                {
                    _sortingService.RegisterCustomSort(sortDescriptor);
                    loadedSortsCount++;
                }

                Logger.Info($"Loaded sorts from plugin: {plugin.Name} (v{plugin.Version})");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Plugin '{Name}' (v{Version}) failed in GetSorts() — skipped",
                    plugin.Name, plugin.Version);
            }
        }

        Logger.Info($"Total custom sorts registered from plugins: {loadedSortsCount}");
    }

    /// <summary>Регистрирует фильтры из загруженных плагинов</summary>
    private void RegisterCustomFilters()
    {
        // Плагины уже загружены с диска в RegisterCustomSorts (LoadAllPlugins), поэтому повторно
        // не сканируем — просто берём те, что поддерживают фильтрацию.
        var filterPlugins = _pluginManager.GetFilterPlugins();

        int loadedFiltersCount = 0;

        foreach (var plugin in filterPlugins)
        {
            try
            {
                // GetFilters() — код плагина; изолируем его, чтобы исключение не роняло запуск приложения.
                foreach (var filterDescriptor in plugin.GetFilters())
                {
                    _filteringService.RegisterCustomFilter(filterDescriptor);
                    loadedFiltersCount++;
                }

                Logger.Info($"Loaded filters from plugin: {plugin.Name} (v{plugin.Version})");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Plugin '{Name}' (v{Version}) failed in GetFilters() — skipped",
                    plugin.Name, plugin.Version);
            }
        }

        Logger.Info($"Total custom filters registered from plugins: {loadedFiltersCount}");
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

        StatusMessage = string.Format(
            Loc.Tr("Status.Main.SortedBy"),
            SelectedSort.DisplayName,
            IsSortDescending ? "desc" : "asc");

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

        StatusMessage = string.Format(Loc.Tr("Status.Main.AvailableFilters"), filters.Count);
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
                statusParts.Add(string.Format(Loc.Tr("Status.Main.SearchLabel"), SearchText));

            statusParts.Add(string.Format(Loc.Tr("Status.Main.EventsCount"), filteredList.Count, AllEvents.Count));

            StatusMessage = string.Join(" • ", statusParts);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error applying filters");
            StatusMessage = string.Format(Loc.Tr("Status.Main.FilteringError"), ex.Message);
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
    private static string GetApplicationVersion() => Core.AppVersion.Current;
    
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
            StatusMessage = Loc.Tr("Status.Main.GeneratingReport");
            Logger.Info("Quick report requested: {TemplateId}", templateId);

            // Получаем сервисы
            var templateService = App.Services?.GetRequiredService<IReportTemplateService>();
            var generationService = App.Services?.GetRequiredService<IReportGenerationService>();

            if (templateService == null || generationService == null)
            {
                StatusMessage = Loc.Tr("Status.Main.ReportServicesNotAvailable");
                Logger.Error("Report services not registered in DI");
                return;
            }

            // Загружаем шаблон
            var template = await templateService.GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                StatusMessage = string.Format(Loc.Tr("Status.Main.TemplateNotFound"), templateId);
                Logger.Warn("Template not found: {TemplateId}", templateId);
                return;
            }

            // Подготавливаем события для отчёта (сортировку применит сам сервис по шаблону)
            var preparedEvents = generationService.PrepareEventsForReport(
                VisibleEvents,
                template);

            if (preparedEvents.Count == 0)
            {
                StatusMessage = Loc.Tr("Status.Main.NoEventsForReport");
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

            StatusMessage = string.Format(Loc.Tr("Status.Main.ReportGenerated"), generatedReport.FilePath);
            Logger.Info("Report generated successfully: {Path}", generatedReport.FilePath);

            // Показываем уведомление с предложением открыть
            await ShowReportGeneratedNotificationAsync(generatedReport);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.Tr("Status.Main.ReportGenerationCancelled");
            Logger.Info("Report generation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Loc.Tr("Status.Main.ReportGenerationError"), ex.Message);
            Logger.Error(ex, "Error generating report");
        }
        finally
        {
            IsFileLoading = false;
        }
    }

    /// <summary>
    /// Открывает единый редактор отчётов: создание нового шаблона (editTemplateId == null) или
    /// редактирование существующего (подтягивает его параметры). Возвращает сохранённый шаблон или null.
    /// </summary>
    private async Task<ReportTemplate?> OpenReportEditorAsync(string? editTemplateId)
    {
        var designerViewModel = App.Services?.GetRequiredService<ReportDesignerViewModel>();

        if (designerViewModel == null)
        {
            StatusMessage = Loc.Tr("Status.Main.ReportServicesNotAvailable");
            return null;
        }

        // Для редактирования нужны события сессии: по ним восстанавливаются доступные поля/фильтры/
        // сортировки, к которым привязывается загружаемый шаблон (иначе маппинг ничего не найдёт).
        if (editTemplateId != null && VisibleEvents.Count == 0)
        {
            StatusMessage = Loc.Tr("Status.Main.LoadTraceBeforeEdit");
            return null;
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

        // Для редактирования подтягиваем параметры существующего шаблона.
        if (editTemplateId != null)
            await designerViewModel.LoadTemplateAsync(editTemplateId);

        // Открываем редактор как in-window overlay (стек диалогов: поверх окна управления шаблонами).
        designerViewModel.MarkPreviewDirty();
        var result = await Dialogs.ShowDialogAsync<ReportTemplate?>(designerViewModel);

        if (result != null)
        {
            StatusMessage = editTemplateId == null
                ? string.Format(Loc.Tr("Status.Main.TemplateCreated"), result.Name)
                : string.Format(Loc.Tr("Status.Main.TemplateUpdated"), result.Name);
            Logger.Info("Report template saved: {Name}", result.Name);

            await RefreshCustomReportsAsync();
        }

        return result;
    }

    /// <summary>Открывает встроенное окно управления кастомными шаблонами отчётов.</summary>
    [RelayCommand]
    private async Task OpenManageTemplatesAsync()
    {
        try
        {
            var vm = App.Services?.GetRequiredService<ManageTemplatesViewModel>();

            if (vm == null)
            {
                StatusMessage = Loc.Tr("Status.Main.ReportServicesNotAvailable");
                return;
            }

            await vm.LoadAsync();

            // Create/Edit требуют сессии событий и окна редактора — обрабатываем здесь, затем
            // перезагружаем список в окне.
            async void OnEdit(object? _, string id)
            {
                await OpenReportEditorAsync(id);
                await vm.LoadAsync();
            }

            async void OnCreate(object? _, EventArgs __)
            {
                await OpenReportEditorAsync(null);
                await vm.LoadAsync();
            }

            vm.EditRequested += OnEdit;
            vm.CreateRequested += OnCreate;

            try
            {
                await Dialogs.ShowDialogAsync<object>(vm);
            }
            finally
            {
                vm.EditRequested -= OnEdit;
                vm.CreateRequested -= OnCreate;
                await RefreshCustomReportsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening manage templates");
            StatusMessage = string.Format(Loc.Tr("Status.Main.Error"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenRecentReportsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var historyViewModel = new ReportHistoryViewModel(_fileDialogService, _settingsService);
            await historyViewModel.LoadReportsCommand.ExecuteAsync(null);

            await Dialogs.ShowDialogAsync<object>(historyViewModel);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening recent reports");
            StatusMessage = string.Format(Loc.Tr("Status.Main.Error"), ex.Message);
        }
    }

    [RelayCommand]
    private Task OpenReportDesignerAsync(CancellationToken cancellationToken)
    {
        // TODO: Открыть окно дизайнера отчётов
        StatusMessage = Loc.Tr("Status.Main.ReportDesignerComingSoon");
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
            StatusMessage = Loc.Tr("Status.Main.SearchQueryEmpty");
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

        StatusMessage = Loc.Tr("Status.Main.SearchReset");
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
                StatusMessage = Loc.Tr("Status.Main.NoFilesSelected");
                return;
            }

            await ProcessSelectedFilesAsync(files, cts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.Tr("Status.Main.FileLoadingCancelled");
            Logger.Info("File loading cancelled by user.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading files");
            StatusMessage = string.Format(Loc.Tr("Status.Main.LoadingError"), ex.Message);
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

                StatusMessage = string.Format(Loc.Tr("Status.Main.ProcessingFileProgress"), i + 1, files.Count, Path.GetFileName(path));
                LoadProgress = (double)(i + 1) / files.Count * 100;

                var fileHash = await CalculateFileHashAsync(path, cancellationToken);

                if (IsDuplicate(fileHash))
                {
                    duplicateCount++;
                    Logger.Warn("Duplicate file skipped: {FilePath}", path);
                    continue;
                }

                var fileInfo = new FileInfo(path);

                // Кэш переоткрытия: если файл с этим хэшем уже в хранилище — читаем события с диска
                // вместо повторного парсинга (мгновенное переоткрытие / восстановление после падения).
                var store = StoreIfEnabled();
                var traceModel = store is not null && await ContainsInStoreAsync(store, fileHash, cancellationToken)
                    ? await LoadFromStoreAsync(fileInfo, fileHash, store, cancellationToken)
                    : await ParseFileAsync(fileInfo, fileHash, cancellationToken);

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
        StatusMessage = string.Format(Loc.Tr("Status.Main.Parsing"), fileInfo.Name);

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

        var model = new TraceFileInfoModel(
            fileInfo.Name,
            fileInfo.FullName,
            fileInfo.Length,
            startTrace,
            endTrace,
            events.Count,
            fileHash);

        // Хранилище: пишем распарсенные события на диск (аддитивно, за флагом StorageMode,
        // вне UI-потока, не фатально). WriteFile заменяет данные файла по хэшу — корректно для reparse.
        await WriteToStoreAsync(model, events, cancellationToken);

        return model;
    }

    // Единый шлюз доступа к стору: SQLite-соединение однопоточное, поэтому все операции с хранилищем
    // (запись при парсинге, удаление при закрытии) сериализуются здесь, даже если идут с фоновых потоков.
    private readonly SemaphoreSlim _storeGate = new(1, 1);

    /// <summary>Хранилище событий, если режим не Off; иначе null (и БД не создаётся).</summary>
    private IEventStore? StoreIfEnabled()
        => _appSettings.StorageMode == StorageMode.Off ? null : App.Services?.GetService<IEventStore>();

    /// <summary>
    /// Пишет события файла в дисковое хранилище на фоне, сериализованно через <see cref="_storeGate"/>.
    /// Ошибка записи не рушит парсинг/UI.
    /// </summary>
    private async Task WriteToStoreAsync(TraceFileInfoModel file, List<EventBase> events, CancellationToken ct)
    {
        var store = StoreIfEnabled();
        if (store is null)
            return;

        await _storeGate.WaitAsync(ct);
        try
        {
            await Task.Run(() => store.WriteFile(file, events), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "EventStore: failed to persist file {File}", file.FileName);
        }
        finally
        {
            _storeGate.Release();
        }
    }

    /// <summary>Проверяет наличие файла в хранилище (сериализованно через шлюз — соединение однопоточное).</summary>
    private async Task<bool> ContainsInStoreAsync(IEventStore store, string fileHash, CancellationToken ct)
    {
        await _storeGate.WaitAsync(ct);
        try
        {
            return await Task.Run(() => store.ContainsFile(fileHash), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "EventStore: ContainsFile failed for {Hash}", fileHash);
            return false; // при сбое проверки просто парсим как обычно
        }
        finally
        {
            _storeGate.Release();
        }
    }

    /// <summary>
    /// Читает события файла из хранилища и заполняет рабочий набор — зеркально <see cref="ParseFileAsync"/>,
    /// но без парсинга и без повторной записи в стор. Диапазон времени берём из первого/последнего события
    /// (порядок записи = порядок разбора).
    /// </summary>
    private async Task<TraceFileInfoModel> LoadFromStoreAsync(
        FileInfo fileInfo,
        string fileHash,
        IEventStore store,
        CancellationToken cancellationToken)
    {
        StatusMessage = string.Format(Loc.Tr("Status.Main.LoadingFromStore"), fileInfo.Name);
        Logger.Info("Loading from store (cache hit): {FileName}", fileInfo.Name);

        await _storeGate.WaitAsync(cancellationToken);
        IReadOnlyList<EventBase> restored;
        try
        {
            restored = await Task.Run(() => store.ReadFile(fileHash), cancellationToken);
        }
        finally
        {
            _storeGate.Release();
        }

        var events = restored as List<EventBase> ?? restored.ToList();

        _eventsByFileHash[fileHash] = events;
        AllEvents.AddRange(events);

        var startTrace = events.Count > 0 ? events[0].Timestamp : DateTime.MinValue;
        var endTrace = events.Count > 0 ? events[^1].Timestamp : DateTime.MinValue;

        Logger.Info("Loaded {Count} event(s) from store: {FileName}", events.Count, fileInfo.Name);

        return new TraceFileInfoModel(
            fileInfo.Name,
            fileInfo.FullName,
            fileInfo.Length,
            startTrace,
            endTrace,
            events.Count,
            fileHash);
    }

    /// <summary>
    /// Режим Session — «зеркало сессии»: при закрытии/удалении файлов убираем их и из хранилища,
    /// чтобы стор всегда отражал загруженный набор. В Accumulate ничего не удаляем (архив хранит всё).
    /// Удаление идёт на фоне и сериализовано тем же шлюзом, что и запись.
    /// </summary>
    private void RemoveFromStoreIfSession(IEnumerable<string> fileHashes)
    {
        if (_appSettings.StorageMode != StorageMode.Session)
            return;

        var store = App.Services?.GetService<IEventStore>();
        if (store is null)
            return;

        var hashes = fileHashes.ToList();
        if (hashes.Count == 0)
            return;

        _ = Task.Run(async () =>
        {
            await _storeGate.WaitAsync();
            try
            {
                foreach (var hash in hashes)
                    store.DeleteFile(hash);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "EventStore: failed to delete {Count} file(s) on close", hashes.Count);
            }
            finally
            {
                _storeGate.Release();
            }
        });
    }

    /// <summary>Режим Session: полностью очищает хранилище (закрытие всех файлов = пустая сессия).</summary>
    private void ClearStoreIfSession()
    {
        if (_appSettings.StorageMode != StorageMode.Session)
            return;

        var store = App.Services?.GetService<IEventStore>();
        if (store is null)
            return;

        _ = Task.Run(async () =>
        {
            await _storeGate.WaitAsync();
            try
            {
                store.Clear();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "EventStore: failed to clear on close-all");
            }
            finally
            {
                _storeGate.Release();
            }
        });
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

            StatusMessage = string.Format(Loc.Tr("Status.Main.FileRemoved"), card.FileInfo.FileName);
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
    private void RemoveFileEvents(string fileHash, bool removeFromStore = true)
    {
        if (!_eventsByFileHash.TryGetValue(fileHash, out var eventsToRemove))
            return;

        // Зеркало сессии: убираем файл из хранилища (кроме внутреннего reparse, который тут же перезапишет).
        if (removeFromStore)
            RemoveFromStoreIfSession(new[] { fileHash });

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

        // Зеркало сессии: убираем закрытые файлы и из хранилища.
        RemoveFromStoreIfSession(hashList);

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
                StatusMessage = Loc.Tr("Status.Main.ConnectionCancelled");
                return;
            }

            var settings = _sshConnectionService.CurrentSettings;
            if (settings == null)
            {
                StatusMessage = Loc.Tr("Status.Main.NoConnectionSettings");
                return;
            }

            StatusMessage = Loc.Tr("Status.Main.FetchingFileList");
            Logger.Info("Fetching files from {Directory}", settings.RemoteDirectory);

            // Получаем список файлов
            var remoteFiles = await _remoteFileService.GetFilesAsync(
                settings.RemoteDirectory,
                cts.Token);

            if (remoteFiles.Count == 0)
            {
                StatusMessage = Loc.Tr("Status.Main.NoTraceFilesOnServer");
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
                StatusMessage = Loc.Tr("Status.Main.NoFilesSelected");
                _sshConnectionService.Disconnect();
                return;
            }

            // Создаём каталог только после подтверждения выбора.
            Directory.CreateDirectory(downloadDirectory);

            int processedCount;

            if (_appSettings.AllowConcurrentProcessing)
            {
                // (Advanced) Overlap: качаем и парсим одновременно. Цикл скачивания — producer,
                // единственный consumer-таск парсит файлы по мере готовности. Один consumer →
                // AllEvents/_eventsByFileHash трогает только один поток, гонок нет.
                var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true
                });

                var downloadTask = DownloadFilesWithProgressAsync(
                    selectedFiles,
                    downloadDirectory,
                    settings.DeleteAfterProcessingFromServer,
                    cts.Token,
                    channel.Writer);

                var processTask = ProcessDownloadedFilesStreamAsync(
                    channel.Reader,
                    deleteLocalFilesAfterProcessing,
                    cts.Token);

                await Task.WhenAll(downloadTask, processTask);
                processedCount = await processTask;
            }
            else
            {
                // Последовательно: сначала скачиваем все файлы, затем парсим.
                var downloadedPaths = await DownloadFilesWithProgressAsync(
                    selectedFiles,
                    downloadDirectory,
                    settings.DeleteAfterProcessingFromServer,
                    cts.Token);

                processedCount = await ProcessDownloadedFilesAsync(
                    downloadedPaths,
                    deleteLocalFilesAfterProcessing,
                    cts.Token);
            }

            StatusMessage = string.Format(Loc.Tr("Status.Main.SuccessfullyProcessedRemote"), processedCount);
            Logger.Info("Remote files processed: {Count}", processedCount);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.Tr("Status.Main.RemoteLoadingCancelled");
            Logger.Info("Remote file loading cancelled by user.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading remote files");
            StatusMessage = string.Format(Loc.Tr("Status.Main.RemoteLoadingError"), ex.Message);
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

    /// <summary>Выносит текущую загрузку из док-панели в отдельное окно.</summary>
    [RelayCommand]
    private void PopOutDownload()
    {
        if (ActiveDownload is null)
            return;

        var window = new DownloadProgressWindow(ActiveDownload);

        // Закрытие окна (X) во время загрузки не отменяет её, а возвращает в док-панель.
        window.Closing += (_, _) =>
        {
            _downloadWindow = null;
            if (ActiveDownload is { IsDownloading: true })
                IsDownloadPoppedOut = false;
        };

        _downloadWindow = window;
        IsDownloadPoppedOut = true;

        var owner = App.Services?.GetRequiredService<IWindowProvider>().GetCurrent() as Window;
        if (owner is not null)
            window.Show(owner);
        else
            window.Show();
    }

    private async Task<IReadOnlyList<string>> DownloadFilesWithProgressAsync(
        IReadOnlyList<RemoteFileInfo> files,
        string downloadDirectory,
        bool deleteAfterDownload,
        CancellationToken cancellationToken,
        ChannelWriter<string>? sink = null)
    {
        var progressViewModel = new DownloadProgressViewModel();
        progressViewModel.Initialize(files);

        var downloadedPaths = new List<string>();

        // Презентация: сразу мини-панелью снизу-справа (немодально). Вынос в окно — по кнопке.
        ActiveDownload = progressViewModel;
        IsDownloadPoppedOut = false;

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

                // В overlap-режиме отдаём путь потребителю сразу, чтобы парсинг шёл параллельно
                // со скачиванием следующего файла.
                if (sink is not null)
                    await sink.WriteAsync(localPath, cancellationToken);
            }

            // Удаляем с сервера если нужно
            if (deleteAfterDownload)
            {
                StatusMessage = Loc.Tr("Status.Main.DeletingFromServer");
                var remotePaths = files.Select(f => f.FullPath).ToList();
                await _remoteFileService.DeleteFilesAsync(remotePaths, cancellationToken);
                Logger.Info("Deleted {Count} files from server", remotePaths.Count);
            }

            // Все файлы отданы потребителю: закрываем канал, чтобы consumer завершил обработку,
            // не дожидаясь косметической паузы ниже.
            sink?.TryComplete();

            await Dispatcher.UIThread.InvokeAsync(() => { progressViewModel.DownloadCompleted(); });

            // Ждём 2 секунды, чтобы пользователь увидел завершение
            await Task.Delay(2000, cancellationToken);

            return downloadedPaths;
        }
        catch (Exception ex)
        {
            // Пробрасываем ошибку потребителю (если он есть), чтобы его await foreach завершился
            // исключением, а не завис в ожидании новых элементов.
            sink?.TryComplete(ex);

            await Dispatcher.UIThread.InvokeAsync(() => { progressViewModel.DownloadFailed(ex.Message); });

            throw;
        }
        finally
        {
            // Страховка: если канал ещё открыт (например, неожиданный выход) — закрываем.
            sink?.TryComplete();

            // Гарантированно убираем презентацию при любом исходе (док и/или вынесенное окно).
            // IsDownloading снимаем заранее, чтобы обработчик Closing не вернул окно в док.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                progressViewModel.IsDownloading = false;
                _downloadWindow?.Close();
                _downloadWindow = null;
                ActiveDownload = null;
                IsDownloadPoppedOut = false;
            });
        }
    }

    /// <summary>
    /// Последовательная обработка: все файлы уже скачаны, парсим их по списку.
    /// Возвращает число реально добавленных (не-дубликатных) файлов.
    /// </summary>
    private async Task<int> ProcessDownloadedFilesAsync(
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

                if (await ProcessSingleDownloadedFileAsync(path, deleteAfterProcessing, cancellationToken))
                    addedCount++;
            }
        }
        finally
        {
            _isBatchUpdate = false;
            // Каталог скачивания удаляется в OpenRemoteFileAsync (finally) только если пользователь
            // включил удаление локальных файлов после обработки.
        }

        if (addedCount > 0) ApplyAllFilters();

        StatusMessage = string.Format(Loc.Tr("Status.Main.ProcessedRemoteFiles"), addedCount);

        return addedCount;
    }

    /// <summary>
    /// (Advanced) Overlap-обработка: consumer читает пути скачанных файлов из канала и парсит их
    /// по мере поступления, пока producer (цикл скачивания) продолжает качать следующие.
    /// Единственный consumer — поэтому <see cref="ParseFileAsync"/> и коллекции остаются
    /// однопоточными. Возвращает число реально добавленных файлов.
    /// </summary>
    private async Task<int> ProcessDownloadedFilesStreamAsync(
        ChannelReader<string> reader,
        bool deleteAfterProcessing,
        CancellationToken cancellationToken)
    {
        var addedCount = 0;

        // Продолжения после await по каналу могут резюмироваться не на UI-потоке. Обработку каждого
        // файла (парсинг + мутация AllEvents/FileCards/StatusMessage) и обновление списка событий
        // выполняем строго на UI-потоке — как и остальной код приложения (single-threaded модель).
        // Overlap при этом сохраняется: тяжёлая SFTP-передача идёт в Task.Run внутри
        // DownloadFileAsync, пока UI-поток парсит уже скачанный файл.
        //
        // В отличие от последовательного режима, обновляем видимый список ПОСЛЕ КАЖДОГО файла, а не
        // одним пакетом в конце: смысл overlap-режима в том, чтобы события уже обработанных файлов
        // появлялись сразу, не дожидаясь окончания всей загрузки.
        await foreach (var path in reader.ReadAllAsync(cancellationToken))
        {
            var added = await Dispatcher.UIThread.InvokeAsync(
                () => ProcessSingleDownloadedFileAsync(path, deleteAfterProcessing, cancellationToken));

            if (!added)
                continue;

            addedCount++;
            await Dispatcher.UIThread.InvokeAsync(ApplyAllFilters);
        }

        StatusMessage = string.Format(Loc.Tr("Status.Main.ProcessedRemoteFiles"), addedCount);

        return addedCount;
    }

    /// <summary>
    /// Обрабатывает один скачанный файл: проверка существования, хэш, отсев дубликатов, парсинг,
    /// добавление карточки файла и (опционально) удаление локального файла. Возвращает
    /// <c>true</c>, если файл действительно добавлен (не пропущен и не дубликат).
    /// </summary>
    private async Task<bool> ProcessSingleDownloadedFileAsync(
        string path,
        bool deleteAfterProcessing,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileInfo = new FileInfo(path);

        if (!fileInfo.Exists)
        {
            Logger.Warn("Downloaded file not found: {Path}", path);
            return false;
        }

        StatusMessage = string.Format(Loc.Tr("Status.Main.ProcessingFile"), fileInfo.Name);

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

            return false;
        }

        var traceModel = await ParseFileAsync(fileInfo, fileHash, cancellationToken);

        await Dispatcher.UIThread.InvokeAsync(() =>
            FileCards.Add(CreateFileCardViewModel(traceModel)));

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

        return true;
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
            StatusMessage = Loc.Tr("Status.Main.NoFilesToReprocess");
            return;
        }

        IsFileLoading = true;
        NotifyCommandsCanExecuteChanged();

        try
        {
            _isBatchUpdate = true;

            var allCards = FileCards.ToList();
            StatusMessage = string.Format(Loc.Tr("Status.Main.ReprocessingAllStart"), allCards.Count);

            for (var i = 0; i < allCards.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var card = allCards[i];
                StatusMessage = string.Format(Loc.Tr("Status.Main.ReprocessingProgress"), i + 1, allCards.Count, card.FileInfo.FileName);

                await ReparseTraceFileAsync(card, cancellationToken);
            }

            StatusMessage = string.Format(Loc.Tr("Status.Main.AllFilesReprocessed"), allCards.Count);
            Logger.Info("Reprocessing completed: {Count} files", allCards.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.Tr("Status.Main.ReprocessingCancelled");
            Logger.Info("Reprocessing cancelled.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during reprocessing");
            StatusMessage = string.Format(Loc.Tr("Status.Main.ReprocessingError"), ex.Message);
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
            StatusMessage = Loc.Tr("Status.Main.NoFilesSelectedForReprocessing");
            return;
        }

        IsFileLoading = true;
        NotifyCommandsCanExecuteChanged();

        try
        {
            _isBatchUpdate = true;

            var selectedCards = SelectedFileCards.ToList();
            StatusMessage = string.Format(Loc.Tr("Status.Main.ReprocessingSelectedStart"), selectedCards.Count);

            for (var i = 0; i < selectedCards.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var card = selectedCards[i];
                StatusMessage = string.Format(Loc.Tr("Status.Main.ReprocessingProgress"), i + 1, selectedCards.Count, card.FileInfo.FileName);

                await ReparseTraceFileAsync(card, cancellationToken);
            }

            StatusMessage = string.Format(Loc.Tr("Status.Main.SelectedFilesReprocessed"), selectedCards.Count);
            Logger.Info("Selected files reprocessed: {Count}", selectedCards.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.Tr("Status.Main.ReprocessingCancelled");
            Logger.Info("Selected files reprocessing cancelled.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error reprocessing selected files");
            StatusMessage = string.Format(Loc.Tr("Status.Main.ReprocessingError"), ex.Message);
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
                StatusMessage = string.Format(Loc.Tr("Status.Main.FileNotFound"), card.FileInfo.FileName);
                Logger.Warn("File not found for reparse: {Path}", card.FileInfo.FilePath);
                return;
            }

            // reparse: не удаляем из стора — ParseFileAsync тут же перезапишет файл (иначе гонка «удаление после записи»).
            RemoveFileEvents(card.FileInfo.FileHash, removeFromStore: false);

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
            StatusMessage = string.Format(Loc.Tr("Status.Main.ReparseError"), card.FileInfo.FileName, ex.Message);
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
            StatusMessage = Loc.Tr("Status.Main.NoFilesToClose");
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

            // Зеркало сессии: пустая сессия → пустое хранилище.
            ClearStoreIfSession();

            StatusMessage = string.Format(Loc.Tr("Status.Main.ClosedAllFiles"), count);
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
            StatusMessage = Loc.Tr("Status.Main.NoFilesSelectedToClose");
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

            StatusMessage = string.Format(Loc.Tr("Status.Main.ClosedSelectedFiles"), selectedCards.Count);
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

    /// <summary>Открывает встроенное окно управления плагинами.</summary>
    [RelayCommand]
    private async Task OpenPluginsAsync() => await ShowPluginsDialogAsync(showCollisions: false);

    /// <summary>Открывает окно управления хранилищем событий (статистика + удаление/очистка).</summary>
    [RelayCommand]
    private async Task OpenStoreManagementAsync()
    {
        try
        {
            var store = App.Services?.GetService<IEventStore>();
            var windowProvider = App.Services?.GetService<IWindowProvider>();
            if (store is null || windowProvider is null)
            {
                StatusMessage = Loc.Tr("Store.Manage.Unavailable");
                return;
            }

            var vm = new StoreManagementViewModel(store, _storeGate, windowProvider);
            await vm.LoadAsync();
            await Dialogs.ShowDialogAsync<object>(vm);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening store management window");
            StatusMessage = string.Format(Loc.Tr("Status.Main.Error"), ex.Message);
        }
    }

    private async Task ShowPluginsDialogAsync(bool showCollisions)
    {
        try
        {
            var vm = new PluginsViewModel(_pluginManager, _fileDialogService, Dialogs);
            vm.LoadPlugins();
            if (showCollisions && vm.HasCollisions)
                vm.ShowCollisions = true;
            await Dialogs.ShowDialogAsync<object>(vm);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening plugins window");
            StatusMessage = string.Format(Loc.Tr("Status.Main.Error"), ex.Message);
        }
    }

    /// <summary>
    /// На старте (только режим Session — «зеркало сессии»): если в хранилище остались файлы прошлой
    /// сессии (например, после падения/зависания), предлагает выбрать и восстановить их из хранилища
    /// без повторного парсинга. В Accumulate архив может быть огромным — там восстановление не к месту
    /// (доступ к архиву — через отдельное окно управления/анализа).
    /// </summary>
    public async Task PromptSessionRecoveryAsync()
    {
        // Только зеркало сессии: восстанавливаем рабочий набор, а не весь накопительный архив.
        if (_appSettings.StorageMode != StorageMode.Session)
            return;

        var store = StoreIfEnabled();
        if (store is null)
            return;

        // Уже есть открытые файлы — не мешаем начатой работе.
        if (FileCards.Count > 0)
            return;

        List<TraceFileInfoModel> manifest;
        await _storeGate.WaitAsync();
        try
        {
            manifest = (await Task.Run(() => store.ListFiles())).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "EventStore: ListFiles failed on startup");
            return;
        }
        finally
        {
            _storeGate.Release();
        }

        if (manifest.Count == 0)
            return;

        var recoveryViewModel = new SessionRecoveryViewModel();
        recoveryViewModel.Initialize(manifest);

        var selected = await Dialogs.ShowDialogAsync<IReadOnlyList<TraceFileInfoModel>>(recoveryViewModel);
        if (selected is null || selected.Count == 0)
            return;

        await RestoreFilesAsync(selected, store);
    }

    /// <summary>
    /// Загружает выбранные при восстановлении файлы из хранилища в рабочий набор. Чтение с диска
    /// (без парсинга и без повторной записи в стор), карточки добавляются на UI-потоке.
    /// </summary>
    private async Task RestoreFilesAsync(IReadOnlyList<TraceFileInfoModel> files, IEventStore store)
    {
        var restored = 0;

        try
        {
            _isBatchUpdate = true;

            foreach (var model in files)
            {
                if (IsDuplicate(model.FileHash))
                    continue;

                IReadOnlyList<EventBase> events;
                await _storeGate.WaitAsync();
                try
                {
                    events = await Task.Run(() => store.ReadFile(model.FileHash));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "EventStore: failed to restore file {Hash}", model.FileHash);
                    continue;
                }
                finally
                {
                    _storeGate.Release();
                }

                var list = events as List<EventBase> ?? events.ToList();
                _eventsByFileHash[model.FileHash] = list;
                AllEvents.AddRange(list);

                var card = model;
                await Dispatcher.UIThread.InvokeAsync(() => FileCards.Add(CreateFileCardViewModel(card)));
                restored++;
            }
        }
        finally
        {
            _isBatchUpdate = false;
        }

        if (restored > 0)
            ApplyAllFilters();

        StatusMessage = string.Format(Loc.Tr("Status.Main.SessionRestored"), restored);
        Logger.Info("Session recovery: restored {Count} file(s) from store", restored);
    }

    /// <summary>
    /// На старте: если есть неразрешённые коллизии плагинов (включено более одного с одним Id),
    /// открывает окно плагинов сразу на разделе коллизий, чтобы пользователь выбрал. Необязательно —
    /// окно можно просто закрыть.
    /// </summary>
    public async Task PromptUnresolvedCollisionsAsync()
    {
        if (_pluginManager.HasUnresolvedCollisions())
            await ShowPluginsDialogAsync(showCollisions: true);
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
                StatusMessage = Loc.Tr("Status.Main.SettingsServiceNotAvailable");
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

            StatusMessage = Loc.Tr("Status.Main.SettingsUpdated");
            Logger.Info("Settings updated from settings window");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening settings window");
            StatusMessage = string.Format(Loc.Tr("Status.Main.Error"), ex.Message);
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
        StatusMessage = Loc.Tr("Status.Main.FactorySettingsRestored");
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
            new StatisticInfoModel(Loc.Tr("Status.Main.StatFiles"), FileCards.Count.ToString()),
            new StatisticInfoModel(Loc.Tr("Status.Main.StatAllEvents"), totalEvents.ToString("N0")),
            new StatisticInfoModel(Loc.Tr("Status.Main.StatVisibleEvents"), VisibleEvents.Count.ToString("N0"))
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
            (> 0, > 0) => string.Format(Loc.Tr("Status.Main.LoadedWithDuplicates"), addedCount, duplicateCount),
            (> 0, 0) => string.Format(Loc.Tr("Status.Main.Loaded"), addedCount),
            (0, > 0) => Loc.Tr("Status.Main.NoFilesLoadedAllDuplicates"),
            _ => Loc.Tr("Status.Main.NoFilesSelected")
        };
    }

    #endregion
}