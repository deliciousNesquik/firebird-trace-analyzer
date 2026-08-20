using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// A report section.
/// </summary>
public sealed class ReportSection
{
    /// <summary>The section title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The section description.</summary>
    public string? Description { get; init; }

    /// <summary>The kind of content the section holds.</summary>
    public SectionContentType ContentType { get; init; }

    /// <summary>Whether to render the section title.</summary>
    public bool ShowTitle { get; init; } = true;

    /// <summary>The section's ordering position.</summary>
    public int Order { get; init; }
}