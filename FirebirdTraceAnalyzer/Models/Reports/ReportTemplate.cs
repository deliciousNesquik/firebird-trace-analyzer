using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// A report template.
/// </summary>
public sealed class ReportTemplate
{
    /// <summary>Unique template identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Template name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Template description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Template author.</summary>
    public string Author { get; init; } = Environment.UserName;

    /// <summary>Creation date.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.Now;

    /// <summary>Last-modified date.</summary>
    public DateTime ModifiedAt { get; init; } = DateTime.Now;

    /// <summary>Template version.</summary>
    public string Version { get; init; } = "1.0";

    /// <summary>Report header settings.</summary>
    public ReportHeader Header { get; init; } = new();

    /// <summary>Report body settings.</summary>
    public ReportBody Body { get; init; } = new();

    /// <summary>Report footer settings.</summary>
    public ReportFooter Footer { get; init; } = new();

    /// <summary>Supported export formats (Pdf, Docx, Xlsx, Csv).</summary>
    public List<ReportFormat> SupportedFormats { get; init; } = new();

    /// <summary>Default export format.</summary>
    public ReportFormat DefaultFormat { get; init; } = ReportFormat.Pdf;

    /// <summary>Filters to apply to events (optional).</summary>
    public List<ReportFilterConfig>? Filters { get; init; }

    /// <summary>Field to sort by.</summary>
    public string? SortByField { get; init; }

    /// <summary>Whether to sort in descending order.</summary>
    public bool SortDescending { get; init; } = true;

    /// <summary>Event limit (for Top N reports).</summary>
    public int? EventLimit { get; init; }

    /// <summary>Tags used for search.</summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>Whether this is a built-in template.</summary>
    public bool IsBuiltIn { get; init; }
}