using System.Collections;
using System.Globalization;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Единое форматирование значений полей событий для всех экспортёров отчётов.
/// Главная цель — корректно печатать коллекции (цепочка ошибок, SQL-параметры и т.п.),
/// а не имя их типа (что происходит при простом value.ToString() над коллекцией).
/// Читаемость элементов задаётся их собственным ToString() в value-объектах.
/// </summary>
public static class ReportValueFormatter
{
    public static string Format(object? value, string? format)
    {
        if (value is null)
            return string.Empty;

        // Числа/даты с указанным форматом ("N0", "yyyy-MM-dd HH:mm:ss", ...)
        if (!string.IsNullOrWhiteSpace(format) && value is IFormattable formattable)
            return formattable.ToString(format, CultureInfo.InvariantCulture);

        // Строка — отдаём как есть. ВАЖНО: string тоже IEnumerable, поэтому проверяем до коллекций.
        if (value is string s)
            return s;

        // Любая коллекция → по элементу на строку, каждый через свой ToString().
        // Так корректно печатаются ErrorLines, SqlParameters и любой будущий список —
        // без отдельной ветки под каждый тип.
        if (value is IEnumerable enumerable)
            return string.Join(Environment.NewLine,
                enumerable.Cast<object?>().Select(o => o?.ToString() ?? string.Empty));

        return value.ToString() ?? string.Empty;
    }
}
