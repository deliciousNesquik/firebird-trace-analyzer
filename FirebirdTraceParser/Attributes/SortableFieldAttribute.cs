namespace FirebirdTraceParser.Attributes;

/// <summary> Маркирует свойство как доступное для сортировки. </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SortableFieldAttribute(string displayName) : Attribute
{
    /// <summary>Отображаемое имя поля</summary>
    public string DisplayName { get; } = displayName ?? throw new ArgumentNullException(nameof(displayName));
    
    /// <summary>Категория сортировки (например, "Общие", "Производительность")</summary>
    public string Category { get; init; } = "General";

    /// <summary>Порядок отображения в списке сортировок (меньше = выше). Это UI-порядок, не логика.</summary>
    public int DisplayOrder { get; init; } = 100;

    /// <summary>Является ли сортировкой по умолчанию</summary>
    public bool IsDefault { get; init; } = false;
}