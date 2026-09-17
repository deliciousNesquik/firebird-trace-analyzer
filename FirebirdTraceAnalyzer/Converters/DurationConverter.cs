using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Форматирует <see cref="TimeSpan"/> в компактную человекочитаемую длительность:
/// "740ms", "5s 900ms", "3m 32s", "1h 4m". null / отрицательное → пустая строка.
/// </summary>
public sealed class DurationConverter : IValueConverter
{
    public static readonly DurationConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan d || d < TimeSpan.Zero)
            return string.Empty;

        if (d.TotalMilliseconds < 1000)
            return $"{(int)d.TotalMilliseconds}ms";

        if (d.TotalSeconds < 60)
            return d.Milliseconds > 0 ? $"{d.Seconds}s {d.Milliseconds}ms" : $"{d.Seconds}s";

        if (d.TotalMinutes < 60)
            return d.Seconds > 0 ? $"{(int)d.TotalMinutes}m {d.Seconds}s" : $"{(int)d.TotalMinutes}m";

        return d.Minutes > 0 ? $"{(int)d.TotalHours}h {d.Minutes}m" : $"{(int)d.TotalHours}h";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
