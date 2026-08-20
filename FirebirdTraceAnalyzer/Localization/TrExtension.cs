using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// XAML translation markup extension: <c>{loc:Tr Settings.Title}</c> or <c>{loc:Tr Key=Settings.Title}</c>.
/// Returns a binding to <see cref="Localizer"/> with a converter (the key travels via ConverterParameter),
/// so the text updates live on language change. The source is set explicitly, so the extension does not
/// depend on <c>x:DataType</c>/compiled bindings.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    /// <summary>Initializes a new instance with an empty <see cref="Key"/> (set it via <c>Key=</c>).</summary>
    public TrExtension() { }

    /// <summary>Initializes a new instance with the given translation key (positional syntax).</summary>
    /// <param name="key">The translation key (e.g. <c>Settings.Title</c>).</param>
    public TrExtension(string key) => Key = key;

    /// <summary>The translation key (e.g. <c>Settings.Title</c>).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Builds the live-updating binding that resolves <see cref="Key"/> through <see cref="Localizer.TrConverter"/>.</summary>
    /// <param name="serviceProvider">The XAML service provider (unused; the binding source is explicit).</param>
    /// <returns>A <see cref="Binding"/> whose value is the translated string for <see cref="Key"/>.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
        => new Binding
        {
            Source = Localizer.Instance,
            Path = nameof(Localizer.Generation),
            Mode = BindingMode.OneWay,
            Converter = Localizer.TrConverter,
            ConverterParameter = Key
        };
}
