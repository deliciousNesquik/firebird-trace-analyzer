using Avalonia;
using FirebirdTraceAnalyzer.Controls.EventCards;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>
/// База компактных карточек триггеров: имя триггера и его сигнатура (таблица/timing/событие).
/// </summary>
public abstract class TriggerCompactCardBase : ConnectedEventCardBase
{
    public static readonly StyledProperty<string> TriggerNameProperty =
        AvaloniaProperty.Register<TriggerCompactCardBase, string>(nameof(TriggerName), string.Empty);

    public static readonly StyledProperty<string?> TableProperty =
        AvaloniaProperty.Register<TriggerCompactCardBase, string?>(nameof(Table));

    public static readonly StyledProperty<string?> TimingProperty =
        AvaloniaProperty.Register<TriggerCompactCardBase, string?>(nameof(Timing));

    public static readonly StyledProperty<string> EventProperty =
        AvaloniaProperty.Register<TriggerCompactCardBase, string>(nameof(Event), string.Empty);

    public string TriggerName
    {
        get => GetValue(TriggerNameProperty);
        set => SetValue(TriggerNameProperty, value);
    }

    public string? Table
    {
        get => GetValue(TableProperty);
        set => SetValue(TableProperty, value);
    }

    public string? Timing
    {
        get => GetValue(TimingProperty);
        set => SetValue(TimingProperty, value);
    }

    public string Event
    {
        get => GetValue(EventProperty);
        set => SetValue(EventProperty, value);
    }
}
