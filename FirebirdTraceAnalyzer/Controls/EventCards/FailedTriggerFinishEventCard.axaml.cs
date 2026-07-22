using Avalonia;
using Avalonia.Controls.Primitives;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class FailedTriggerFinishEventCard : TransactionalEventCardBase
{
    public static readonly StyledProperty<string> TriggerNameProperty =
        AvaloniaProperty.Register<FailedTriggerFinishEventCard, string>(nameof(TriggerName), "<not set>");
    
    public static readonly StyledProperty<string> TableProperty =
        AvaloniaProperty.Register<FailedTriggerFinishEventCard, string>(nameof(Table), "<not set>");
    
    public static readonly StyledProperty<string> TimingProperty =
        AvaloniaProperty.Register<FailedTriggerFinishEventCard, string>(nameof(Timing), "<not set>");
    
    public static readonly StyledProperty<string> EventProperty =
        AvaloniaProperty.Register<FailedTriggerFinishEventCard, string>(nameof(Event), "<not set>");
    
    public static readonly StyledProperty<int> ExecuteMsProperty =
        AvaloniaProperty.Register<FailedTriggerFinishEventCard, int>(nameof(ExecuteMs), 0);
    
    public static readonly StyledProperty<int> FetchCountProperty =
        AvaloniaProperty.Register<FailedTriggerFinishEventCard, int>(nameof(FetchCount), 0);
    
    public static readonly StyledProperty<int> ReadCountProperty =
        AvaloniaProperty.Register<FailedTriggerFinishEventCard, int>(nameof(ReadCount), 0);
    
    public static readonly StyledProperty<int> WriteCountProperty =
        AvaloniaProperty.Register<FailedTriggerFinishEventCard, int>(nameof(WriteCount), 0);
    
    public static readonly StyledProperty<int> MarkCountProperty =
        AvaloniaProperty.Register<FailedTriggerFinishEventCard, int>(nameof(MarkCount), 0);
    
    public string TriggerName
    {
        get => GetValue(TriggerNameProperty);
        set => SetValue(TriggerNameProperty, value);
    }
    
    public string Table
    {
        get => GetValue(TableProperty);
        set => SetValue(TableProperty, value);
    }
    
    public string Timing
    {
        get => GetValue(TimingProperty);
        set => SetValue(TimingProperty, value);
    }
    
    public string Event
    {
        get => GetValue(EventProperty);
        set => SetValue(EventProperty, value);
    }
    
    public int ExecuteMs
    {
        get => GetValue(ExecuteMsProperty);
        set => SetValue(ExecuteMsProperty, value);
    }
    
    public int FetchCount
    {
        get => GetValue(FetchCountProperty);
        set => SetValue(FetchCountProperty, value);
    }
    
    public int ReadCount
    {
        get => GetValue(ReadCountProperty);
        set => SetValue(ReadCountProperty, value);
    }
    
    public int WriteCount
    {
        get => GetValue(WriteCountProperty);
        set => SetValue(WriteCountProperty, value);
    }
    
    public int MarkCount
    {
        get => GetValue(MarkCountProperty);
        set => SetValue(MarkCountProperty, value);
    }
    
    public string TriggerDescription
    {
        get
        {
            // Database trigger
            if (string.IsNullOrWhiteSpace(Table))
            {
                return $"Trigger {TriggerName} ({Event}):";
            }

            // DML trigger
            return $"Trigger {TriggerName} FOR {Table} ({Timing} {Event}):";
        }
    }
    
}