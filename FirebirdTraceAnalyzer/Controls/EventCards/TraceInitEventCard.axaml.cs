using Avalonia;
using Avalonia.Controls.Primitives;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class TraceInitEventCard : TemplatedControl
{
    
    public static readonly StyledProperty<DateTime> TimestampProperty =
        AvaloniaProperty.Register<TraceInitEventCard, DateTime>(nameof(Timestamp), DateTime.MinValue);
    
    public static readonly StyledProperty<int> TraceIdProperty =
        AvaloniaProperty.Register<TraceInitEventCard, int>(nameof(TraceId), 0);
    
    public static readonly StyledProperty<string> HexTraceIdProperty =
        AvaloniaProperty.Register<TraceInitEventCard, string>(nameof(HexTraceId), "0");
    
    public static readonly StyledProperty<int> SessionIdProperty =
        AvaloniaProperty.Register<TraceInitEventCard, int>(nameof(SessionId), 0);
    
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
    
    public int SessionId
    {
        get => GetValue(SessionIdProperty);
        set => SetValue(SessionIdProperty, value);
    }
}