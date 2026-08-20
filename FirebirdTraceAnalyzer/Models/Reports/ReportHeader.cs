namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Report header with meta-information.
/// </summary>
public sealed class ReportHeader
{
    /// <summary>Report title (templatable).</summary>
    public string Title { get; init; } = "Trace Analysis Report";

    /// <summary>Subtitle.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Whether to show the logo.</summary>
    public bool ShowLogo { get; init; } = true;

    /// <summary>Variables displayed in the header.</summary>
    public List<ReportVariable> Variables { get; init; } = new();

    /// <summary>Whether to show the generation date.</summary>
    public bool ShowGeneratedDate { get; init; } = true;

    /// <summary>Date format string.</summary>
    public string DateFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";
}