using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FirebirdTraceAnalyzer.Core;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Конвертирует скорость в байтах/сек в читаемый формат
/// </summary>
public class SpeedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double speed)
        {
            return ByteSizeFormatter.FormatSpeed(speed);
        }

        return "0 B/s";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}