using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Represents a generated report with its associated metadata, template, format, file path, and size.
/// </summary>
public sealed class GeneratedReport
{
    /// <summary>
    /// Used template for report generation
    /// </summary>
    public required ReportTemplate Template { get; init; }
    
    /// <summary>
    /// Metadata of the generated report
    /// </summary>
    public required ReportMetadata Metadata { get; init; }
    
    /// <summary>
    /// Format of the generated report
    /// </summary>
    public required ReportFormat Format { get; init; }
    
    /// <summary>
    /// Path to the generated report file
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// Size of the generated report file in bytes
    /// </summary>
    public long FileSize { get; init; }
    
    /// <summary>
    /// Date and time when the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
}