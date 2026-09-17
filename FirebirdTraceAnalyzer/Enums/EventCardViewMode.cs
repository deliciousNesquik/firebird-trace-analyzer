namespace FirebirdTraceAnalyzer.Enums;

/// <summary>
///     Режим отображения списка событий трассировки.
/// </summary>
public enum EventCardViewMode
{
    /// <summary>Детальные карточки со всеми полями (режим по умолчанию).</summary>
    Full = 0,

    /// <summary>Плотный список: одна строка на событие.</summary>
    Compact = 1
}
