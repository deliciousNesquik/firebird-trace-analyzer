using System.Text.Json;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Управляет файлами правил парсера в пользовательской папке
/// (%AppData%/FirebirdTraceAnalyzer, рядом с settings.json/профилями/логами).
/// При первом запуске копирует поставляемые с приложением rules.json и rules.schema.json,
/// далее парсер читает их оттуда — чтобы пользователь мог редактировать/заменять правила,
/// не трогая файлы внутри бандла приложения.
/// </summary>
public static class RulesConfiguration
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string RulesFileName = "rules.json";
    private const string SchemaFileName = "rules.schema.json";

    public static string ConfigDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FirebirdTraceAnalyzer");

    public static string RulesFilePath => Path.Combine(ConfigDirectory, RulesFileName);
    public static string SchemaFilePath => Path.Combine(ConfigDirectory, SchemaFileName);

    private static string BundledRulesPath => Path.Combine(AppContext.BaseDirectory, "Configuration", RulesFileName);
    private static string BundledSchemaPath => Path.Combine(AppContext.BaseDirectory, "Configuration", SchemaFileName);

    /// <summary>
    /// Гарантирует наличие правил в пользовательской папке (при первом запуске копирует
    /// поставляемые) и возвращает путь к rules.json для загрузки парсером. При сбое —
    /// фолбэк на поставляемый файл, чтобы приложение всё равно стартовало.
    /// </summary>
    public static string EnsureRulesFile()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            SeedIfMissing(BundledRulesPath, RulesFilePath);
            SeedIfMissing(BundledSchemaPath, SchemaFilePath);

            if (File.Exists(RulesFilePath))
                return RulesFilePath;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Could not prepare user rules file; falling back to bundled rules");
        }

        return BundledRulesPath;
    }

    /// <summary>
    /// Импортирует rules.json из указанного файла в пользовательскую папку (с заменой).
    /// Делает лёгкую проверку структуры, чтобы не подложить заведомо битый файл
    /// (иначе приложение упадёт на валидации правил при следующем старте).
    /// </summary>
    public static void ImportRules(string sourcePath)
    {
        var json = File.ReadAllText(sourcePath);

        using (var document = JsonDocument.Parse(json))
        {
            if (!document.RootElement.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.Number
                || version.GetInt32() != 1)
                throw new InvalidDataException("Unsupported or missing \"schemaVersion\" (expected 1).");

            if (!document.RootElement.TryGetProperty("rules", out var rules)
                || rules.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Missing \"rules\" object.");
        }

        Directory.CreateDirectory(ConfigDirectory);
        File.Copy(sourcePath, RulesFilePath, overwrite: true);

        Logger.Info("Rules imported from {Source}", sourcePath);
    }

    private static void SeedIfMissing(string source, string target)
    {
        if (File.Exists(target) || !File.Exists(source))
            return;

        File.Copy(source, target);
    }
}
