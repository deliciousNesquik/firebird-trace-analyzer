namespace FirebirdTraceAnalyzer.Enums.Reports;

/// <summary>
/// Defines the types of content that can be present in a report section.
/// </summary>
public enum SectionContentType
{
    /// <summary>
    /// The section contains a list of trace events and their details.
    /// </summary>
    Events,
    
    /// <summary>
    /// The section contains statistical data and analysis of the trace events.
    /// </summary>
    Statistics
}