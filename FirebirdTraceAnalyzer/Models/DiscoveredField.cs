using System.Reflection;
using FirebirdTraceParser.Enums;

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
    
    // Атрибуты поля
    public bool IsSortable { get; init; }
    public bool IsFilterable { get; init; }
    public FilterType? FilterType { get; init; }
    public string? Format { get; init; }

    /// <summary>Порядок в списке фильтров (из FilterableFieldAttribute.DisplayOrder; меньше = выше).</summary>
    public int FilterDisplayOrder { get; init; } = 100;

    /// <summary>Порядок в списке сортировок (из SortableFieldAttribute.DisplayOrder; меньше = выше).</summary>
    public int SortDisplayOrder { get; init; } = 100;

    /// <summary>Флаг, определяющий сортировку по умолчанию</summary>
    public bool IsDefaultSort { get; init; } = false;
    
    // Метаданные
    public PropertyInfo PropertyInfo { get; init; } = null!;
    public Type DeclaringType { get; init; } = null!;
}