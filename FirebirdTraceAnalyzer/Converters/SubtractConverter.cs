using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Вычитает из числового значения величину из ConverterParameter. Используется, чтобы задать
/// размер overlay-редактора как «размер главного окна минус поля» (почти на весь экран).
/// </summary>
public sealed class SubtractConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double d)
            return value;

        var delta = 0d;
        if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
            delta = p;

        return Math.Max(0, d - delta);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
