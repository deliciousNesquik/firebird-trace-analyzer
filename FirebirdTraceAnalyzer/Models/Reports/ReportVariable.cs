using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// A variable available for use in a report.
/// </summary>
public sealed class ReportVariable
{
    /// <summary>The variable type.</summary>
    public ReportVariableType Type { get; init; }

    /// <summary>The variable's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Templating key (e.g. {FILE_NAMES}).</summary>
    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>Value format string.</summary>
    public string? Format { get; init; }

    /// <summary>Whether the variable is shown in the report.</summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>Display ordering position.</summary>
    public int DisplayOrder { get; init; }
}