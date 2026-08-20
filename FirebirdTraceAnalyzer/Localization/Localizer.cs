using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// Binding source for the <see cref="TrExtension"/> markup extension. A notifying singleton: on every
/// language change it increments <see cref="Generation"/>, which makes all <c>{loc:Tr}</c> bindings
/// re-evaluate their text through <see cref="TrConverter"/>. This updates the text in already-open
/// windows live — no restart and no per-line code in the views, mirroring how themes refresh brushes.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    /// <summary>The shared singleton instance used by <see cref="TrExtension"/> and <see cref="Loc"/>.</summary>
    public static Localizer Instance { get; } = new();

    /// <summary>Converter for <see cref="TrExtension"/>: the translation key arrives via ConverterParameter.</summary>
    public static readonly IValueConverter TrConverter = new TrValueConverter();

    private ILocalizationService? _service;

    private Localizer() { }

    /// <summary>Bumped on every language change — the trigger that re-evaluates <c>{loc:Tr}</c> bindings.</summary>
    public int Generation { get; private set; }

    /// <summary>Raised when <see cref="Generation"/> changes, driving live re-evaluation of bindings.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Attaches the localization service and subscribes to its language-change event. Detaches from a
    /// previously attached service first, so re-attaching does not leak subscriptions.
    /// </summary>
    /// <param name="service">The localization service to resolve keys and observe for language changes.</param>
    public void Attach(ILocalizationService service)
    {
        if (_service != null)
            _service.LanguageChanged -= OnLanguageChanged;

        _service = service;
        _service.LanguageChanged += OnLanguageChanged;
        Invalidate();
    }

    /// <summary>Translates a key; returns the key itself when no service is attached yet.</summary>
    /// <param name="key">The translation key.</param>
    /// <returns>The translated string, or <paramref name="key"/> as a fallback.</returns>
    public string Translate(string key) => _service?.Tr(key) ?? key;

    private void OnLanguageChanged(object? sender, EventArgs e) => Invalidate();

    /// <summary>Increments <see cref="Generation"/> and notifies bindings so they re-translate.</summary>
    private void Invalidate()
    {
        Generation++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Generation)));
    }

    /// <summary>
    /// Ignores the trigger value (<see cref="Generation"/>) and translates the key passed via
    /// ConverterParameter. Kept private and nested because it is internal machinery of live
    /// localization, not a reusable value converter.
    /// </summary>
    private sealed class TrValueConverter : IValueConverter
    {
        /// <summary>Translates the ConverterParameter key; ignores <paramref name="value"/>.</summary>
        /// <param name="value">The binding value (the <see cref="Generation"/> trigger); unused.</param>
        /// <param name="targetType">The binding target type; unused.</param>
        /// <param name="parameter">The translation key.</param>
        /// <param name="culture">The culture; unused (translation is dictionary-based).</param>
        /// <returns>The translated string, or the parameter itself when it is not a string key.</returns>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => parameter is string key ? Instance.Translate(key) : parameter ?? string.Empty;

        /// <summary>Not supported — translation is one-way.</summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
