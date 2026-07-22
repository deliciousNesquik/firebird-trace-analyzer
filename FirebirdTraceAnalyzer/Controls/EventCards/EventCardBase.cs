using Avalonia;
using Avalonia.Controls.Primitives;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

/// <summary>
/// Базовый класс карточек событий. Держит свойства, общие для ВСЕХ типов событий
/// (метка времени и идентификаторы трассировки), чтобы не дублировать их регистрацию в каждой карточке.
/// Значения по умолчанию сохранены прежними.
/// </summary>
public abstract class EventCardBase : TemplatedControl
{
    public static readonly StyledProperty<DateTime> TimestampProperty =
        AvaloniaProperty.Register<EventCardBase, DateTime>(nameof(Timestamp), DateTime.MinValue);

    public static readonly StyledProperty<int> TraceIdProperty =
        AvaloniaProperty.Register<EventCardBase, int>(nameof(TraceId), 0);

    public static readonly StyledProperty<string> HexTraceIdProperty =
        AvaloniaProperty.Register<EventCardBase, string>(nameof(HexTraceId), "0");

    public DateTime Timestamp
    {
        get => GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    public int TraceId
    {
        get => GetValue(TraceIdProperty);
        set => SetValue(TraceIdProperty, value);
    }

    public string HexTraceId
    {
        get => GetValue(HexTraceIdProperty);
        set => SetValue(HexTraceIdProperty, value);
    }
}
