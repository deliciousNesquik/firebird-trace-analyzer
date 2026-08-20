using System.Reflection;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Describes a field discovered on an event type (via reflection over the parser models).
/// </summary>
public sealed record DiscoveredField
{
    /// <summary>Dotted path to the property on the event (e.g. "Attachment.User", "Errors[].ErrorCode").</summary>
    public required string PropertyPath { get; init; }

    /// <summary>Human-readable field name shown in the UI.</summary>
    public required string DisplayName { get; init; }

    /// <summary>CLR type of the property.</summary>
    public required Type PropertyType { get; init; }

    /// <summary>Category the field is grouped under in the UI.</summary>
    public required string Category { get; init; }

    // Атрибуты поля
    /// <summary>Whether the field can be used for sorting.</summary>
    public bool IsSortable { get; init; }

    /// <summary>Whether the field can be used for filtering.</summary>
    public bool IsFilterable { get; init; }

    /// <summary>Preferred filter control type, when specified by the field attribute.</summary>
    public FilterType? FilterType { get; init; }

    /// <summary>Optional format string used when rendering the value.</summary>
    public string? Format { get; init; }

    /// <summary>Order in the filter list (from FilterableFieldAttribute.DisplayOrder; lower = higher).</summary>
    public int FilterDisplayOrder { get; init; } = 100;

    /// <summary>Order in the sort list (from SortableFieldAttribute.DisplayOrder; lower = higher).</summary>
    public int SortDisplayOrder { get; init; } = 100;

    /// <summary>Whether this field is the default sort.</summary>
    public bool IsDefaultSort { get; init; } = false;

    // Метаданные
    /// <summary>Reflection handle of the underlying property.</summary>
    public PropertyInfo PropertyInfo { get; init; } = null!;

    /// <summary>Type that declares the property.</summary>
    public Type DeclaringType { get; init; } = null!;
}
