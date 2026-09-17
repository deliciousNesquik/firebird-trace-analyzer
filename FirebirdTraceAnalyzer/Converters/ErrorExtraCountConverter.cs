using System.Globalization;
using Avalonia.Data.Converters;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Для компактной строки ошибки: возвращает приглушённый хвост " · +N" с числом дополнительных
/// строк стека (сверх первой). Если строка одна (или список пуст) — пустая строка.
/// </summary>
public sealed class ErrorExtraCountConverter : IValueConverter
{
    public static readonly ErrorExtraCountConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IReadOnlyCollection<ErrorLines> { Count: > 1 } errors
            ? $" · +{errors.Count - 1}"
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
