using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>Converts a boolean value into a string masking character.</summary>
/// <returns>(\0 - without masking) if true and ('*' - masking character) if false</returns>
/// <remarks>masking character hardcoded, if you need a different one, modify the return values accordingly</remarks>
public class BoolToPasswordCharConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool showPassword)
            return '*'; // By default, mask the password

        // The "\0" character or attribute overrides imposed string formatting patterns
        return showPassword ? '\0' : '*';
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}