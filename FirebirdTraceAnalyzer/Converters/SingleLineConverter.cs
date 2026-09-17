using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Схлопывает любой пробельный текст (переводы строк, табуляции, повторные пробелы) в одну строку
/// с одиночными пробелами. Нужен для компактных карточек: SQL в трейсе часто многострочный, а строка
/// плотного списка должна оставаться однострочной (обрезается по ширине через TextTrimming).
/// </summary>
public sealed class SingleLineConverter : IValueConverter
{
    public static readonly SingleLineConverter Instance = new();

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s ? Whitespace.Replace(s, " ").Trim() : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
