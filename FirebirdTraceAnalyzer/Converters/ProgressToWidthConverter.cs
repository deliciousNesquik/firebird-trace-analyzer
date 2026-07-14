using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Конвертирует процент (0-100) в ширину для прогресс-бара (максимум 300px)
/// </summary>
public class ProgressToWidthConverter : IValueConverter
{
    private const double MaxWidth = 300.0;
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double progress)
        {
            // Ограничиваем от 0 до 100
            var normalizedProgress = Math.Clamp(progress, 0, 100);
            return (normalizedProgress / 100.0) * MaxWidth;
        }

        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}