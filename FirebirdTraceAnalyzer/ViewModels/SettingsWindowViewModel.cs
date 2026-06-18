using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Models;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel окна настроек. Работает с рабочей копией значений: правки применяются к приложению
/// только по кнопке «Save». Reset/Import загружают значения в рабочую копию (тоже до Save),
/// Export пишет текущую рабочую копию в файл.
/// </summary>
public partial class SettingsWindowViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ISettingsService _settingsService;
    private readonly IWindowProvider _windowProvider;
    private readonly IThemeService _themeService;

    #region General

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRegexSearch))]
    private bool _isClassicSearch;

    /// <summary>Производное состояние для правой кнопки сегмента (обратное к Classic).</summary>
    public bool IsRegexSearch => !IsClassicSearch;

    [ObservableProperty] private AppTheme _theme = AppTheme.Auto;

    public IReadOnlyList<AppTheme> AvailableThemes { get; } = Enum.GetValues<AppTheme>();

    #endregion

    #region Remote

    [ObservableProperty] private string _remoteDownloadPath = string.Empty;

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
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Конструктор только для XAML-дизайнера.</summary>
    public SettingsWindowViewModel()
    {
        _settingsService = null!;
        _windowProvider = null!;
        _themeService = null!;
    }

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        IWindowProvider windowProvider,
        IThemeService themeService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

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

        var ui = _settingsService.Ui;
        ui.Files = SectionFiles;
        ui.Search = SectionSearch;
        ui.Events = SectionEvents;
        ui.Statistics = SectionStatistics;
        ui.Logs = SectionLogs;

        _settingsService.Save();

        // Применяем тему сразу, чтобы изменение вступило в силу без перезапуска.
        _themeService.Apply(Theme);

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
    private async Task BrowseDownloadPathAsync()
    {
        var topLevel = _windowProvider.GetCurrent();
        if (topLevel?.StorageProvider == null)
            return;

        try
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Download Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
                RemoteDownloadPath = folders[0].Path.LocalPath;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error selecting download folder");
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
            RemoteDownloadPath = RemoteDownloadPath?.Trim() ?? string.Empty
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
