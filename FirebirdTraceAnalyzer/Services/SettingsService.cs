using System.Text.Json;
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
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;

    public AppSettings App { get; private set; }
    public UiSectionSettings Ui { get; private set; }

    public SettingsService(IOptions<AppSettings> defaultApp, IOptions<UiSectionSettings> defaultUi)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsFilePath = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "settings.json");

        // Клонируем значения из IOptions, чтобы не мутировать общий singleton-экземпляр опций.
        App = Clone(defaultApp.Value);
        Ui = Clone(defaultUi.Value);

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

            App = loaded.App ?? App;
            Ui = loaded.Ui ?? Ui;

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

    private static AppSettings Clone(AppSettings source) => new()
    {
        IsClassicSearch = source.IsClassicSearch,
        Theme = source.Theme,
        RemoteDownloadPath = source.RemoteDownloadPath
    };

    private static UiSectionSettings Clone(UiSectionSettings source) => new()
    {
        Files = source.Files,
        Search = source.Search,
        Events = source.Events,
        Statistics = source.Statistics,
        Logs = source.Logs
    };
}
