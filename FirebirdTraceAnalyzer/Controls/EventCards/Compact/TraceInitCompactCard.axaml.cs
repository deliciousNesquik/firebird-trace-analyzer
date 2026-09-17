using Avalonia;
using FirebirdTraceAnalyzer.Controls.EventCards;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>
/// Компактная (однострочная) карточка начала trace‑сессии.
/// </summary>
public class TraceInitCompactCard : EventCardBase
{
    public static readonly StyledProperty<int> SessionIdProperty =
        AvaloniaProperty.Register<TraceInitCompactCard, int>(nameof(SessionId), 0);

    public int SessionId
    {
        get => GetValue(SessionIdProperty);
        set => SetValue(SessionIdProperty, value);
    }
}
