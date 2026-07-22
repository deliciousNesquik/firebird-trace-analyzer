using Avalonia;
using Avalonia.Controls.Primitives;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class StatementFinishEventCard : TransactionalEventCardBase
{
    public static readonly StyledProperty<long> StatementIdProperty =
        AvaloniaProperty.Register<StatementFinishEventCard, long>(nameof(StatementId), 0);
    
    public static readonly StyledProperty<string> SqlProperty =
        AvaloniaProperty.Register<StatementFinishEventCard, string>(nameof(Sql), "<not set>");
    
    public static readonly StyledProperty<IReadOnlyList<SqlParameters>> ParamsProperty =
        AvaloniaProperty.Register<StatementFinishEventCard, IReadOnlyList<SqlParameters>>(nameof(Params));
    
    public static readonly StyledProperty<int> ExecuteMsProperty =
        AvaloniaProperty.Register<StatementFinishEventCard, int>(nameof(ExecuteMs), 0);
    
    public static readonly StyledProperty<int> FetchCountProperty =
        AvaloniaProperty.Register<StatementFinishEventCard, int>(nameof(FetchCount), 0);
    
    public static readonly StyledProperty<int> ReadCountProperty =
        AvaloniaProperty.Register<StatementFinishEventCard, int>(nameof(ReadCount), 0);
    
    public static readonly StyledProperty<int> WriteCountProperty =
        AvaloniaProperty.Register<StatementFinishEventCard, int>(nameof(WriteCount), 0);
    
    public static readonly StyledProperty<int> MarkCountProperty =
        AvaloniaProperty.Register<StatementFinishEventCard, int>(nameof(MarkCount), 0);
    
    public long StatementId
    {
        get => GetValue(StatementIdProperty);
        set => SetValue(StatementIdProperty, value);
    }
    
    public string Sql
    {
        get => GetValue(SqlProperty);
        set => SetValue(SqlProperty, value);
    }
    
    public string ExecuteSql => ExecuteStatementsBuilder.Build(Sql, Params);

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