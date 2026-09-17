using Avalonia;
using FirebirdTraceAnalyzer.Controls.EventCards;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>
/// Компактная (однострочная) карточка завершения trace‑сессии.
/// </summary>
public class TraceFiniCompactCard : EventCardBase
{
    public static readonly StyledProperty<int> SessionIdProperty =
        AvaloniaProperty.Register<TraceFiniCompactCard, int>(nameof(SessionId), 0);

    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<TraceFiniCompactCard, TimeSpan?>(nameof(Duration));

    public int SessionId
    {
        get => GetValue(SessionIdProperty);
        set => SetValue(SessionIdProperty, value);
    }

    /// <summary>Длительность сессии (может быть null, если начала сессии нет в файле).</summary>
    public TimeSpan? Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }
}
