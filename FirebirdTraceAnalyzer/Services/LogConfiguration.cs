using System.Text.Json;
using System.Text.Json.Serialization;
using FirebirdTraceAnalyzer.Models;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Управляет путями к файлам логов: подставляет их в NLog через GlobalDiagnosticsContext
/// (в nlog.config используются ${gdc:item=appLogFile} и ${gdc:item=parserLogFile}),
/// а также умеет очищать логи и резолвить эффективные пути.
/// </summary>
public static class LogConfiguration
{
    public const string AppLogGdcKey = "appLogFile";
    public const string ParserLogGdcKey = "parserLogFile";

    /// <summary>Папка логов по умолчанию: %AppData%/FirebirdTraceAnalyzer/logs (Win) или
    /// ~/Library/Application Support/FirebirdTraceAnalyzer/logs (macOS) — там же, где settings.json.</summary>
    private static string DefaultLogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FirebirdTraceAnalyzer", "logs");

    /// <summary>Путь к логу приложения по умолчанию.</summary>
    public static string DefaultAppLogFile => Path.Combine(DefaultLogDirectory, "application.log");

    /// <summary>Путь к логу парсера по умолчанию.</summary>
    public static string DefaultParserLogFile => Path.Combine(DefaultLogDirectory, "parser.log");

    /// <summary>Эффективный путь к логу приложения (настройка или значение по умолчанию).</summary>
    public static string ResolveAppLogFile(string? configured) =>
        Expand(string.IsNullOrWhiteSpace(configured) ? DefaultAppLogFile : configured);

    /// <summary>Эффективный путь к логу парсера (настройка или значение по умолчанию).</summary>
    public static string ResolveParserLogFile(string? configured) =>
        Expand(string.IsNullOrWhiteSpace(configured) ? DefaultParserLogFile : configured);

    /// <summary>
    /// Прописывает пути логов в NLog. Вызывать до начала логирования (со значениями по умолчанию),
    /// и повторно после загрузки настроек (с пользовательскими путями) — File-таргеты подхватят
    /// новый путь на следующей записи.
    /// </summary>
    public static void Apply(string? appLogPath, string? parserLogPath)
    {
        GlobalDiagnosticsContext.Set(AppLogGdcKey, ResolveAppLogFile(appLogPath));
        GlobalDiagnosticsContext.Set(ParserLogGdcKey, ResolveParserLogFile(parserLogPath));
    }

    /// <summary>
    /// Применяет пути логов, прочитав их напрямую из settings.json (без DI). Вызывается из
    /// Program.Main ДО первой записи — чтобы стартовые логи (в т.ч. парсера) сразу шли в
    /// настроенную папку, а не в каталог по умолчанию. При ошибке — пути по умолчанию.
    /// </summary>
    public static void ApplyFromSettingsFile()
    {
        string? appLogPath = null;
        string? parserLogPath = null;

        try
        {
            var settingsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FirebirdTraceAnalyzer", "settings.json");

            if (File.Exists(settingsFile))
            {
                var json = File.ReadAllText(settingsFile);
                var settings = JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });

                appLogPath = settings?.App?.AppLogPath;
                parserLogPath = settings?.App?.ParserLogPath;
            }
        }
        catch
        {
            // Не удалось прочитать настройки — применим пути по умолчанию.
        }

        Apply(appLogPath, parserLogPath);
    }

    /// <summary>
    /// Удаляет файл лога и его архивы (файлы в той же папке с тем же базовым именем).
    /// Возвращает количество удалённых файлов.
    /// </summary>
    public static int ClearLogs(string logFile)
    {
        var path = Expand(logFile);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return 0;

        var baseName = Path.GetFileNameWithoutExtension(path);
        var deleted = 0;

        // Сам файл + архивы вида application*.log в той же папке.
        foreach (var file in Directory.EnumerateFiles(directory, baseName + "*"))
        {
            try
            {
                File.Delete(file);
                deleted++;
            }
            catch
            {
                // файл может быть временно занят — пропускаем
            }
        }

        return deleted;
    }

    /// <summary>Раскрывает "~" и переменные окружения, возвращает абсолютный путь.</summary>
    private static string Expand(string path)
    {
        var trimmed = path.Trim();

        if (trimmed == "~" || trimmed.StartsWith("~/") || trimmed.StartsWith("~\\"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            trimmed = trimmed.Length <= 1 ? home : Path.Combine(home, trimmed[2..]);
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(trimmed));
    }
}
