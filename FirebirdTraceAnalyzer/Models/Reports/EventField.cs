using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Поле события для отображения в отчёте
/// </summary>
public sealed class EventField
{
    /// <summary>Название поля</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Отображаемое название</summary>
    public string DisplayName { get; init; } = string.Empty;
    
    /// <summary>Путь к свойству (например, "Performance.ExecuteMs")</summary>
    public string PropertyPath { get; init; } = string.Empty;
    
    /// <summary>Форматирование (например, "{0:N0} ms")</summary>
    public string? Format { get; init; }
    
    /// <summary>Ширина колонки (для таблиц, в %)</summary>
    public int? WidthPercent { get; init; }
    
    /// <summary>Порядок отображения</summary>
    public int Order { get; init; }
    
    /// <summary>Выравнивание</summary>
    public TextAlignment Alignment { get; init; } = TextAlignment.Left;
}