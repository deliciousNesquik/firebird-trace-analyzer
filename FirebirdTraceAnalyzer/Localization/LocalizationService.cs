using System.Globalization;
using System.Text.Json;
using Avalonia.Platform;
using NLog;

namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// Реализация локализации на JSON-словарях из ресурсов приложения
/// (<c>avares://FirebirdTraceAnalyzer/Assets/i18n/{code}.json</c>). Английский всегда загружен как
/// фолбэк. Добавление нового языка = новый JSON-файл + строка в манифесте, без изменений кода.
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

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }
    public string CurrentLanguage { get; private set; } = DefaultCode;
    public event EventHandler? LanguageChanged;

    public LocalizationService()
    {
        AvailableLanguages = LoadManifest();
        _fallback = LoadDictionary(DefaultCode);
        _current = _fallback;

        // Подключаем источник привязок {loc:Tr} к этому сервису.
        Localizer.Instance.Attach(this);
    }

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
