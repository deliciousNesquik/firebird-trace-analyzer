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
    public int? TransactionId { get; init; }
    
    /// <summary>Уровень изоляции (READ_COMMITTED, SNAPSHOT, etc.)</summary>
    [SortableField("Isolation level", Category = "Transaction")]
    [FilterableField("Isolation level", Category = "Transaction", FilterType =  FilterType.StringMultiSelect)]
    public string? IsolationLevel { get; init; }
    
    
    /// <summary>Режим консистентности (READ_CONSISTENCY, etc.)</summary>
    [SortableField("Consistency mode", Category = "Transaction")]
    [FilterableField("Consistency mode", Category = "Transaction", FilterType =  FilterType.StringMultiSelect)]
    public string? ConsistencyMode { get; init; }
    
    /// <summary>Режим блокировки (WAIT, NOWAIT)</summary>
    [SortableField("Lock mode", Category = "Transaction")]
    [FilterableField("Lock mode", Category = "Transaction", FilterType =  FilterType.StringMultiSelect)]
    public string? LockMode { get; init; }
    
    /// <summary>Режим доступа (READ_WRITE, READ_ONLY)</summary>
    [SortableField("Access mode", Category = "Transaction")]
    [FilterableField("Access mode", Category = "Transaction", FilterType =  FilterType.StringMultiSelect)]
    public string? AccessMode { get; init; }
}