using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceParser.Models.Events;

/// <summary> Событие ошибки на уровне API Firebird (ERROR AT). </summary>
public sealed class ErrorEvent : EventBase
{
    public required AttachmentInfo Attachment { get; init; }
    
    /// <summary>Компонент, в котором произошла ошибка (например, "JResultSet::fetchNext")</summary>
    [SortableField("Error Component", Category = "Error")]
    [FilterableField("Error Component", Category = "Error", FilterType = FilterType.StringMultiSelect)]
    public required string Component { get; init; }
    
    /// <summary>Цепочка ошибок (может быть несколько строк "код: сообщение")</summary>
    [FilterableField("Error Codes", Category = "Error", FilterType = FilterType.StringMultiSelect)]
    public required IReadOnlyList<ErrorLines> Errors { get; init; }
}
