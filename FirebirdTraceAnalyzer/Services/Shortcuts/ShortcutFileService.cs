using System.Text.Json;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>
/// Хранит пользовательские оверрайды горячих клавиш в %AppData%/FirebirdTraceAnalyzer/shortcuts.json.
/// Файл содержит ТОЛЬКО оверрайды (отсутствие записи = дефолт; значение null = действие
/// осознанно отключено) и создаётся при первой смене — отсутствие файла означает «всё по
/// дефолту», поэтому сида, в отличие от rules.json, нет. Запись атомарная (tmp + move),
/// порченый файл бэкапится в .bak и молча откатывает на дефолты — та же философия, что и у
/// SettingsService. Применение правок из файла — при старте приложения (watcher сознательно
/// не нужен: единственный писатель — UI настроек, кнопка «Перечитать» может быть добавлена
/// позже поверх того же пайплайна).
/// </summary>
public sealed class ShortcutFileService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // Файл для ручного редактирования — ключи в camelCase: "version", "shortcuts".
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    /// <param name="directory">Шов для тестов; в проде null → %AppData%/FirebirdTraceAnalyzer.</param>
    public ShortcutFileService(string? directory = null)
    {
        var baseDir = directory
                      ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                          "FirebirdTraceAnalyzer");
        _filePath = Path.Combine(baseDir, "shortcuts.json");
    }

    public string FilePath => _filePath;

    /// <summary>Загружает оверрайды. Отсутствие файла → пустой словарь без предупреждений.</summary>
    public ShortcutFileData Load()
    {
        if (!File.Exists(_filePath))
            return ShortcutFileData.Empty;

        try
        {
            var json = File.ReadAllText(_filePath);
            ShortcutFileModel? model;

            try
            {
                model = JsonSerializer.Deserialize<ShortcutFileModel>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                TryBackup();
                Logger.Warn(ex, "Corrupt shortcuts file, defaults apply (backed up to .bak)");
                return new ShortcutFileData(
                    new Dictionary<string, string?>(),
                    [$"Corrupt shortcuts.json: {ex.Message}"]);
            }

            if (model is null)
                return ShortcutFileData.Empty;

            if (model.Version != CurrentVersion)
            {
                // Как с rules.json: несовместимая версия файла не применяется молча.
                TryBackup();
                Logger.Warn("Shortcuts file version {Actual} != {Expected}, defaults apply (backed up)",
                    model.Version, CurrentVersion);
                return new ShortcutFileData(
                    new Dictionary<string, string?>(),
                    [$"Unsupported shortcuts.json version {model.Version}"]);
            }

            return new ShortcutFileData(model.Shortcuts ?? [], []);
        }
        catch (Exception ex)
        {
            // Файл нечитаем (права/блокировка): не валим старт, дефолты применяются.
            Logger.Warn(ex, "Failed to read shortcuts file, defaults apply");
            return new ShortcutFileData(
                new Dictionary<string, string?>(),
                [$"Unreadable shortcuts.json: {ex.Message}"]);
        }
    }

    /// <summary>Атомарная запись: tmp + move поверх, как в SettingsService.Save.</summary>
    public void Save(IReadOnlyDictionary<string, string?> overrides)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var model = new ShortcutFileModel
            {
                Version = CurrentVersion,
                Shortcuts = new Dictionary<string, string?>(overrides, StringComparer.Ordinal),
            };

            var json = JsonSerializer.Serialize(model, JsonOptions);
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, overwrite: true);

            Logger.Debug("Shortcuts saved to {Path}", _filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save shortcuts file");
        }
    }

    /// <summary>Сброс всех клавиш = удаление файла; отсутствие файла после этого — валидное состояние.</summary>
    public void ResetFile()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to delete shortcuts file");
        }
    }

    private void TryBackup()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Copy(_filePath, _filePath + ".bak", overwrite: true);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to back up corrupt shortcuts file");
        }
    }

    private sealed class ShortcutFileModel
    {
        public int Version { get; set; }

        public Dictionary<string, string?>? Shortcuts { get; set; }
    }
}

/// <summary>Результат загрузки shortcuts.json: оверрайды + предупреждения толерантного парсинга.</summary>
public sealed record ShortcutFileData(
    IReadOnlyDictionary<string, string?> Overrides,
    IReadOnlyList<string> Warnings)
{
    public static readonly ShortcutFileData Empty =
        new(new Dictionary<string, string?>(), []);
}
