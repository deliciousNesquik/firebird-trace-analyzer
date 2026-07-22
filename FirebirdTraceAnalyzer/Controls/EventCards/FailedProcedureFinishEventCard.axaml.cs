using Avalonia;
using Avalonia.Controls.Primitives;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class FailedProcedureFinishEventCard : TransactionalEventCardBase
{
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
    
    public string ProcedureName
    {
        get => GetValue(ProcedureNameProperty);
        set => SetValue(ProcedureNameProperty, value);
    }


    public string ExecuteProcedure => ExecuteProcedureBuilder.Build(ProcedureName, Params);
    
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