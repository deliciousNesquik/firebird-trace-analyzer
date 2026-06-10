using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class TriggerFinishEventCard : TemplatedControl
{
    public static readonly StyledProperty<DateTime> TimestampProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, DateTime>(nameof(Timestamp), DateTime.MinValue);
    
    public static readonly StyledProperty<int> TraceIdProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, int>(nameof(TraceId), 0);
    
    public static readonly StyledProperty<string> HexTraceIdProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(HexTraceId), "0");
    
    public static readonly StyledProperty<string> DatabasePathProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(DatabasePath), "<not set>");
    
    public static readonly StyledProperty<string> UserProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(User), "<not set>");
    
    public static readonly StyledProperty<string> RoleProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(Role), "<not set>");
    
    public static readonly StyledProperty<long> AttachmentIdProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, long>(nameof(AttachmentId), 0);
    
    public static readonly StyledProperty<string> ProtocolProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(Protocol), "<not set>");
    
    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(Address), "<not set>");
    
    public static readonly StyledProperty<int> PortProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, int>(nameof(Port), 0);
    
    public static readonly StyledProperty<string> CharsetProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(Charset), "<not set>");
    
    public static readonly StyledProperty<string> ProcessPathProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(ProcessPath), "<not set>");
    
    public static readonly StyledProperty<int> ProcessIdProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, int>(nameof(ProcessId), 0);
    
    public static readonly StyledProperty<int> TransactionIdProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, int>(nameof(TransactionId), 0);
    
    public static readonly StyledProperty<string> IsolationLevelProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(IsolationLevel), "<not set>");
    
    public static readonly StyledProperty<string> ConsistencyModeProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(ConsistencyMode), "<not set>");
    
    public static readonly StyledProperty<string> LockModeProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(LockMode), "<not set>");
    
    public static readonly StyledProperty<string> AccessModeProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(AccessMode), "<not set>");
    
    public static readonly StyledProperty<string> TriggerNameProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(TriggerName), "<not set>");
    
    public static readonly StyledProperty<string> TableProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(Table), "<not set>");
    
    public static readonly StyledProperty<string> TimingProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(Timing), "<not set>");
    
    public static readonly StyledProperty<string> EventProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, string>(nameof(Event), "<not set>");
    
    public static readonly StyledProperty<int> ExecuteMsProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, int>(nameof(ExecuteMs), 0);
    
    public static readonly StyledProperty<int> FetchCountProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, int>(nameof(FetchCount), 0);
    
    public static readonly StyledProperty<int> ReadCountProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, int>(nameof(ReadCount), 0);
    
    public static readonly StyledProperty<int> WriteCountProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, int>(nameof(WriteCount), 0);
    
    public static readonly StyledProperty<int> MarkCountProperty =
        AvaloniaProperty.Register<TriggerFinishEventCard, int>(nameof(MarkCount), 0);
    
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
    
    public string DatabasePath
    {
        get => GetValue(DatabasePathProperty);
        set => SetValue(DatabasePathProperty, value);
    }
    
    public string User
    {
        get => GetValue(UserProperty);
        set => SetValue(UserProperty, value);
    }
    
    public string Role
    {
        get => GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }
    
    public long AttachmentId
    {
        get => GetValue(AttachmentIdProperty);
        set => SetValue(AttachmentIdProperty, value);
    }
    
    public string Protocol
    {
        get => GetValue(ProtocolProperty);
        set => SetValue(ProtocolProperty, value);
    }
    
    public string Address
    {
        get => GetValue(AddressProperty);
        set => SetValue(AddressProperty, value);
    }
    
    public int Port
    {
        get => GetValue(PortProperty);
        set => SetValue(PortProperty, value);
    }
    
    public string Charset
    {
        get => GetValue(CharsetProperty);
        set => SetValue(CharsetProperty, value);
    }
    
    public string ProcessPath
    {
        get => GetValue(ProcessPathProperty);
        set => SetValue(ProcessPathProperty, value);
    }
    
    public int ProcessId
    {
        get => GetValue(ProcessIdProperty);
        set => SetValue(ProcessIdProperty, value);
    }
    
    public int TransactionId
    {
        get => GetValue(TransactionIdProperty);
        set => SetValue(TransactionIdProperty, value);
    }
 
    public string IsolationLevel
    {
        get => GetValue(IsolationLevelProperty);
        set => SetValue(IsolationLevelProperty, value);
    }
    
    public string ConsistencyMode
    {
        get => GetValue(ConsistencyModeProperty);
        set => SetValue(ConsistencyModeProperty, value);
    }
    
    public string LockMode
    {
        get => GetValue(LockModeProperty);
        set => SetValue(LockModeProperty, value);
    }
    
    public string AccessMode
    {
        get => GetValue(AccessModeProperty);
        set => SetValue(AccessModeProperty, value);
    }
    
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