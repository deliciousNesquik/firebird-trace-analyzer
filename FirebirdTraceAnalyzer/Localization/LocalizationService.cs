using System.Globalization;
using System.Text.Json;
using Avalonia.Platform;
using NLog;

namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// JSON-dictionary implementation of <see cref="ILocalizationService"/> that loads translations from
/// application resources (<c>avares://FirebirdTraceAnalyzer/Assets/i18n/{code}.json</c>). English is
/// always loaded as the fallback. Adding a new language means a new JSON file plus a manifest entry,
/// with no code changes.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string DefaultCode = "en";
    private const string BaseUri = "avares://FirebirdTraceAnalyzer/Assets/i18n/";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Кэш загруженных словарей по коду языка.
    private readonly Dictionary<string, Dictionary<string, string>> _dicts = new(StringComparer.OrdinalIgnoreCase);
    // Чтобы не спамить лог одним и тем же отсутствующим ключом.
    private readonly HashSet<string> _missReported = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _fallback;
    private Dictionary<string, string> _current;

    /// <inheritdoc />
    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    /// <inheritdoc />
    public string CurrentLanguage { get; private set; } = DefaultCode;

    /// <inheritdoc />
    public event EventHandler? LanguageChanged;

    /// <summary>
    /// Loads the language manifest and the English fallback dictionary, then attaches the shared
    /// <see cref="Localizer"/> so that <c>{loc:Tr}</c> bindings resolve against this service.
    /// </summary>
    public LocalizationService()
    {
        AvailableLanguages = LoadManifest();
        _fallback = LoadDictionary(DefaultCode);
        _current = _fallback;

        // Подключаем источник привязок {loc:Tr} к этому сервису.
        Localizer.Instance.Attach(this);
    }

    /// <inheritdoc />
    public void SetLanguage(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            code = DefaultCode;

        _current = LoadDictionary(code);
        CurrentLanguage = code;

        // Культура — для числовых/датовых конвертеров и экспортёров, которые используют CultureInfo.
        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }
        catch (CultureNotFoundException)
        {
            // Код языка не соответствует .NET-культуре — не критично, текст берём из словаря.
        }

        Logger.Info("Language applied: {Code}", code);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public string Tr(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (_current.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;

        if (_fallback.TryGetValue(key, out var fallback) && !string.IsNullOrEmpty(fallback))
            return fallback;

        if (_missReported.Add(key))
            Logger.Warn("Missing translation key: {Key}", key);

        return key;
    }

    /// <summary>
    /// Reads the <c>languages.json</c> manifest listing the available languages. Falls back to a
    /// single English entry when the manifest is missing, empty, or fails to parse.
    /// </summary>
    /// <returns>The available languages, English first.</returns>
    private IReadOnlyList<LanguageOption> LoadManifest()
    {
        try
        {
            var uri = new Uri(BaseUri + "languages.json");
            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                var list = JsonSerializer.Deserialize<List<LanguageOption>>(stream, JsonOpts);
                if (list is { Count: > 0 })
                    return list;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load i18n languages manifest");
        }

        return new[] { new LanguageOption(DefaultCode, "English") };
    }

    /// <summary>
    /// Loads and caches the translation dictionary for the given language code from resources. Returns
    /// an empty dictionary when the file is missing or fails to parse (callers fall back to English).
    /// </summary>
    /// <param name="code">The language code whose dictionary to load (e.g. "en", "ru").</param>
    /// <returns>The key/value translation dictionary for the language.</returns>
    private Dictionary<string, string> LoadDictionary(string code)
    {
        if (_dicts.TryGetValue(code, out var cached))
            return cached;

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var uri = new Uri(BaseUri + code + ".json");
            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream, JsonOpts) ?? dict;
            }
            else
            {
                Logger.Warn("Translation file not found for '{Code}'", code);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load translation file for '{Code}'", code);
        }

        _dicts[code] = dict;
        return dict;
    }
}
