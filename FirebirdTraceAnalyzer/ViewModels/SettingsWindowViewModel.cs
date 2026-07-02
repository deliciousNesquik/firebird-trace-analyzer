using System.IO;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Window;
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
    private readonly IFileDialogService? _fileDialogService;

    #region General

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRegexSearch))]
    private bool _isClassicSearch;

    /// <summary>Производное состояние для правой кнопки сегмента (обратное к Classic).</summary>
    public bool IsRegexSearch => !IsClassicSearch;

    [ObservableProperty] private AppTheme _theme = AppTheme.Auto;

    public IReadOnlyList<AppTheme> AvailableThemes { get; } = Enum.GetValues<AppTheme>();

    #endregion

    #region Paths

    [ObservableProperty] private string _remoteDownloadPath = string.Empty;

    [ObservableProperty] private string _reportsPath = string.Empty;

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
        _fileDialogService = null;
    }

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        IWindowProvider windowProvider,
        IThemeService themeService,
        IFileDialogService fileDialogService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));

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
        app.Theme = Theme;
        app.RemoteDownloadPath = RemoteDownloadPath?.Trim() ?? string.Empty;
        app.ReportsPath = ReportsPath?.Trim() ?? string.Empty;
        app.AppLogPath = AppLogPath?.Trim() ?? string.Empty;
        app.ParserLogPath = ParserLogPath?.Trim() ?? string.Empty;

        var ui = _settingsService.Ui;
        ui.Files = SectionFiles;
        ui.Search = SectionSearch;
        ui.Events = SectionEvents;
        ui.Statistics = SectionStatistics;
        ui.Logs = SectionLogs;

        _settingsService.Save();

        // Применяем тему сразу, чтобы изменение вступило в силу без перезапуска.
        _themeService.Apply(Theme);

        // Применяем пути логов: File-таргеты NLog переключатся на новый путь со следующей записи.
        LogConfiguration.Apply(app.AppLogPath, app.ParserLogPath);

        Logger.Info("Settings saved from settings window");
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var defaults = _settingsService.GetDefaults();
        LoadFrom(defaults.App, defaults.Ui);
        StatusMessage = "Restored factory defaults. Press Save to apply.";
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
                Title = "Export Settings",
                SuggestedFileName = "firebird-trace-settings.json",
                DefaultExtension = "json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Settings file") { Patterns = new[] { "*.json" } }
                }
            });

            if (file == null)
                return;

            await _settingsService.ExportAsync(file.Path.LocalPath, BuildWorkingSettings());
            StatusMessage = $"Exported to {file.Name}";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting settings");
            StatusMessage = $"Export failed: {ex.Message}";
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
                Title = "Import Settings",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Settings file") { Patterns = new[] { "*.json" } }
                }
            });

            if (files.Count == 0)
                return;

            var imported = await _settingsService.ReadFromFileAsync(files[0].Path.LocalPath);
            LoadFrom(imported.App, imported.Ui);
            StatusMessage = "Settings imported. Review and press Save to apply.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error importing settings");
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task BrowseDownloadPathAsync() => PickFolderIntoAsync(
        "Select Download Folder",
        path => RemoteDownloadPath = path);

    [RelayCommand]
    private Task BrowseReportsPathAsync() => PickFolderIntoAsync(
        "Select Reports Folder",
        path => ReportsPath = path);

    [RelayCommand]
    private Task BrowseAppLogPathAsync() => PickFolderIntoAsync(
        "Select Application Log Folder",
        folder => AppLogPath = Path.Combine(folder, "application.log"));

    [RelayCommand]
    private Task BrowseParserLogPathAsync() => PickFolderIntoAsync(
        "Select Parser Log Folder",
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
                Title = "Import rules.json",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Rules file") { Patterns = new[] { "*.json" } }
                }
            });

            if (files.Count == 0)
                return;

            RulesConfiguration.ImportRules(files[0].Path.LocalPath);
            StatusMessage = "Rules imported. Restart the application to apply.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error importing rules");
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    private async Task RevealFileAsync(string filePath)
    {
        if (_fileDialogService == null)
            return;

        var revealed = await _fileDialogService.RevealInFileManagerAsync(filePath);
        if (!revealed)
            StatusMessage = "File does not exist yet";
    }

    private void ClearLogs(string logFile, string kind)
    {
        var deleted = LogConfiguration.ClearLogs(logFile);
        StatusMessage = deleted > 0
            ? $"Cleared {deleted} {kind} log file(s)"
            : $"No {kind} log files to clear";
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
            StatusMessage = $"Error: {ex.Message}";
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
        Theme = app.Theme;
        RemoteDownloadPath = app.RemoteDownloadPath;
        ReportsPath = app.ReportsPath;
        AppLogPath = app.AppLogPath;
        ParserLogPath = app.ParserLogPath;

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
            Theme = Theme,
            RemoteDownloadPath = RemoteDownloadPath?.Trim() ?? string.Empty,
            ReportsPath = ReportsPath?.Trim() ?? string.Empty,
            AppLogPath = AppLogPath?.Trim() ?? string.Empty,
            ParserLogPath = ParserLogPath?.Trim() ?? string.Empty
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
