using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class StatementRestartEventCard : TemplatedControl
{
    
    private static string FormatParam(SqlParameters parameters)
    {
        var value = parameters.Value?.ToString();

        if (value == "<NULL>")
            return "NULL";

        return parameters.Dtype.ToLower() switch
        {
            "varchar(32764)" or "varchar" or "char" or "text" =>
                $"'{value?.Replace("'", "''")}'",

            "timestamp" =>
                $"'{value}'",

            "date" =>
                $"'{value}'",

            "time" =>
                $"'{value}'",

            "bigint" or "int" or "smallint" or "integer" =>
                value ?? "NULL",

            _ =>
                value ?? "NULL"
        };
    }
    
    public static readonly StyledProperty<DateTime> TimestampProperty =
        AvaloniaProperty.Register<StatementStartEventCard, DateTime>(nameof(Timestamp), DateTime.MinValue);
    
    public static readonly StyledProperty<int> TraceIdProperty =
        AvaloniaProperty.Register<StatementStartEventCard, int>(nameof(TraceId), 0);
    
    public static readonly StyledProperty<string> HexTraceIdProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(HexTraceId), "0");
    
    public static readonly StyledProperty<long> StatementIdProperty =
        AvaloniaProperty.Register<StatementStartEventCard, long>(nameof(StatementId), 0);
    
    public static readonly StyledProperty<string> DatabasePathProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(DatabasePath), "<not set>");
    
    public static readonly StyledProperty<string> UserProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(User), "<not set>");
    
    public static readonly StyledProperty<string> RoleProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(Role), "<not set>");
    
    public static readonly StyledProperty<long> AttachmentIdProperty =
        AvaloniaProperty.Register<StatementStartEventCard, long>(nameof(AttachmentId), 0);
    
    public static readonly StyledProperty<string> ProtocolProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(Protocol), "<not set>");
    
    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(Address), "<not set>");
    
    public static readonly StyledProperty<int> PortProperty =
        AvaloniaProperty.Register<StatementStartEventCard, int>(nameof(Port), 0);
    
    public static readonly StyledProperty<string> CharsetProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(Charset), "<not set>");
    
    public static readonly StyledProperty<string> ProcessPathProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(ProcessPath), "<not set>");
    
    public static readonly StyledProperty<int> ProcessIdProperty =
        AvaloniaProperty.Register<StatementStartEventCard, int>(nameof(ProcessId), 0);
    
    public static readonly StyledProperty<int> TransactionIdProperty =
        AvaloniaProperty.Register<StatementStartEventCard, int>(nameof(TransactionId), 0);
    
    public static readonly StyledProperty<int> RestartCountProperty =
        AvaloniaProperty.Register<StatementStartEventCard, int>(nameof(RestartCount), 0);
    
    public static readonly StyledProperty<string> IsolationLevelProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(IsolationLevel), "<not set>");
    
    public static readonly StyledProperty<string> ConsistencyModeProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(ConsistencyMode), "<not set>");
    
    public static readonly StyledProperty<string> LockModeProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(LockMode), "<not set>");
    
    public static readonly StyledProperty<string> AccessModeProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(AccessMode), "<not set>");
    
    public static readonly StyledProperty<string> SqlProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(Sql), "<not set>");
    
    public static readonly StyledProperty<string> ExecuteSqlProperty =
        AvaloniaProperty.Register<FailedStatementFinishEventCard, string>(nameof(ExecuteSql), "<not set>");
    
    public static readonly StyledProperty<IReadOnlyList<SqlParameters>> ParamsProperty =
        AvaloniaProperty.Register<StatementStartEventCard, IReadOnlyList<SqlParameters>>(nameof(Params));
    
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
    
    public long StatementId
    {
        get => GetValue(StatementIdProperty);
        set => SetValue(StatementIdProperty, value);
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
    
    public int RestartCount
    {
        get => GetValue(RestartCountProperty);
        set => SetValue(RestartCountProperty, value);
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
    
    public string Sql
    {
        get => GetValue(SqlProperty);
        set => SetValue(SqlProperty, value);
    }
    
    
    public string ExecuteSql
    {
        get
        {
            if (Params == null || Params.Count == 0)
                return Sql;

            var sql = new StringBuilder();
            int index = 0;

            foreach (var ch in Sql)
            {
                if (ch == '?' && index < Params.Count)
                {
                    sql.Append(FormatParam(Params[index]));
                    index++;
                }
                else
                {
                    sql.Append(ch);
                }
            }

            return sql.ToString();
        }
    }
    
    public IReadOnlyList<SqlParameters> Params
    {
        get => GetValue(ParamsProperty);
        set => SetValue(ParamsProperty, value);
    }
    
}