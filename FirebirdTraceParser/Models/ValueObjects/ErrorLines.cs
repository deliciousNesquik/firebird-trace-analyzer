namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Информация об одной ошибке в цепочке
/// </summary>
public sealed record ErrorLines
{
    public int ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;
}