using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Общая информация о производительности выполнения SQL‑операции.
/// </summary>
public sealed record PerformanceInfo
{
    /// <summary>Время выполнения запроса в миллисекундах</summary>
    [SortableField("Execution time (ms)", Category = "Performance")]
    [FilterableField("Execution time (ms)", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int ExecuteMs { get; init; }
    
    /// <summary>Количество выданных записей (fetch)</summary>
    [SortableField("Count of fetch", Category = "Performance")]
    [FilterableField("Count of fetch", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int FetchCount { get; init; }
    
    /// <summary>Количество операций чтения</summary>
    [SortableField("Count of write", Category = "Performance")]
    [FilterableField("Count of write", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int ReadCount { get; init; }
    
    /// <summary>Количество операций записи</summary>
    [SortableField("Count of write", Category = "Performance")]
    [FilterableField("Count of write", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int WriteCount { get; init; }
    
    
    /// <summary>Количество внутренних отметок (mark)</summary>
    [SortableField("Count of mark", Category = "Performance")]
    [FilterableField("Count of mark", Category = "Performance", FilterType = FilterType.NumericRange)]
    public required int MarkCount { get; init; }
}