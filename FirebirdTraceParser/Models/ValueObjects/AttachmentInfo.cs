using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Parsing.Utils;

namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Информация о подключении к базе данных Firebird.
/// </summary>
public sealed class AttachmentInfo
{
    [FilterableField("Database path", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public required string DatabasePath { get; init; }

    [SortableField("Attachment ID", Category = "Attachment")]
    [FilterableField("Attachment ID", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public required long AttachmentId { get; init; }
    
    [SortableField("User", Category = "Attachment")]
    [FilterableField("User", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public required string User { get; init; }

    [SortableField("Role", Category = "Attachment")]
    [FilterableField("Role", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public required string Role { get; init; }

    [SortableField("Charset", Category = "Attachment")]
    [FilterableField("Charset", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public required string Charset { get; init; }

    [SortableField("Protocol", Category = "Attachment")]
    [FilterableField("Protocol", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public required string Protocol { get; init; }

    [SortableField("Address", Category = "Attachment")]
    [FilterableField("Address", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public required string Address { get; init; }
    
    [SortableField("Port", Category = "Attachment")]
    [FilterableField("Port", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public required int Port { get; init; }

    
    /// <summary>Путь к исполняемому файлу клиента (опционально)</summary>
    [FilterableField("Client Application", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public string? ProcessPath { get; init; }
    
    /// <summary>ID процесса клиента (опционально)</summary>
    [FilterableField("Client application PID", Category = "Attachment", FilterType =  FilterType.StringMultiSelect)]
    public int? ProcessId { get; init; }
}