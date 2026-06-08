using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceAnalyzer.Services.Filtering;

/// <summary>
/// Метаданные поля, доступного для фильтрации.
/// </summary>
public sealed class FilterFieldInfo
{
    /// <summary>Путь к свойству (например, "Timestamp", "Attachment.User")</summary>
    public string PropertyPath { get; }
    
    /// <summary>Отображаемое имя</summary>
    public string DisplayName { get; }
    
    /// <summary>Категория</summary>
    public string Category { get; }
    
    /// <summary>Приоритет</summary>
    public int Priority { get; }
    
    /// <summary>Тип свойства</summary>
    public Type PropertyType { get; }
    
    /// <summary>Тип фильтра</summary>
    public FilterType FilterType { get; }

    public FilterFieldInfo(
        string propertyPath,
        string displayName,
        Type propertyType,
        string category,
        int priority,
        FilterType filterType)
    {
        PropertyPath = propertyPath;
        DisplayName = displayName;
        PropertyType = propertyType;
        Category = category;
        Priority = priority;
        FilterType = filterType;
    }
}