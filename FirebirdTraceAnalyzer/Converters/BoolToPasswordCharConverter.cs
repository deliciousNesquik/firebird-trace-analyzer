using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Конвертирует bool в символ маскировки пароля
/// true = показать пароль (0 - без маскировки)
/// false = скрыть пароль ('•')
/// </summary>
public class BoolToPasswordCharConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool showPassword)
        {
            // Если showPassword = true, возвращаем '\0' (нет маскировки)
            // Если showPassword = false, возвращаем '•' (маскировка)
            return showPassword ? '\0' : '*';
        }

        return '•'; // По умолчанию маскируем
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}