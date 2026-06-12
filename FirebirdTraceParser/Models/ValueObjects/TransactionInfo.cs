using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Parsing.Utils;

namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Информация о транзакции Firebird.
/// Соответствует Python TransactionInfo.
/// </summary>
public sealed record TransactionInfo
{
    [SortableField("Transaction ID", Category = "Transaction")]
    [FilterableField("Transaction ID", Category = "Transaction", FilterType =  FilterType.StringMultiSelect)]
    public long? TransactionId { get; init; }
    
    /// <summary>Уровень изоляции (READ_COMMITTED, SNAPSHOT, etc.)</summary>
    [SortableField("Isolation Level", Category = "Transaction")]
    [FilterableField("Isolation Level", Category = "Transaction", FilterType =  FilterType.StringMultiSelect)]
    public string? IsolationLevel { get; init; }
    
    
    /// <summary>Режим консистентности (READ_CONSISTENCY, etc.)</summary>
    [SortableField("Consistency Mode", Category = "Transaction")]
    [FilterableField("Consistency Mode", Category = "Transaction", FilterType =  FilterType.StringMultiSelect)]
    public string? ConsistencyMode { get; init; }
    
    /// <summary>Режим блокировки (WAIT, NOWAIT)</summary>
    [SortableField("Lock Mode", Category = "Transaction")]
    [FilterableField("Lock Mode", Category = "Transaction", FilterType =  FilterType.StringMultiSelect)]
    public string? LockMode { get; init; }
    
    /// <summary>Режим доступа (READ_WRITE, READ_ONLY)</summary>
    [SortableField("Access Mode", Category = "Transaction")]
    [FilterableField("Access Mode", Category = "Transaction", FilterType =  FilterType.StringMultiSelect)]
    public string? AccessMode { get; init; }
}