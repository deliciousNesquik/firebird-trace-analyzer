using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Represents a field in an event report, including its name, display name,
/// property path, kind, aggregate function, format, width, order, and alignment.
/// </summary>
public sealed class EventField
{
    /// <summary>
    /// Title of the field
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Display name of the field (for UI)
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
    
    /// <summary>
    /// Path to the property in the event object (e.g., "Attachment.User")
    /// </summary>
    public string PropertyPath { get; init; } = string.Empty;

    /// <summary>
    /// Kind of the column (Field, Aggregate, etc.) <see cref="ColumnKind"/>
    /// </summary>
    public ColumnKind Kind { get; init; } = ColumnKind.Field;

    /// <summary>
    /// Aggregate function to apply to the field (if applicable) <see cref="AggregateFunction"/>
    /// </summary>
    public AggregateFunction? Aggregate { get; init; }

    /// <summary>
    /// Format string for displaying the field (e.g., "yyyy-MM-dd HH:mm:ss")
    /// </summary>
    public string? Format { get; init; }
    
    /// <summary>
    /// Width of the column in percent (0-100). If null, the width is auto-calculated.
    /// </summary>
    public int? WidthPercent { get; init; }
    
    /// <summary>
    /// Order of the column in the report (0-based index)
    /// </summary>
    public int Order { get; init; }
    
    /// <summary>
    /// Text alignment for the column <see cref="TextAlignment"/>
    /// </summary>
    public TextAlignment Alignment { get; init; } = TextAlignment.Left;
}