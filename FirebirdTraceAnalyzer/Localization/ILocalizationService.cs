namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// Сервис локализации интерфейса. Хранит текущий язык и словари переводов, отдаёт строки по ключам
/// (с фолбэком на английский), умеет переключать язык в рантайме. Зеркало по духу для
/// <c>IThemeService</c>: применяется на старте и живёт синглтоном.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Доступные языки (из манифеста Assets/i18n/languages.json).</summary>
    IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    /// <summary>Код текущего языка (например, "en", "ru").</summary>
    string CurrentLanguage { get; }

    /// <summary>Срабатывает после смены языка — источник для живого обновления UI.</summary>
    event EventHandler? LanguageChanged;

    /// <summary>Переключает язык (грузит словарь, ставит культуру, поднимает событие).</summary>
    void SetLanguage(string code);

    /// <summary>Перевод по ключу. Фолбэк: текущий язык → английский → сам ключ.</summary>
    string Tr(string key);
}
