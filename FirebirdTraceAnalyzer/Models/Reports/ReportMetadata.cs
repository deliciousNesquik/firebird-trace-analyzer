using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Метаданные для генерации отчёта (runtime данные)
/// </summary>
public sealed class ReportMetadata
{
    /// <summary>События для отчёта</summary>
    public required IReadOnlyList<EventBase> Events { get; init; }
    
    /// <summary>Информация о файлах</summary>
    public required IReadOnlyList<TraceFileInfoModel> Files { get; init; }
    
    /// <summary>Всего событий (до фильтрации)</summary>
    public required long TotalEventsCount { get; init; }
    
    /// <summary>Активные фильтры (текстовое описание)</summary>
    public string? ActiveFilters { get; init; }
    
    /// <summary>Активная сортировка (текстовое описание)</summary>
    public string? ActiveSort { get; init; }
    
    /// <summary>Дата генерации отчёта</summary>
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    
    /// <summary>Версия приложения</summary>
    public string ApplicationVersion { get; init; } = "1.0.0";
}