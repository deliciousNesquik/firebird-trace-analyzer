using FirebirdTraceParser.Enums;

namespace FirebirdTraceParser.Attributes;

/// <summary> Маркирует свойство как доступное для фильтрации. </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class FilterableFieldAttribute(string displayName) : Attribute
{
    /// <summary>Отображаемое имя поля</summary>
    public string DisplayName { get; } = displayName ?? throw new ArgumentNullException(nameof(displayName));

    /// <summary>Приоритет отображения (меньше = выше)</summary>
    public int Priority { get; init; } = 100;
    
    /// <summary>Категория фильтра (например, "Общие", "Подключение")</summary>
    public string Category { get; init; } = "General";
    
    /// <summary>Тип фильтра</summary>
    public FilterType FilterType { get; init; } = FilterType.Auto;
}