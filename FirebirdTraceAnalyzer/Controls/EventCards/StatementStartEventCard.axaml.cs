using Avalonia;
using Avalonia.Controls.Primitives;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class StatementStartEventCard : TransactionalEventCardBase
{
    public static readonly StyledProperty<long> StatementIdProperty =
        AvaloniaProperty.Register<StatementStartEventCard, long>(nameof(StatementId), 0);
    
    public static readonly StyledProperty<string> SqlProperty =
        AvaloniaProperty.Register<StatementStartEventCard, string>(nameof(Sql), "<not set>");
    
    public static readonly StyledProperty<IReadOnlyList<SqlParameters?>> ParamsProperty =
        AvaloniaProperty.Register<StatementStartEventCard, IReadOnlyList<SqlParameters?>>(nameof(Params));
    
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

    public IReadOnlyList<SqlParameters?> Params
    {
        get => GetValue(ParamsProperty);
        set => SetValue(ParamsProperty, value);
    }
    
}