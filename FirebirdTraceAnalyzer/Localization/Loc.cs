namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// Provides a static class for localization, allowing translation of strings using a shared Localizer instance.
/// </summary>
public static class Loc
{
    /// <summary>
    /// Translates the specified key using the shared Localizer instance.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <returns>The translated string. If the key is not found, it returns the key itself as a fallback.</returns>
    public static string Tr(string key) => Localizer.Instance.Translate(key);
}
