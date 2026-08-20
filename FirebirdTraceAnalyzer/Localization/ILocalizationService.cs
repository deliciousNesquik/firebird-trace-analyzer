namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// Represents a service for managing localization and translations in the application.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Available languages. The first one is the default language (English).
    /// </summary>
    /// <remarks>See files in the i18n directory.</remarks>
    IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    /// <summary>
    /// Current language code (e.g. "en", "ru"). The default is "en".
    /// </summary>
    string CurrentLanguage { get; }

    /// <summary>
    /// Occurs after the language is changed — source for live UI updates.
    /// </summary>
    event EventHandler? LanguageChanged;

    /// <summary>
    /// Switches the language (loads the dictionary, sets the culture, raises the event).
    /// </summary>
    /// <param name="code">The language code to switch to.</param>
    void SetLanguage(string code);

    /// <summary>
    /// Translates a key into the current language. Falls back to the default language, then to the key itself.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <returns>The translated string.</returns>
    string Tr(string key);
}
