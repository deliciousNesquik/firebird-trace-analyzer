using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceParser.Models.Events;

/// <summary> Базовая модель события трассировки Firebird. </summary>
public class EventBase
{
    /// <summary>Время события в формате ISO 8601</summary>
    [SortableField("Time", Category = "General", IsDefault = true)]
    [FilterableField("Time", Category = "General", FilterType = FilterType.DateTimeRange)]
    public required DateTime Timestamp { get; init; }
    
    /// <summary>Идентификатор trace‑сессии (decimal)</summary>
    [SortableField("Trace ID", Category = "General")]
    [FilterableField("Trace ID", Category = "General", FilterType =  FilterType.StringMultiSelect)]
    public required int TraceId { get; init; }
    
    /// <summary>Идентификатор trace‑сессии (hexadecimal)</summary>
    [SortableField("Hex Trace ID", Category = "General")]
    [FilterableField("Hex Trace ID", Category = "General", FilterType =  FilterType.StringMultiSelect)]
    public required string HexTraceId { get; init; }
    
    /// <summary>Тип события трассировки</summary>
    [SortableField("Type", Category = "General")]
    [FilterableField("Type", Category = "General", FilterType =  FilterType.EnumMultiSelect)]
    public required EventType EventType { get; init; }
}