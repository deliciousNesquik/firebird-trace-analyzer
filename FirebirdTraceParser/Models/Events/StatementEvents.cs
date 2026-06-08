using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.ValueObjects;
using FirebirdTraceParser.Parsing.Utils;

namespace FirebirdTraceParser.Models.Events;

/// <summary>
/// Базовый класс для событий SQL statement.
/// </summary>
public class StatementEventBase : EventBase
{
    public required AttachmentInfo Attachment { get; init; }
    public required TransactionInfo? Transaction { get; init; }
    
    [SortableField("Statement ID", Priority = 2, Category = "Statements")]
    [FilterableField("Statement ID", Category = "Statements", FilterType =  FilterType.StringMultiSelect)]
    public long? StatementId { get; init; }
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