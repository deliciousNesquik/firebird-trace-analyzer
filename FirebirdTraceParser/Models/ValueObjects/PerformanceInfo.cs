using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Общая информация о производительности выполнения SQL‑операции.
/// </summary>
public sealed record PerformanceInfo
{
    /// <summary>Время выполнения запроса в миллисекундах</summary>
    [SortableField("Execution time (ms)", Priority = 30, Category = "Performance")]
    [FilterableField("Execution time (ms)", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int ExecuteMs { get; init; }
    
    /// <summary>Количество выданных записей (fetch)</summary>
    [SortableField("Count of fetch", Priority = 31, Category = "Performance")]
    [FilterableField("Count of fetch", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int FetchCount { get; init; }
    
    /// <summary>Количество операций чтения</summary>
    [SortableField("Count of write", Priority = 32, Category = "Performance")]
    [FilterableField("Count of write", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int ReadCount { get; init; }
    
    /// <summary>Количество операций записи</summary>
    [SortableField("Count of write", Priority = 33, Category = "Performance")]
    [FilterableField("Count of write", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int WriteCount { get; init; }
    
    
    /// <summary>Количество внутренних отметок (mark)</summary>
    [SortableField("Count of mark", Priority = 33, Category = "Performance")]
    [FilterableField("Count of mark", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int MarkCount { get; init; }
}