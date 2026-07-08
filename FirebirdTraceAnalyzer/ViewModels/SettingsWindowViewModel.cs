using System.IO;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel окна настроек. Работает с рабочей копией значений: правки применяются к приложению
/// только по кнопке «Save». Reset/Import загружают значения в рабочую копию (тоже до Save),
/// Export пишет текущую рабочую копию в файл.
/// </summary>
public partial class SettingsWindowViewModel : ViewModelBase, IDialogViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ISettingsService _settingsService;
    private readonly IWindowProvider _windowProvider;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileDialogService? _fileDialogService;

    #region General

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRegexSearch))]
    private bool _isClassicSearch;

    /// <summary>Производное состояние для правой кнопки сегмента (обратное к Classic).</summary>
    public bool IsRegexSearch => !IsClassicSearch;

    [ObservableProperty] private AppTheme _theme = AppTheme.Auto;

    public IReadOnlyList<AppTheme> AvailableThemes { get; } = Enum.GetValues<AppTheme>();

    /// <summary>Выбранный язык интерфейса (элемент из <see cref="AvailableLanguages"/>).</summary>
    [ObservableProperty] private LanguageOption? _selectedLanguage;

    /// <summary>Доступные языки (из манифеста переводов).</summary>
    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    #endregion

    #region Advanced

    /// <summary>(Advanced) Парсить уже скачанный файл, пока качается следующий.</summary>
    [ObservableProperty] private bool _allowConcurrentProcessing;

    /// <summary>(Advanced) Режим разработчика: показывает диагностические инструменты (статистика парсера).</summary>
    [ObservableProperty] private bool _developerMode;

    #endregion

    #region Paths

    [ObservableProperty] private string _remoteDownloadPath = string.Empty;

    [ObservableProperty] private string _reportsPath = string.Empty;

    #endregion

    #region Storage

    /// <summary>Выбранный режим дискового хранилища событий (элемент из <see cref="AvailableStorageModes"/>).</summary>
    [ObservableProperty] private StorageModeOption? _selectedStorageMode;

    /// <summary>Доступные режимы хранилища с локализованными подписями.</summary>
    public IReadOnlyList<StorageModeOption> AvailableStorageModes { get; } =
    [
        new(StorageMode.Off, Loc.Tr("Settings.Storage.Mode.Off")),
        new(StorageMode.Session, Loc.Tr("Settings.Storage.Mode.Session")),
        new(StorageMode.Accumulate, Loc.Tr("Settings.Storage.Mode.Accumulate"))
    ];

    [ObservableProperty] private string _storagePath = string.Empty;

    #endregion

    #region Logs

    [ObservableProperty] private string _appLogPath = string.Empty;

    [ObservableProperty] private string _parserLogPath = string.Empty;

    #endregion

    #region UI Sections

    [ObservableProperty] private bool _sectionFiles;
    [ObservableProperty] private bool _sectionSearch;
    [ObservableProperty] private bool _sectionEvents;
    [ObservableProperty] private bool _sectionStatistics;
    [ObservableProperty] private bool _sectionLogs;

    #endregion

    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Запрос на закрытие окна. Аргумент: были ли сохранены изменения.</summary>
    public event EventHandler<object?>? CloseRequested;

    /// <summary>Конструктор только для XAML-дизайнера.</summary>
    public SettingsWindowViewModel()
    {
        _settingsService = null!;
        _windowProvider = null!;
        _themeService = null!;
        _localizationService = null!;
        _fileDialogService = null;
        AvailableLanguages = new[] { new LanguageOption("en", "English") };
    }

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        IWindowProvider windowProvider,
        IThemeService themeService,
        ILocalizationService localizationService,
        IFileDialogService fileDialogService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));

        AvailableLanguages = _localizationService.AvailableLanguages;

        LoadFrom(_settingsService.App, _settingsService.Ui);
    }

    #region Commands

    [RelayCommand]
    private void SelectClassic() => IsClassicSearch = true;

    [RelayCommand]
    private void SelectRegex() => IsClassicSearch = false;

    [RelayCommand]
    private void Save()
    {
        var app = _settingsService.App;
        app.IsClassicSearch = IsClassicSearch;
        app.AllowConcurrentProcessing = AllowConcurrentProcessing;
        app.DeveloperMode = DeveloperMode;
        app.Theme = Theme;
        app.Language = SelectedLanguage?.Code ?? "en";
        app.RemoteDownloadPath = RemoteDownloadPath?.Trim() ?? string.Empty;
        app.ReportsPath = ReportsPath?.Trim() ?? string.Empty;
        app.AppLogPath = AppLogPath?.Trim() ?? string.Empty;
        app.ParserLogPath = ParserLogPath?.Trim() ?? string.Empty;
        app.StorageMode = SelectedStorageMode?.Mode ?? StorageMode.Session;
        app.StoragePath = StoragePath?.Trim() ?? string.Empty;

        var ui = _settingsService.Ui;
        ui.Files = SectionFiles;
        ui.Search = SectionSearch;
        ui.Events = SectionEvents;
        ui.Statistics = SectionStatistics;
        ui.Logs = SectionLogs;

        _settingsService.Save();

        // Применяем тему и язык сразу, чтобы изменения вступили в силу без перезапуска.
        _themeService.Apply(Theme);
        _localizationService.SetLanguage(app.Language);

        // Применяем пути логов: File-таргеты NLog переключатся на новый путь со следующей записи.
        LogConfiguration.Apply(app.AppLogPath, app.ParserLogPath);

        Logger.Info("Settings saved from settings window");

        // Применили и сохранили → закрываем диалог с результатом «изменения сохранены»,
        // чтобы главное окно перечитало живые свойства (IsClassicSearch, видимость секций).
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var defaults = _settingsService.GetDefaults();
        LoadFrom(defaults.App, defaults.Ui);
        StatusMessage = Loc.Tr("Status.Settings.RestoredDefaults");
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var topLevel = _windowProvider.GetCurrent();
        if (topLevel?.StorageProvider == null)
            return;

        try
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Loc.Tr("FileDialog.ExportSettings"),
                SuggestedFileName = "firebird-trace-settings.json",
                DefaultExtension = "json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(Loc.Tr("FileDialog.SettingsFileType")) { Patterns = new[] { "*.json" } }
                }
            });

            if (file == null)
                return;

            await _settingsService.ExportAsync(file.Path.LocalPath, BuildWorkingSettings());
            StatusMessage = string.Format(Loc.Tr("Status.Settings.ExportedTo"), file.Name);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting settings");
            StatusMessage = string.Format(Loc.Tr("Status.Settings.ExportFailed"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var topLevel = _windowProvider.GetCurrent();
        if (topLevel?.StorageProvider == null)
            return;

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.Tr("FileDialog.ImportSettings"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Loc.Tr("FileDialog.SettingsFileType")) { Patterns = new[] { "*.json" } }
                }
            });

            if (files.Count == 0)
                return;

            var imported = await _settingsService.ReadFromFileAsync(files[0].Path.LocalPath);
            LoadFrom(imported.App, imported.Ui);
            StatusMessage = Loc.Tr("Status.Settings.SettingsImported");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error importing settings");
            StatusMessage = string.Format(Loc.Tr("Status.Settings.ImportFailed"), ex.Message);
        }
    }

    [RelayCommand]
    private Task BrowseDownloadPathAsync() => PickFolderIntoAsync(
        Loc.Tr("FileDialog.SelectDownloadFolder"),
        path => RemoteDownloadPath = path);

    [RelayCommand]
    private Task BrowseReportsPathAsync() => PickFolderIntoAsync(
        Loc.Tr("FileDialog.SelectReportsFolder"),
        path => ReportsPath = path);

    [RelayCommand]
    private Task BrowseStoragePathAsync() => PickFolderIntoAsync(
        Loc.Tr("FileDialog.SelectStorageFolder"),
        path => StoragePath = path);

    [RelayCommand]
    private Task BrowseAppLogPathAsync() => PickFolderIntoAsync(
        Loc.Tr("FileDialog.SelectAppLogFolder"),
        folder => AppLogPath = Path.Combine(folder, "application.log"));

    [RelayCommand]
    private Task BrowseParserLogPathAsync() => PickFolderIntoAsync(
        Loc.Tr("FileDialog.SelectParserLogFolder"),
        folder => ParserLogPath = Path.Combine(folder, "parser.log"));

    [RelayCommand]
    private Task OpenAppLogFolderAsync() => RevealFileAsync(LogConfiguration.ResolveAppLogFile(AppLogPath));

    [RelayCommand]
    private Task OpenParserLogFolderAsync() => RevealFileAsync(LogConfiguration.ResolveParserLogFile(ParserLogPath));

    [RelayCommand]
    private void ClearAppLogs() => ClearLogs(LogConfiguration.ResolveAppLogFile(AppLogPath), "application");

    [RelayCommand]
    private void ClearParserLogs() => ClearLogs(LogConfiguration.ResolveParserLogFile(ParserLogPath), "parser");

    [RelayCommand]
    private Task OpenRulesFolderAsync() => RevealFileAsync(RulesConfiguration.RulesFilePath);

    [RelayCommand]
    private async Task ImportRulesFileAsync()
    {
        var topLevel = _windowProvider.GetCurrent();
        if (topLevel?.StorageProvider == null)
            return;

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.Tr("FileDialog.ImportRules"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Loc.Tr("FileDialog.RulesFileType")) { Patterns = new[] { "*.json" } }
                }
            });

            if (files.Count == 0)
                return;

            RulesConfiguration.ImportRules(files[0].Path.LocalPath);
            StatusMessage = Loc.Tr("Status.Settings.RulesImported");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error importing rules");
            StatusMessage = string.Format(Loc.Tr("Status.Settings.ImportFailed"), ex.Message);
        }
    }

    private async Task RevealFileAsync(string filePath)
    {
        if (_fileDialogService == null)
            return;

        var revealed = await _fileDialogService.RevealInFileManagerAsync(filePath);
        if (!revealed)
            StatusMessage = Loc.Tr("Status.Settings.FileNotExistYet");
    }

    private void ClearLogs(string logFile, string kind)
    {
        var deleted = LogConfiguration.ClearLogs(logFile);
        StatusMessage = deleted > 0
            ? string.Format(Loc.Tr("Status.Settings.ClearedLogs"), deleted, kind)
            : string.Format(Loc.Tr("Status.Settings.NoLogsToClear"), kind);
        Logger.Info("Cleared {Count} {Kind} log file(s)", deleted, kind);
    }

    private async Task PickFolderIntoAsync(string title, Action<string> assign)
    {
        var topLevel = _windowProvider.GetCurrent();
        if (topLevel?.StorageProvider == null)
            return;

        try
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            if (folders.Count > 0)
                assign(folders[0].Path.LocalPath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error selecting folder");
            StatusMessage = string.Format(Loc.Tr("Status.Settings.Error"), ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, false);
    }

    #endregion

    private void LoadFrom(AppSettings app, UiSectionSettings ui)
    {
        IsClassicSearch = app.IsClassicSearch;
        AllowConcurrentProcessing = app.AllowConcurrentProcessing;
        DeveloperMode = app.DeveloperMode;
        Theme = app.Theme;
        var code = string.IsNullOrWhiteSpace(app.Language) ? "en" : app.Language;
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase))
                           ?? AvailableLanguages.FirstOrDefault();
        RemoteDownloadPath = app.RemoteDownloadPath;
        ReportsPath = app.ReportsPath;
        AppLogPath = app.AppLogPath;
        ParserLogPath = app.ParserLogPath;
        SelectedStorageMode = AvailableStorageModes.FirstOrDefault(o => o.Mode == app.StorageMode)
                              ?? AvailableStorageModes[0];
        StoragePath = app.StoragePath;

        SectionFiles = ui.Files;
        SectionSearch = ui.Search;
        SectionEvents = ui.Events;
        SectionStatistics = ui.Statistics;
        SectionLogs = ui.Logs;
    }

    private UserSettings BuildWorkingSettings() => new()
    {
        App = new AppSettings
        {
            IsClassicSearch = IsClassicSearch,
            AllowConcurrentProcessing = AllowConcurrentProcessing,
            DeveloperMode = DeveloperMode,
            Theme = Theme,
            Language = SelectedLanguage?.Code ?? "en",
            RemoteDownloadPath = RemoteDownloadPath?.Trim() ?? string.Empty,
            ReportsPath = ReportsPath?.Trim() ?? string.Empty,
            AppLogPath = AppLogPath?.Trim() ?? string.Empty,
            ParserLogPath = ParserLogPath?.Trim() ?? string.Empty,
            StorageMode = SelectedStorageMode?.Mode ?? StorageMode.Session,
            StoragePath = StoragePath?.Trim() ?? string.Empty
        },
        Ui = new UiSectionSettings
        {
            Files = SectionFiles,
            Search = SectionSearch,
            Events = SectionEvents,
            Statistics = SectionStatistics,
            Logs = SectionLogs
        }
    };
}

/// <summary>Пункт выпадающего списка режима хранилища: значение enum + локализованная подпись.</summary>
public sealed record StorageModeOption(StorageMode Mode, string Label);
