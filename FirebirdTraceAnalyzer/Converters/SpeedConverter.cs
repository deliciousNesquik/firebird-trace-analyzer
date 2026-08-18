using System.Globalization;
using Avalonia.Data.Converters;
using FirebirdTraceAnalyzer.Core;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// A value converter that converts a speed in bytes per second to a human-readable string format.
/// </summary>
/// <remarks>The symbols <c>"B/s"</c>, <c>"KB/s"</c>, <c>"MB/s"</c>, etc., are hard-coded; if different designations are required, modify the return values in this converter and in the <see cref="ByteSizeFormatter"/> class accordingly. </remarks>
public class SpeedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double speed)
            return "0 B/s";

        return ByteSizeFormatter.FormatSpeed(speed);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}