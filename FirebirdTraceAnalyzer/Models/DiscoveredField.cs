using System.Reflection;
using FirebirdTraceParser.Attributes;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Информация об обнаруженном поле события
/// </summary>
public sealed record DiscoveredField
{
    public required string PropertyPath { get; init; }
    public required string DisplayName { get; init; }
    public required Type PropertyType { get; init; }
    public required string Category { get; init; }
    public required int Priority { get; init; }
    
    // Атрибуты поля
    public bool IsSortable { get; init; }
    public bool IsFilterable { get; init; }
    public FilterType? FilterType { get; init; }
    public string? Format { get; init; }
    
    // Метаданные
    public PropertyInfo PropertyInfo { get; init; } = null!;
    public Type DeclaringType { get; init; } = null!;
}