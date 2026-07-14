using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceParser.Models.Events;

/// <summary>
/// Базовый класс для событий SQL statement.
/// </summary>
public class StatementEventBase : EventBase
{
    public required AttachmentInfo Attachment { get; init; }
    public required TransactionInfo? Transaction { get; init; }
    
    [SortableField("Statement ID", Category = "Statements")]
    [FilterableField("Statement ID", Category = "Statements", FilterType =  FilterType.StringMultiSelect)]
    public long? StatementId { get; init; }
    [FilterableField("SQL", Category = "Statements", FilterType = FilterType.TextSearch)]
    public required string Sql { get; init; }
    public required IReadOnlyList<SqlParameters> Parameters { get; init; }
}

/// <summary>
/// Событие начала выполнения statement.
/// </summary>
public sealed class StatementStartEvent : StatementEventBase;


public sealed class StatementRestartEvent : StatementEventBase
{
    public required int? RestartCount { get; init; } 
}

/// <summary>
/// Событие завершения выполнения statement.
/// </summary>
public sealed class StatementFinishEvent : StatementEventBase
{
    public required PerformanceInfo Performance { get; init; }
    public PerformanceTable? PerformanceTable { get; init; }
}

public sealed class FailedStatementFinishEvent : StatementEventBase
{
    public required PerformanceInfo Performance { get; init; }
    public PerformanceTable? PerformanceTable { get; init; }
}