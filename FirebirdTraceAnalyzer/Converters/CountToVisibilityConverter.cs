using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Показываем поиск только если элементов > 10
        if (value is int count)
            return count > 10;

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}