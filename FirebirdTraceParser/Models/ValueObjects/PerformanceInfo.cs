using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Общая информация о производительности выполнения SQL‑операции.
/// </summary>
public sealed record PerformanceInfo
{
    /// <summary>Время выполнения запроса в миллисекундах</summary>
    [SortableField("Execution Time (ms)", Category = "Performance")]
    [FilterableField("Execution Time (ms)", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int ExecuteMs { get; init; }
    
    /// <summary>Количество выданных записей (fetch)</summary>
    [SortableField("Count Of Fetch", Category = "Performance")]
    [FilterableField("Count Of Fetch", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int FetchCount { get; init; }
    
    /// <summary>Количество операций чтения</summary>
    [SortableField("Count Of Read", Category = "Performance")]
    [FilterableField("Count Of Read", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int ReadCount { get; init; }
    
    /// <summary>Количество операций записи</summary>
    [SortableField("Count Of Write", Category = "Performance")]
    [FilterableField("Count Of Write", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int WriteCount { get; init; }
    
    
    /// <summary>Количество внутренних отметок (mark)</summary>
    [SortableField("Count Of Mark", Category = "Performance")]
    [FilterableField("Count Of Mark", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int MarkCount { get; init; }
}