using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Returns the offset of the modal window from the main window based on the ConverterParameter delta.
/// Used to set the overlay window size to "main window size minus margins" (almost full-screen).
/// </summary>
public sealed class SubtractConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double d || parameter is not string s || !double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
            return value;

        return Math.Max(0, d - p);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();   
    }
}
