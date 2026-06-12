using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceParser.Models.Events;

/// <summary>
/// Базовый класс для событий триггеров.
/// </summary>
public class TriggerEventBase : EventBase
{
    public required AttachmentInfo Attachment { get; init; }
    public required TransactionInfo Transaction { get; init; }
    
    [SortableField("Trigger Name", Category = "Triggers")]
    [FilterableField("Trigger Name", Category = "Triggers", FilterType = FilterType.StringMultiSelect)]
    public required string TriggerName { get; init; }
    
    [SortableField("Table Name", Category = "Triggers")]
    [FilterableField("Table Name", Category = "Triggers", FilterType = FilterType.StringMultiSelect)]
    public string? Table { get; init; } // ← убрали 'required'
    
    [SortableField("Timing", Category = "Triggers")]
    [FilterableField("Timing", Category = "Triggers", FilterType = FilterType.StringMultiSelect)]
    public string? Timing { get; init; } // ← убрали 'required'
    
    [SortableField("Event", Category = "Triggers")]
    [FilterableField("Event", Category = "Triggers", FilterType = FilterType.StringMultiSelect)]
    public required string Event { get; init; } // ← оставляем required, но изменим логику
}

/// <summary>
/// Событие начала выполнения триггера.
/// </summary>
public sealed class TriggerStartEvent : TriggerEventBase;

/// <summary>
/// Событие завершения выполнения триггера.
/// </summary>
public sealed class TriggerFinishEvent : TriggerEventBase
{
    public required PerformanceInfo Performance { get; init; }
    public PerformanceTable? PerformanceTable { get; init; }
}

public sealed class FailedTriggerFinishEvent : TriggerEventBase
{
    public required PerformanceInfo Performance { get; init; }
    public PerformanceTable? PerformanceTable { get; init; }
}