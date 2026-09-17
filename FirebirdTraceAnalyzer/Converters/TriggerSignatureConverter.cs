using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Converters;

/// <summary>
/// Собирает приглушённый суффикс сигнатуры триггера для компактной строки из полей
/// Table / Timing / Event, например: " · ORDERS · AFTER UPDATE" или " · ON CONNECT".
/// Отсутствующие части (Table/Timing могут быть null) опускаются вместе с разделителями.
/// </summary>
public sealed class TriggerSignatureConverter : IMultiValueConverter
{
    public static readonly TriggerSignatureConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var table = values.Count > 0 ? values[0] as string : null;
        var timing = values.Count > 1 ? values[1] as string : null;
        var @event = values.Count > 2 ? values[2] as string : null;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(table))
            parts.Add(table.Trim());

        var timingEvent = string.IsNullOrWhiteSpace(timing)
            ? (@event ?? string.Empty).Trim()
            : $"{timing.Trim()} {(@event ?? string.Empty).Trim()}".Trim();
        if (timingEvent.Length > 0)
            parts.Add(timingEvent);

        return parts.Count > 0 ? " · " + string.Join(" · ", parts) : string.Empty;
    }
}
