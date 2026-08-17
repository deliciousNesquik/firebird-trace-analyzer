using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>Converts a boolean value into the passed access permission character (r/w/x или -).</summary>
/// <returns>The permission character if the boolean value is true, otherwise "-".</returns>
/// <remarks>Character use for not having permission is "-" hardcoded if you need a different one, modify the return values accordingly</remarks>
public class BoolToPermissionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool hasPermission || parameter is not string permissionChar)
            return "-";

        return hasPermission ? permissionChar : "-";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}