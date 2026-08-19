namespace FirebirdTraceAnalyzer.Enums.Reports;

/// <summary>
/// Represents the types of variables that can be used in reports.
/// </summary>
public enum ReportVariableType
{
    #region Files

    /// <summary>
    /// File names loaded in report
    /// </summary>
    FileNames,
    
    /// <summary>
    /// Full paths of files loaded in report
    /// </summary>
    FilePaths,
    
    /// <summary>
    /// Count of files loaded in report
    /// </summary>
    FileCount,
    
    /// <summary>
    /// Total size of files loaded in report
    /// </summary>
    FileSizeTotal,

    #endregion

    #region Events

    /// <summary>
    /// Total count of events in report
    /// </summary>
    TotalEventsCount,
    
    /// <summary>
    /// Count of events after filtering in report
    /// </summary>
    FilteredEventsCount,
    
    /// <summary>
    /// Count of events after sorting/limiting in report
    /// </summary>
    VisibleEventsCount,

    #endregion

    #region Time Ranges

    /// <summary>
    /// Start time of the trace
    /// </summary>
    TraceStartTime,
    
    /// <summary>
    /// End time of the trace
    /// </summary>
    TraceEndTime,
    
    /// <summary>
    /// Total duration of the trace
    /// </summary>
    TraceDuration,

    #endregion

    #region Filters & Sorting

    /// <summary>
    /// Active filters applied to the report
    /// </summary>
    ActiveFilters,
    
    /// <summary>
    /// Active sorting applied to the report
    /// </summary>
    ActiveSort,

    #endregion

    #region Statistics

    /// <summary>
    /// Average execution time of events in the report
    /// </summary>
    AverageExecutionTime,
    
    /// <summary>
    /// Maximum execution time of events in the report
    /// </summary>
    MaxExecutionTime,
    
    /// <summary>
    /// Minimum execution time of events in the report
    /// </summary>
    MinExecutionTime,

    #endregion

    #region Meta

    /// <summary>
    /// Date when the report was generated
    /// </summary>
    GeneratedDate,
    
    /// <summary>
    /// Name of the user who generated the report
    /// </summary>
    GeneratedBy,
    
    /// <summary>
    /// Version of the application that generated the report
    /// </summary>
    ApplicationVersion

    #endregion
    
}