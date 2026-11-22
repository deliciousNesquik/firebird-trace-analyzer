using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services;
using FirebirdTraceAnalyzer.Services.Shortcuts;
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
    private readonly IToastService? _toasts;
    private readonly IShortcutRegistry? _shortcutRegistry;
    private readonly ShortcutFileService? _shortcutFile;

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

    /// <summary>(Advanced) Режим статистики парсера: показывает диагностический инструмент.</summary>
    [ObservableProperty] private bool _statisticsMode;
    
    /// <summary>(Advanced) Режим инспектора событий: показывает диагностический инструмент.</summary>
    [ObservableProperty] private bool _inspectorMode;

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
        IFileDialogService fileDialogService,
        IToastService? toasts = null,
        IShortcutRegistry? shortcutRegistry = null,
        ShortcutFileService? shortcutFile = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _toasts = toasts;
        _shortcutRegistry = shortcutRegistry;
        _shortcutFile = shortcutFile;

        AvailableLanguages = _localizationService.AvailableLanguages;

        LoadFrom(_settingsService.App, _settingsService.Ui);
        RebuildShortcuts();
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
        app.StatisticsMode = StatisticsMode;
        app.InspectorMode = InspectorMode;
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
            var msg = string.Format(Loc.Tr("Status.Settings.ExportedTo"), file.Name);
            StatusMessage = msg;
            _toasts?.Success(msg);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting settings");
            var msg = string.Format(Loc.Tr("Status.Settings.ExportFailed"), ex.Message);
            StatusMessage = msg;
            _toasts?.Error(msg);
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
            var msg = Loc.Tr("Status.Settings.SettingsImported");
            StatusMessage = msg;
            _toasts?.Success(msg);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error importing settings");
            var msg = string.Format(Loc.Tr("Status.Settings.ImportFailed"), ex.Message);
            StatusMessage = msg;
            _toasts?.Error(msg);
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
            var msg = Loc.Tr("Status.Settings.RulesImported");
            StatusMessage = msg;
            _toasts?.Success(msg);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error importing rules");
            var msg = string.Format(Loc.Tr("Status.Settings.ImportFailed"), ex.Message);
            StatusMessage = msg;
            _toasts?.Error(msg);
        }
    }

    private async Task RevealFileAsync(string filePath)
    {
        if (_fileDialogService == null)
            return;

        var revealed = await _fileDialogService.RevealInFileManagerAsync(filePath);
        if (!revealed)
        {
            var msg = Loc.Tr("Status.Settings.FileNotExistYet");
            StatusMessage = msg;
            _toasts?.Warning(msg);
        }
    }

    private void ClearLogs(string logFile, string kind)
    {
        var deleted = LogConfiguration.ClearLogs(logFile);
        if (deleted > 0)
        {
            StatusMessage = string.Format(Loc.Tr("Status.Settings.ClearedLogs"), deleted, kind);
        }
        else
        {
            // «Нечего чистить» — предупреждение (нет файла логов или он пуст).
            var msg = string.Format(Loc.Tr("Status.Settings.NoLogsToClear"), kind);
            StatusMessage = msg;
            _toasts?.Warning(msg);
        }
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

    #region Shortcuts

    /// <summary>Строки страницы горячих клавиш: заголовки категорий + строки действий.</summary>
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<object> _shortcutEntries = [];

    /// <summary>Секция скрыта, когда реестр недоступен (designer-режим).</summary>
    public bool HasShortcutsSection => _shortcutRegistry is not null;

    /// <summary>Строка, находящаяся в режиме записи сочетания (или null).</summary>
    private ShortcutRowViewModel? RecordingRow() =>
        ShortcutEntries.OfType<ShortcutRowViewModel>().FirstOrDefault(r => r.IsRecording);

    /// <summary>Пересобирает список: категории в фиксированном порядке, действия — по имени.</summary>
    private void RebuildShortcuts()
    {
        ShortcutEntries.Clear();

        if (_shortcutRegistry is null)
            return;

        var categoryOrder = new[]
        {
            ShortcutCatalog.CategoryFiles,
            ShortcutCatalog.CategoryLayout,
            ShortcutCatalog.CategorySearch,
            ShortcutCatalog.CategoryApp,
            ShortcutCatalog.CategoryReports,
        };

        foreach (var category in categoryOrder)
        {
            var actions = _shortcutRegistry.Actions
                .Where(a => a.CategoryLocKey == category)
                .OrderBy(a => a.DisplayName, StringComparer.CurrentCulture)
                .ToList();

            if (actions.Count == 0)
                continue;

            ShortcutEntries.Add(new ShortcutHeaderViewModel(Loc.Tr(category)));

            foreach (var action in actions)
                ShortcutEntries.Add(CreateRow(action));
        }
    }

    private ShortcutRowViewModel CreateRow(ShortcutAction action) =>
        new(action, GestureDisplayOf(action.Id));

    /// <summary>Платформозависимый текст жеста («Shift+Cmd+O» / «Ctrl+Shift+O») или «—».</summary>
    private string GestureDisplayOf(string actionId)
    {
        if (_shortcutRegistry?.ResolveSpec(actionId) is not { } spec)
            return Loc.Tr("Shortcuts.None");

        // «p» — платформенный формат: ⌘⇧O на macOS, Ctrl+Shift+O на Win/Linux (как в меню).
        return GestureParser.ToKeyGesture(spec, _shortcutRegistry.Platform)
                           ?.ToString("p", System.Globalization.CultureInfo.InvariantCulture)
               ?? Loc.Tr("Shortcuts.None");
    }

    [RelayCommand]
    private void BeginRecord(ShortcutRowViewModel? row)
    {
        if (row is null || !row.IsRebindable)
            return;

        CancelRecord(); // одновременно записывается не более одного сочетания

        // На время записи снимаем функциональные биндинги: иначе нажатие жеста, занятого
        // другим действием, ВЫПОЛНИЛО БЫ это действие (KeyBinding'и окна проходят раньше
        // routed KeyDown, нативные ускорители macOS — на уровне ОС), и перехват не сработал.
        Shortcut.SetSuspended(true);
        row.IsRecording = true;
    }

    [RelayCommand]
    private void CancelRecord()
    {
        Shortcut.SetSuspended(false);

        foreach (var r in ShortcutEntries.OfType<ShortcutRowViewModel>())
        {
            r.IsRecording = false;
            r.PendingDisplay = null;
            r.PendingSpec = null;
            r.HintText = null;
            r.HasConflict = false;
            r.ConflictTitle = null;
        }
    }

    /// <summary>
    /// Перехват клавиш во время записи. Вызывается из code-behind по туннелирующему KeyDown
    /// (чтобы получать ВСЕ клавиши до TextBox'ов). Возвращает true — клавиша обработана.
    /// </summary>
    public bool HandleRecordKey(Key key, KeyModifiers keyModifiers)
    {
        var row = RecordingRow();

        if (row is null)
            return false;

        // Голые модификаторы: нажимаются, но не записываются.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return true;

        if (key == Key.Escape && keyModifiers == KeyModifiers.None)
        {
            CancelRecord();
            return true;
        }

        if (key == Key.Enter && keyModifiers == KeyModifiers.None)
        {
            ConfirmRecord();
            return true;
        }

        // Логическая запись: на платформе Ctrl и есть Primary (Windows/Linux), Meta — на macOS.
        // Так записанное на одной машине сочетание портабельно в shortcuts.json на другую.
        var isMac = _shortcutRegistry?.Platform == ShortcutPlatform.MacOS;

        var logical = LogicalModifiers.None;
        if ((keyModifiers & KeyModifiers.Meta) != 0) logical |= LogicalModifiers.Primary;
        if ((keyModifiers & KeyModifiers.Control) != 0)
            logical |= isMac ? LogicalModifiers.Control : LogicalModifiers.Primary;
        if ((keyModifiers & KeyModifiers.Shift) != 0) logical |= LogicalModifiers.Shift;
        if ((keyModifiers & KeyModifiers.Alt) != 0) logical |= LogicalModifiers.Alt;

        var spec = GestureParser.Normalize(new GestureSpec(key, logical));

        // Голая клавиша перехватывает печать — разрешаем только функциональные F1..F24.
        var isFunctionKey = key is >= Key.F1 and <= Key.F24;
        if (spec.Modifiers == LogicalModifiers.None && !isFunctionKey)
        {
            row.HintText = Loc.Tr("Shortcuts.NeedModifier");
            return true;
        }

        row.PendingSpec = spec;
        row.PendingDisplay = GestureParser.ToKeyGesture(spec, _shortcutRegistry!.Platform)?.ToString();
        row.HintText = null;

        // Превентивная конфлюкт-детекция: показываем, чей это жест. Применение — по Enter/«Занять».
        var conflict = _shortcutRegistry.FindConflicting(spec.ToString(), row.ActionId);
        row.HasConflict = conflict is not null;
        row.ConflictTitle = conflict?.DisplayName;

        return true;
    }

    [RelayCommand]
    private void ConfirmRecord()
    {
        var row = RecordingRow();

        if (row is null)
            return;

        if (row.PendingSpec is null)
        {
            CancelRecord();
            return;
        }

        var conflict = _shortcutRegistry!.FindConflicting(row.PendingSpec.ToString(), row.ActionId);

        if (conflict is not null)
        {
            // Жест занят — ждём явного решения пользователя («Занять» / Esc).
            row.HasConflict = true;
            row.ConflictTitle = conflict.DisplayName;
            return;
        }

        ApplyPending(row);
    }

    [RelayCommand]
    private void TakeOverConflict()
    {
        var row = RecordingRow();

        if (row?.PendingSpec is null)
            return;

        var conflict = _shortcutRegistry!.FindConflicting(row.PendingSpec.ToString(), row.ActionId);

        if (conflict is not null && !_shortcutRegistry.SetOverride(conflict.Id, null))
        {
            // Снять прежнего владельца не вышло (системное действие, IsRebindable=false):
            // применять нельзя — получились бы два живых биндинга на один жест.
            row.HintText = Loc.Tr("Shortcuts.SystemLocked");
            return;
        }

        ApplyPending(row);
    }

    private void ApplyPending(ShortcutRowViewModel row)
    {
        // Rebindable защищает и саму цель записи — но проверяем результат явно: тихий отказ
        // здесь означал бы дублирующий биндинг.
        if (!_shortcutRegistry!.SetOverride(row.ActionId, row.PendingSpec?.ToString()))
        {
            row.HintText = Loc.Tr("Shortcuts.SystemLocked");
            return;
        }

        _shortcutFile?.Save(_shortcutRegistry.Overrides); // применяется мгновенно: applier уже обновил меню по Changed
        ExitRecording(row);
        RefreshGestureDisplays();
    }

    [RelayCommand]
    private void ResetRow(ShortcutRowViewModel? row)
    {
        if (row is null)
            return;

        _shortcutRegistry?.ClearOverride(row.ActionId);
        _shortcutFile?.Save(_shortcutRegistry!.Overrides);
        RefreshGestureDisplays();
    }

    [RelayCommand]
    private void DisableRow(ShortcutRowViewModel? row)
    {
        if (row is null)
            return;

        _shortcutRegistry?.SetOverride(row.ActionId, null);
        _shortcutFile?.Save(_shortcutRegistry!.Overrides);
        RefreshGestureDisplays();
    }

    [RelayCommand]
    private void ResetAllShortcuts()
    {
        if (_shortcutRegistry is null)
            return;

        // Сброс всех клавиш = удаление файла: отсутствие shortcuts.json — валидное «всё по дефолту».
        _shortcutFile?.ResetFile();
        _shortcutRegistry.ResetAll();
        RefreshGestureDisplays();
    }

    private void RefreshGestureDisplays()
    {
        foreach (var r in ShortcutEntries.OfType<ShortcutRowViewModel>())
        {
            r.GestureDisplay = GestureDisplayOf(r.ActionId);
            ExitRecording(r);
        }

        // Если записи уже не было — no-op.
        Shortcut.SetSuspended(false);
    }

    private static void ExitRecording(ShortcutRowViewModel row)
    {
        row.IsRecording = false;
        row.PendingDisplay = null;
        row.PendingSpec = null;
        row.HintText = null;
        row.HasConflict = false;
        row.ConflictTitle = null;
    }

    #endregion

    private void LoadFrom(AppSettings app, UiSectionSettings ui)
    {
        IsClassicSearch = app.IsClassicSearch;
        StatisticsMode = app.StatisticsMode;
        InspectorMode = app.InspectorMode;
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
            StatisticsMode = StatisticsMode,
            InspectorMode = InspectorMode,
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
