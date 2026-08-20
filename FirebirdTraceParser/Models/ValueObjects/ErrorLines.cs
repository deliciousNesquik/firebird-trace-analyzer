using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Information about an error in the chain
/// </summary>
public sealed record ErrorLines
{
    /// <summary>
    /// Single-bit error code in the chain
    /// </summary>
    [FilterableField("Error Code", Category = "Error", FilterType = FilterType.EnumMultiSelect)]
    public int ErrorCode { get; init; }
    
    /// <summary>
    /// Error message from a single unit in the chain
    /// </summary>
    [FilterableField("Error Message", Category = "Error", FilterType = FilterType.TextSearch)]
    public string Message { get; init; } = string.Empty;

    /// <summary>Human-readable representation: "&lt;code&gt;: &lt;message&gt;".</summary>
    public override string ToString() => $"{ErrorCode}: {Message}";
}