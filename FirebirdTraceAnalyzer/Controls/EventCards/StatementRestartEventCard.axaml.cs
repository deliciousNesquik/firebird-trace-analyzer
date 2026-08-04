using Avalonia;
using Avalonia.Controls.Primitives;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class StatementRestartEventCard : TransactionalEventCardBase
{
    public static readonly StyledProperty<long> StatementIdProperty =
        AvaloniaProperty.Register<StatementRestartEventCard, long>(nameof(StatementId), 0);
    
    public static readonly StyledProperty<int> RestartCountProperty =
        AvaloniaProperty.Register<StatementRestartEventCard, int>(nameof(RestartCount), 0);
    
    public static readonly StyledProperty<string> SqlProperty =
        AvaloniaProperty.Register<StatementRestartEventCard, string>(nameof(Sql), "<not set>");
    
    public static readonly StyledProperty<IReadOnlyList<SqlParameters>> ParamsProperty =
        AvaloniaProperty.Register<StatementRestartEventCard, IReadOnlyList<SqlParameters>>(nameof(Params));
    
    public long StatementId
    {
        get => GetValue(StatementIdProperty);
        set => SetValue(StatementIdProperty, value);
    }
    
    public int RestartCount
    {
        get => GetValue(RestartCountProperty);
        set => SetValue(RestartCountProperty, value);
    }
    
    public string Sql
    {
        get => GetValue(SqlProperty);
        set => SetValue(SqlProperty, value);
    }
    
    
    public string ExecuteSql
    {
        get => ExecuteStatementsBuilder.Build(Sql, Params);
    }
    
    public IReadOnlyList<SqlParameters> Params
    {
        get => GetValue(ParamsProperty);
        set => SetValue(ParamsProperty, value);
    }
    
}