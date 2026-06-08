using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceParser.Models.Events;

/// <summary>
/// Базовая модель события трассировки Firebird.
/// </summary>
public class EventBase
{
    /// <summary>Время события в формате ISO 8601</summary>
    [SortableField("Event time", Priority = 1, Category = "General", IsDefault = true)]
    [FilterableField("Event time", Category = "General", FilterType = FilterType.DateTimeRange)]
    public required DateTime Timestamp { get; init; }
    
    /// <summary>Идентификатор trace‑сессии (decimal)</summary>
    [SortableField("Trace ID", Priority = 2, Category = "General")]
    [FilterableField("Trace ID", Category = "General", FilterType =  FilterType.StringMultiSelect)]
    public required int TraceId { get; init; }
    
    /// <summary>Идентификатор trace‑сессии (hexadecimal)</summary>
    [SortableField("Hex Trace ID", Priority = 3, Category = "General")]
    [FilterableField("Hex Trace ID", Category = "General", FilterType =  FilterType.StringMultiSelect)]
    public required string HexTraceId { get; init; }
    
    /// <summary>Тип события трассировки</summary>
    [SortableField("Event type", Priority = 4, Category = "General")]
    [FilterableField("Event type", Category = "General", FilterType =  FilterType.EnumMultiSelect)]
    public required EventType EventType { get; init; }
}