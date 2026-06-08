using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceParser.Models.Events;

/// <summary>
/// Базовый класс для событий хранимых процедур.
/// </summary>
public class ProcedureEventBase : EventBase
{
    public required AttachmentInfo Attachment { get; init; }
    public required TransactionInfo Transaction { get; init; }
    
    [SortableField("Procedure name", Priority = 2, Category = "Procedures")]
    [FilterableField("Procedure name", Category = "Procedures", FilterType =  FilterType.StringMultiSelect)]
    public required string ProcedureName { get; init; }
    public required IReadOnlyList<SqlParameters> Parameters { get; init; }
}

/// <summary>
/// Событие начала выполнения хранимой процедуры.
/// </summary>
public sealed class ProcedureStartEvent : ProcedureEventBase;

/// <summary>
/// Событие завершения выполнения хранимой процедуры.
/// </summary>
public sealed class ProcedureFinishEvent : ProcedureEventBase
{
    public required PerformanceInfo Performance { get; init; }
    public PerformanceTable? PerformanceTable { get; init; }
}

public sealed class FailedProcedureFinishEvent : ProcedureEventBase
{
    public required PerformanceInfo Performance { get; init; }
    public PerformanceTable? PerformanceTable { get; init; }
}