using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Информация о глобальной сессии трассировки Firebird.
/// </summary>
public sealed record TraceSessionInfo
{
    /// <summary>Идентификатор глобальной trace‑сессии</summary>
    [SortableField("Session ID", Priority = 2, Category = "Global")]
    [FilterableField("Session ID", Category = "Global", FilterType =  FilterType.StringMultiSelect)]
    public required int SessionId { get; init; }
}