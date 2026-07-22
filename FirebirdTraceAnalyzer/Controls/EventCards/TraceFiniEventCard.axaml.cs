using Avalonia;
using Avalonia.Controls.Primitives;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class TraceFiniEventCard : EventCardBase
{
    
    public static readonly StyledProperty<int> SessionIdProperty =
        AvaloniaProperty.Register<TraceFiniEventCard, int>(nameof(SessionId), 0);
    
    public int SessionId
    {
        get => GetValue(SessionIdProperty);
        set => SetValue(SessionIdProperty, value);
    }
}