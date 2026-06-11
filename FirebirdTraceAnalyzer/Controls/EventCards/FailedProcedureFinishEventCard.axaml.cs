using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class FailedProcedureFinishEventCard : TemplatedControl
{
    public static readonly StyledProperty<DateTime> TimestampProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, DateTime>(nameof(Timestamp), DateTime.MinValue);
    
    public static readonly StyledProperty<int> TraceIdProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, int>(nameof(TraceId), 0);
    
    public static readonly StyledProperty<string> HexTraceIdProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(HexTraceId), "0");
    
    public static readonly StyledProperty<string> DatabasePathProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(DatabasePath), "<not set>");
    
    public static readonly StyledProperty<string> UserProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(User), "<not set>");
    
    public static readonly StyledProperty<string> RoleProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(Role), "<not set>");
    
    public static readonly StyledProperty<long> AttachmentIdProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, long>(nameof(AttachmentId), 0);
    
    public static readonly StyledProperty<string> ProtocolProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(Protocol), "<not set>");
    
    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(Address), "<not set>");
    
    public static readonly StyledProperty<int> PortProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, int>(nameof(Port), 0);
    
    public static readonly StyledProperty<string> CharsetProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(Charset), "<not set>");
    
    public static readonly StyledProperty<string> ProcessPathProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(ProcessPath), "<not set>");
    
    public static readonly StyledProperty<int> ProcessIdProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, int>(nameof(ProcessId), 0);
    
    public static readonly StyledProperty<long> TransactionIdProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, long>(nameof(TransactionId), 0);
    
    public static readonly StyledProperty<string> IsolationLevelProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(IsolationLevel), "<not set>");
    
    public static readonly StyledProperty<string> ConsistencyModeProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(ConsistencyMode), "<not set>");
    
    public static readonly StyledProperty<string> LockModeProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(LockMode), "<not set>");
    
    public static readonly StyledProperty<string> AccessModeProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(AccessMode), "<not set>");
    
    public static readonly StyledProperty<string> ProcedureNameProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, string>(nameof(ProcedureName), "<not set>");
    
    public static readonly StyledProperty<IReadOnlyList<SqlParameters>> ParamsProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, IReadOnlyList<SqlParameters>>(nameof(Params));
    
    public static readonly StyledProperty<int> ExecuteMsProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, int>(nameof(ExecuteMs), 0);
    
    public static readonly StyledProperty<int> FetchCountProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, int>(nameof(FetchCount), 0);
    
    public static readonly StyledProperty<int> ReadCountProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, int>(nameof(ReadCount), 0);
    
    public static readonly StyledProperty<int> WriteCountProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, int>(nameof(WriteCount), 0);
    
    public static readonly StyledProperty<int> MarkCountProperty =
        AvaloniaProperty.Register<FailedProcedureFinishEventCard, int>(nameof(MarkCount), 0);
    
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
    
    public long TransactionId
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
    
    public string ProcedureName
    {
        get => GetValue(ProcedureNameProperty);
        set => SetValue(ProcedureNameProperty, value);
    }


    public string ExecuteProcedure
    {
        get
        {
            var execute = new StringBuilder();

            execute.Append($"EXECUTE PROCEDURE {ProcedureName}(");

            execute.Append(string.Join(", ", Params.Select(param =>
            {
                var value = param.Value?.ToString();

                if (value == "<NULL>")
                    return "NULL";

                if (int.TryParse(value, out _) ||
                    decimal.TryParse(value, out _) ||
                    bool.TryParse(value, out _))
                {
                    return value;
                }

                return $"'{value?.Replace("'", "''")}'";
            })));

            execute.Append(')');
        
            return execute.ToString();
        }
    }
    
    public IReadOnlyList<SqlParameters> Params
    {
        get => GetValue(ParamsProperty);
        set => SetValue(ParamsProperty, value);
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
    
}