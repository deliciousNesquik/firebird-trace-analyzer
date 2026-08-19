using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>Converts the visibility condition while adhering to user-defined parameters.</summary>
/// <param name="value">The actual number of elements to be used in the condition</param>
/// <param name="parameter">The threshold value for the visibility condition</param>
/// <remarks>The condition is met if the actual value is strictly greater than the threshold set by the user.</remarks>
public class CountToVisibilityConverter : IValueConverter
{
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int count) return false;
        var threshold = parameter is int p
            ? p
            : int.TryParse(parameter?.ToString(), NumberStyles.Integer, culture, out var t) ? t : 0;
        return count > threshold;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}