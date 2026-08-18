using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Converts a TimeSpan to a string representation in the format "hh:mm:ss" or "mm:ss".
/// </summary>
public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan timeSpan) 
            return "--:--";
        
        if (timeSpan <= TimeSpan.Zero)
            return "00:00";
        
        if (timeSpan.TotalHours >= 1)
            return timeSpan.ToString(@"hh\:mm\:ss");
        
        return timeSpan.ToString(@"mm\:ss");

    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}