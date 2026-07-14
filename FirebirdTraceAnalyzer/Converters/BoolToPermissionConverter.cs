using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Конвертирует bool прав доступа в символ (r/w/x или -)
/// </summary>
public class BoolToPermissionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasPermission && parameter is string permissionChar)
        {
            return hasPermission ? permissionChar : "-";
        }

        return "-";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}