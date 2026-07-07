namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// Статический помощник перевода для использования из C# (ViewModels, сервисы), где неудобно
/// внедрять <see cref="ILocalizationService"/>. Делегирует в тот же синглтон <see cref="Localizer"/>,
/// что и XAML-расширение <c>{loc:Tr}</c>, поэтому язык и словари общие. Фолбэк — сам ключ.
/// </summary>
public static class Loc
{
    /// <summary>Перевод по ключу на текущем языке (фолбэк: английский → сам ключ).</summary>
    public static string Tr(string key) => Localizer.Instance.Translate(key);
}
