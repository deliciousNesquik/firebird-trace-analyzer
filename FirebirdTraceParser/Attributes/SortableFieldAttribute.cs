namespace FirebirdTraceParser.Attributes;

/// <summary> Маркирует свойство как доступное для сортировки. </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SortableFieldAttribute(string displayName) : Attribute
{
    /// <summary>Отображаемое имя поля</summary>
    public string DisplayName { get; } = displayName ?? throw new ArgumentNullException(nameof(displayName));
    
    /// <summary>Категория сортировки (например, "Общие", "Производительность")</summary>
    public string Category { get; init; } = "General";

    /// <summary>Является ли сортировкой по умолчанию</summary>
    public bool IsDefault { get; init; } = false;
}