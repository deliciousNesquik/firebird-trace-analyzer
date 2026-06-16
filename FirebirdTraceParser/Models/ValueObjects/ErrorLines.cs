namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Информация об одной ошибке в цепочке
/// </summary>
public sealed record ErrorLines
{
    public int ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;

    /// <summary>Человекочитаемое представление: "&lt;код&gt;: &lt;сообщение&gt;".</summary>
    public override string ToString() => $"{ErrorCode}: {Message}";
}