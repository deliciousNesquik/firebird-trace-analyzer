using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// XAML-расширение перевода: <c>{loc:Tr Settings.Title}</c> или <c>{loc:Tr Key=Settings.Title}</c>.
/// Возвращает привязку к <see cref="Localizer"/> с конвертером (ключ — через ConverterParameter),
/// поэтому текст обновляется при смене языка вживую. Источник задан явно, так что расширение не
/// зависит от <c>x:DataType</c>/compiled bindings.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    /// <summary>Ключ перевода (например, <c>Settings.Title</c>).</summary>
    public string Key { get; set; } = string.Empty;

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
