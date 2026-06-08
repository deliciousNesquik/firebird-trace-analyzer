using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceParser.Models.Enums;

namespace FirebirdTraceAnalyzer.Services.Reports.BuiltInTemplates;

/// <summary>
/// Встроенные шаблоны отчётов
/// </summary>
public static class BuiltInReportTemplates
{
    public static IReadOnlyList<ReportTemplate> GetAll()
    {
        return new List<ReportTemplate>
        {
            CreateTop5StatementsTemplate(),
            CreateTop10ProceduresTemplate(),
            CreateErrorReportTemplate()
        };
    }

    /// <summary>
    /// Шаблон "Top 5 Slowest Statements"
    /// </summary>
    private static ReportTemplate CreateTop5StatementsTemplate()
    {
        return new ReportTemplate
        {
            Id = "builtin_top5_statements",
            Name = "Top 5 Slowest Statements",
            Description = "Report showing the 5 slowest SQL statements",
            Author = "System",
            Category = ReportCategory.Quick,
            IsBuiltIn = true,
            
            Header = new ReportHeader
            {
                Title = "Top 5 Slowest SQL Statements",
                Subtitle = "Performance Analysis Report",
                ShowLogo = true,
                ShowGeneratedDate = true,
                Variables = new List<ReportVariable>
                {
                    new()
                    {
                        Type = ReportVariableType.FileNames,
                        DisplayName = "Analyzed Files",
                        TemplateKey = "{FILE_NAMES}",
                        IsVisible = true,
                        DisplayOrder = 1
                    },
                    new()
                    {
                        Type = ReportVariableType.TotalEventsCount,
                        DisplayName = "Total Events",
                        TemplateKey = "{TOTAL_EVENTS}",
                        Format = "N0",
                        IsVisible = true,
                        DisplayOrder = 2
                    },
                    new()
                    {
                        Type = ReportVariableType.FilteredEventsCount,
                        DisplayName = "Filtered Events",
                        TemplateKey = "{FILTERED_EVENTS}",
                        Format = "N0",
                        IsVisible = true,
                        DisplayOrder = 3
                    },
                    new()
                    {
                        Type = ReportVariableType.TraceDuration,
                        DisplayName = "Trace Duration",
                        TemplateKey = "{TRACE_DURATION}",
                        IsVisible = true,
                        DisplayOrder = 4
                    }
                }
            },
            
            Body = new ReportBody
            {
                DisplayStyle = EventDisplayStyle.Table,
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "Timestamp",
                        DisplayName = "Time",
                        PropertyPath = "Timestamp",
                        Format = "yyyy-MM-dd HH:mm:ss",
                        WidthPercent = 15,
                        Order = 1,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "User",
                        DisplayName = "User",
                        PropertyPath = "Attachment.User",
                        WidthPercent = 10,
                        Order = 2,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "ExecutionTime",
                        DisplayName = "Execution Time (ms)",
                        PropertyPath = "Performance.ExecuteMs",
                        Format = "N0",
                        WidthPercent = 12,
                        Order = 3,
                        Alignment = TextAlignment.Right
                    },
                    new()
                    {
                        Name = "SqlText",
                        DisplayName = "SQL Query",
                        PropertyPath = "Sql",
                        WidthPercent = 48,
                        Order = 4,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "ReadCount",
                        DisplayName = "Reads",
                        PropertyPath = "Performance.ReadCount",
                        Format = "N0",
                        WidthPercent = 10,
                        Order = 5,
                        Alignment = TextAlignment.Right
                    },
                    new()
                    {
                        Name = "WriteCount",
                        DisplayName = "Writes",
                        PropertyPath = "Performance.WriteCount",
                        Format = "N0",
                        WidthPercent = 10,
                        Order = 6,
                        Alignment = TextAlignment.Right
                    },
                    new()
                    {
                        Name = "FetchedCount",
                        DisplayName = "Fetches",
                        PropertyPath = "Performance.FetchCount",
                        Format = "N0",
                        WidthPercent = 8,
                        Order = 7,
                        Alignment = TextAlignment.Right
                    }
                },
                ShowSummary = true,
                Sections = new List<ReportSection>
                {
                    new()
                    {
                        Title = "Slowest Statements",
                        Description = "Top 5 statements sorted by execution time",
                        ContentType = SectionContentType.Events,
                        ShowTitle = true,
                        Order = 1
                    },
                    new()
                    {
                        Title = "Summary Statistics",
                        ContentType = SectionContentType.Statistics,
                        ShowTitle = true,
                        Order = 2
                    }
                }
            },
            
            Footer = new ReportFooter
            {
                Show = true,
                Text = "Generated by Firebird Trace Analyzer",
                ShowPageNumbers = true
            },
            
            Filters =
            [
                new ReportFilterConfig()
                {
                    DisplayName = "Event type",
                    FilterId = "filter_eventtype",
                    PropertyPath = "EventType",
                    IsActive = true,
                    SelectedValues = [EventType.ExecuteStatementFinish]
                }
            ],
            SortByField = "Performance.ExecuteMs",
            SortDescending = true,
            EventLimit = 5,
            
            SupportedFormats = new List<ReportFormat> { ReportFormat.PDF, ReportFormat.DOCX, ReportFormat.XLSX, ReportFormat.CSV },
            DefaultFormat = ReportFormat.PDF,
            
            Tags = new List<string> { "performance", "sql", "statements", "top5" }
        };
    }

    /// <summary>
    /// Шаблон "Top 10 Slowest Procedures"
    /// </summary>
    private static ReportTemplate CreateTop10ProceduresTemplate()
    {
        return new ReportTemplate
        {
            Id = "builtin_top10_procedures",
            Name = "Top 10 Slowest Procedures",
            Description = "Report showing the 10 slowest stored procedures",
            Author = "System",
            Category = ReportCategory.Quick,
            IsBuiltIn = true,
            
            Header = new ReportHeader
            {
                Title = "Top 10 Slowest Stored Procedures",
                Subtitle = "Performance Analysis Report",
                ShowLogo = true,
                ShowGeneratedDate = true,
                Variables = new List<ReportVariable>
                {
                    new()
                    {
                        Type = ReportVariableType.FileNames,
                        DisplayName = "Analyzed Files",
                        TemplateKey = "{FILE_NAMES}",
                        IsVisible = true,
                        DisplayOrder = 1
                    },
                    new()
                    {
                        Type = ReportVariableType.TotalEventsCount,
                        DisplayName = "Total Events",
                        TemplateKey = "{TOTAL_EVENTS}",
                        Format = "N0",
                        IsVisible = true,
                        DisplayOrder = 2
                    },
                    new()
                    {
                        Type = ReportVariableType.FilteredEventsCount,
                        DisplayName = "Filtered Events",
                        TemplateKey = "{FILTERED_EVENTS}",
                        Format = "N0",
                        IsVisible = true,
                        DisplayOrder = 3
                    }
                }
            },
            
            Body = new ReportBody
            {
                DisplayStyle = EventDisplayStyle.Table,
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "Timestamp",
                        DisplayName = "Time",
                        PropertyPath = "Timestamp",
                        Format = "yyyy-MM-dd HH:mm:ss",
                        WidthPercent = 18,
                        Order = 1,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "ProcedureName",
                        DisplayName = "Procedure Name",
                        PropertyPath = "ProcedureName",
                        WidthPercent = 30,
                        Order = 2,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "User",
                        DisplayName = "User",
                        PropertyPath = "Attachment.User",
                        WidthPercent = 12,
                        Order = 3,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "ExecutionTime",
                        DisplayName = "Execution Time (ms)",
                        PropertyPath = "Performance.ExecuteMs",
                        Format = "N0",
                        WidthPercent = 15,
                        Order = 4,
                        Alignment = TextAlignment.Right
                    },
                    new()
                    {
                        Name = "ReadCount",
                        DisplayName = "Reads",
                        PropertyPath = "Performance.ReadCount",
                        Format = "N0",
                        WidthPercent = 10,
                        Order = 5,
                        Alignment = TextAlignment.Right
                    },
                    new()
                    {
                        Name = "WriteCount",
                        DisplayName = "Writes",
                        PropertyPath = "Performance.WriteCount",
                        Format = "N0",
                        WidthPercent = 10,
                        Order = 6,
                        Alignment = TextAlignment.Right
                    }
                },
                ShowSummary = true,
                Sections = new List<ReportSection>
                {
                    new()
                    {
                        Title = "Slowest Procedures",
                        Description = "Top 10 procedures sorted by execution time",
                        ContentType = SectionContentType.Events,
                        ShowTitle = true,
                        Order = 1
                    },
                    new()
                    {
                        Title = "Summary Statistics",
                        ContentType = SectionContentType.Statistics,
                        ShowTitle = true,
                        Order = 2
                    }
                }
            },
            
            Footer = new ReportFooter
            {
                Show = true,
                Text = "Generated by Firebird Trace Analyzer",
                ShowPageNumbers = true
            },
            
            Filters =
            [
                new ReportFilterConfig()
                {
                    DisplayName = "Event type",
                    FilterId = "filter_eventtype",
                    PropertyPath = "EventType",
                    IsActive = true,
                    SelectedValues = [EventType.ExecuteProcedureFinish]
                }
            ],
            SortByField = "Performance.ExecuteMs",
            SortDescending = true,
            EventLimit = 10,
            
            SupportedFormats = new List<ReportFormat> { ReportFormat.PDF, ReportFormat.DOCX, ReportFormat.XLSX, ReportFormat.CSV },
            DefaultFormat = ReportFormat.PDF,
            
            Tags = new List<string> { "performance", "procedures", "top10" }
        };
    }

    /// <summary>
    /// Шаблон "Error Report"
    /// </summary>
    private static ReportTemplate CreateErrorReportTemplate()
    {
        return new ReportTemplate
        {
            Id = "builtin_error_report",
            Name = "Error Report",
            Description = "Summary of all errors occurred during trace",
            Author = "System",
            Category = ReportCategory.Quick,
            IsBuiltIn = true,
            
            Header = new ReportHeader
            {
                Title = "Error Report",
                Subtitle = "Database Errors Analysis",
                ShowLogo = true,
                ShowGeneratedDate = true,
                Variables = new List<ReportVariable>
                {
                    new()
                    {
                        Type = ReportVariableType.FileNames,
                        DisplayName = "Analyzed Files",
                        TemplateKey = "{FILE_NAMES}",
                        IsVisible = true,
                        DisplayOrder = 1
                    },
                    new()
                    {
                        Type = ReportVariableType.FilteredEventsCount,
                        DisplayName = "Total Errors",
                        TemplateKey = "{FILTERED_EVENTS}",
                        Format = "N0",
                        IsVisible = true,
                        DisplayOrder = 2
                    }
                }
            },
            
            Body = new ReportBody
            {
                DisplayStyle = EventDisplayStyle.DetailedList,
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "Timestamp",
                        DisplayName = "Time",
                        PropertyPath = "Timestamp",
                        Format = "yyyy-MM-dd HH:mm:ss",
                        Order = 1,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "Component",
                        DisplayName = "Component",
                        PropertyPath = "Component",
                        Order = 2,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "User",
                        DisplayName = "User",
                        PropertyPath = "Attachment.User",
                        Order = 3,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "Errors messages",
                        DisplayName = "Errors messages",
                        PropertyPath = "Errors",
                        Order = 4,
                        Alignment = TextAlignment.Left
                    }
                },
                ShowSummary = true,
                Sections = new List<ReportSection>
                {
                    new()
                    {
                        Title = "Errors",
                        Description = "All errors occurred during the trace period",
                        ContentType = SectionContentType.Events,
                        ShowTitle = true,
                        Order = 1
                    }
                }
            },
            
            Footer = new ReportFooter
            {
                Show = true,
                Text = "Generated by Firebird Trace Analyzer",
                ShowPageNumbers = true
            },
            
            Filters =
            [
                new ReportFilterConfig()
                {
                    DisplayName = "Event type",
                    FilterId = "filter_eventtype",
                    PropertyPath = "EventType",
                    IsActive = true,
                    SelectedValues = [EventType.Error]
                }
            ],
            SortByField = "Timestamp",
            SortDescending = false,
            EventLimit = 10,
            
            SupportedFormats = new List<ReportFormat> { ReportFormat.PDF, ReportFormat.DOCX, ReportFormat.CSV },
            DefaultFormat = ReportFormat.PDF,
            
            Tags = new List<string> { "errors", "troubleshooting" }
        };
    }
}