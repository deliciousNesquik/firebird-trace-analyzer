using System.Text.Json;
using System.Text.Json.Serialization;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;
using Microsoft.Extensions.Options;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Загружает и сохраняет пользовательские настройки в %AppData%/FirebirdTraceAnalyzer/settings.json.
/// Значения по умолчанию приходят из поставляемого appsettings.json (через IOptions); пользовательский
/// файл переопределяет их и хранит изменения, которые делает пользователь в рантайме.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // Чтобы тема писалась читаемо ("Auto"/"Light"/"Dark"), а не числом.
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsFilePath;

    // Заводские значения по умолчанию (из appsettings.json) — для кнопки «Сброс».
    private readonly UserSettings _defaults;

    // App и Ui — стабильные экземпляры на всё время жизни сервиса: ссылки на них держит
    // MainWindowViewModel, поэтому при Reset/Import мы копируем поля внутрь, а не заменяем объект.
    public AppSettings App { get; }
    public UiSectionSettings Ui { get; }

    public SettingsService(IOptions<AppSettings> defaultApp, IOptions<UiSectionSettings> defaultUi)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsFilePath = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "settings.json");

        _defaults = new UserSettings
        {
            App = CloneApp(defaultApp.Value),
            Ui = CloneUi(defaultUi.Value)
        };

        App = CloneApp(defaultApp.Value);
        Ui = CloneUi(defaultUi.Value);

        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                Logger.Info("No user settings file found, using defaults from appsettings.json");
                return;
            }

            var json = File.ReadAllText(_settingsFilePath);
            var loaded = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);

            if (loaded == null)
                return;

            ApplyInto(loaded);

            Logger.Info("User settings loaded from {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load user settings, falling back to defaults");
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(new UserSettings { App = App, Ui = Ui }, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);

            Logger.Debug("User settings saved to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save user settings");
        }
    }

    public UserSettings GetDefaults() => new()
    {
        App = CloneApp(_defaults.App),
        Ui = CloneUi(_defaults.Ui)
    };

    public async Task ExportAsync(string path, UserSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(path, json);
        Logger.Info("Settings exported to {Path}", path);
    }

    public async Task<UserSettings> ReadFromFileAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);

        var loaded = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions)
                     ?? throw new InvalidDataException("Settings file is empty or has an invalid format.");

        // Гарантируем непустые секции, чтобы вызывающий код не падал на null.
        loaded.App ??= CloneApp(_defaults.App);
        loaded.Ui ??= CloneUi(_defaults.Ui);

        return loaded;
    }

    public string GetRemoteDownloadDirectory()
    {
        if (!string.IsNullOrWhiteSpace(App.RemoteDownloadPath))
            return ExpandPath(App.RemoteDownloadPath);

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "RemoteDownloads");
    }

    /// <summary>
    /// Раскрывает "~" в домашний каталог пользователя (.NET сам этого не делает — иначе
    /// рядом с приложением создаётся буквальная папка "~"). Также раскрывает переменные
    /// окружения вида %VAR% / $VAR.
    /// </summary>
    private static string ExpandPath(string path)
    {
        var trimmed = path.Trim();

        if (trimmed == "~" || trimmed.StartsWith("~/") || trimmed.StartsWith("~\\"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            trimmed = trimmed.Length <= 1
                ? home
                : Path.Combine(home, trimmed[2..]);
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(trimmed));
    }

    /// <summary>Копирует значения из <paramref name="source"/> в стабильные App/Ui.</summary>
    private void ApplyInto(UserSettings source)
    {
        if (source.App != null)
            CopyApp(source.App, App);

        if (source.Ui != null)
            CopyUi(source.Ui, Ui);
    }

    private static void CopyApp(AppSettings source, AppSettings target)
    {
        target.IsClassicSearch = source.IsClassicSearch;
        target.Theme = source.Theme;
        target.RemoteDownloadPath = source.RemoteDownloadPath;
    }

    private static void CopyUi(UiSectionSettings source, UiSectionSettings target)
    {
        target.Files = source.Files;
        target.Search = source.Search;
        target.Events = source.Events;
        target.Statistics = source.Statistics;
        target.Logs = source.Logs;
    }

    private static AppSettings CloneApp(AppSettings source)
    {
        var copy = new AppSettings();
        CopyApp(source, copy);
        return copy;
    }

    private static UiSectionSettings CloneUi(UiSectionSettings source)
    {
        var copy = new UiSectionSettings();
        CopyUi(source, copy);
        return copy;
    }
}
