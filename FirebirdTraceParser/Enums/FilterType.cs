namespace FirebirdTraceParser.Enums;

/// <summary> Типы фильтров. </summary>
public enum FilterType
{
    /// <summary>Автоматическое определение по типу свойства</summary>
    Auto,
    
    /// <summary>Enum список чекбоксов</summary>
    EnumMultiSelect,
    
    /// <summary>String множественный выбор + поиск</summary>
    StringMultiSelect,
    
    /// <summary>Числовой диапазон (int, long, decimal)</summary>
    NumericRange,
    
    /// <summary>Диапазон дат</summary>
    DateTimeRange,
    
    /// <summary>Boolean переключатель</summary>
    Boolean,
    
    /// <summary>Полнотекстовый поиск (для SQL, длинных текстов)</summary>
    TextSearch
}