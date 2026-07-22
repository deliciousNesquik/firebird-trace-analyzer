using Avalonia;
using Avalonia.Controls.Primitives;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class TriggerStartEventCard : TransactionalEventCardBase
{
    public static readonly StyledProperty<string> TriggerNameProperty =
        AvaloniaProperty.Register<TriggerStartEventCard, string>(nameof(TriggerName), "<not set>");
    
    public static readonly StyledProperty<string> TableProperty =
        AvaloniaProperty.Register<TriggerStartEventCard, string>(nameof(Table), "<not set>");
    
    public static readonly StyledProperty<string> TimingProperty =
        AvaloniaProperty.Register<TriggerStartEventCard, string>(nameof(Timing), "<not set>");
    
    public static readonly StyledProperty<string> EventProperty =
        AvaloniaProperty.Register<TriggerStartEventCard, string>(nameof(Event), "<not set>");
    
    
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