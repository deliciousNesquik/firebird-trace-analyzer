using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using FirebirdTraceAnalyzer.Core;

namespace FirebirdTraceAnalyzer.Converters;

public sealed class BytesToReadableSizeConverter : IValueConverter
{
    public static readonly BytesToReadableSizeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsAssignableTo(typeof(string)))
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);

        if (value is null)
            return string.Empty;

        if (!TryGetBytes(value, out var bytes))
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);

        return ByteSizeFormatter.FormatBytes(bytes);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static bool TryGetBytes(object value, out long bytes)
    {
        switch (value)
        {
            case long l:
                bytes = l;
                return true;
            case int i:
                bytes = i;
                return true;
            case uint ui:
                bytes = ui;
                return true;
            case ulong ul when ul <= long.MaxValue:
                bytes = (long)ul;
                return true;
            default:
                bytes = 0;
                return false;
        }
    }
}