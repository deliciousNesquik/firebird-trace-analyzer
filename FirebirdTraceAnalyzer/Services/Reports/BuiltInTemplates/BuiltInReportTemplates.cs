using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Localization;
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
            CreateErrorReportTemplate(),
            CreateErrorsByUserTemplate(),
            CreateSqlSumExecuteTimeTemplate(),
            CreateProcedureSumExecuteTimeTemplate(),
            CreateSqlCallCountTemplate(),
            CreateProcedureCallCountTemplate()
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
            Name = Loc.Tr("Report.BuiltIn.Top5Statements.Name"),
            Description = Loc.Tr("Report.BuiltIn.Top5Statements.Description"),
            Author = "System",
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
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "Timestamp",
                        DisplayName = "Time",
                        PropertyPath = "Timestamp",
                        Format = "yyyy-MM-dd HH:mm:ss",
                        WidthPercent = 13,
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
                        WidthPercent = 28,
                        Order = 4,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "Parameters",
                        DisplayName = "Parameters",
                        PropertyPath = "Parameters",
                        WidthPercent = 12,
                        Order = 5,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "ReadCount",
                        DisplayName = "Reads",
                        PropertyPath = "Performance.ReadCount",
                        Format = "N0",
                        WidthPercent = 9,
                        Order = 6,
                        Alignment = TextAlignment.Right
                    },
                    new()
                    {
                        Name = "WriteCount",
                        DisplayName = "Writes",
                        PropertyPath = "Performance.WriteCount",
                        Format = "N0",
                        WidthPercent = 8,
                        Order = 7,
                        Alignment = TextAlignment.Right
                    },
                    new()
                    {
                        Name = "FetchedCount",
                        DisplayName = "Fetches",
                        PropertyPath = "Performance.FetchCount",
                        Format = "N0",
                        WidthPercent = 8,
                        Order = 8,
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
            
            SupportedFormats = new List<ReportFormat> { ReportFormat.Pdf, ReportFormat.Docx, ReportFormat.Xlsx, ReportFormat.Csv },
            DefaultFormat = ReportFormat.Pdf,
            
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
            Name = Loc.Tr("Report.BuiltIn.Top10Procedures.Name"),
            Description = Loc.Tr("Report.BuiltIn.Top10Procedures.Description"),
            Author = "System",
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
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "Timestamp",
                        DisplayName = "Time",
                        PropertyPath = "Timestamp",
                        Format = "yyyy-MM-dd HH:mm:ss",
                        WidthPercent = 12,
                        Order = 1,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "ProcedureName",
                        DisplayName = "Procedure Name",
                        PropertyPath = "ProcedureName",
                        WidthPercent = 26,
                        Order = 2,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "Parameters",
                        DisplayName = "Parameters",
                        PropertyPath = "Parameters",
                        WidthPercent = 20,
                        Order = 3,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "User",
                        DisplayName = "User",
                        PropertyPath = "Attachment.User",
                        WidthPercent = 10,
                        Order = 4,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "ExecutionTime",
                        DisplayName = "Execution Time (ms)",
                        PropertyPath = "Performance.ExecuteMs",
                        Format = "N0",
                        WidthPercent = 14,
                        Order = 5,
                        Alignment = TextAlignment.Right
                    },
                    new()
                    {
                        Name = "ReadCount",
                        DisplayName = "Reads",
                        PropertyPath = "Performance.ReadCount",
                        Format = "N0",
                        WidthPercent = 9,
                        Order = 6,
                        Alignment = TextAlignment.Right
                    },
                    new()
                    {
                        Name = "WriteCount",
                        DisplayName = "Writes",
                        PropertyPath = "Performance.WriteCount",
                        Format = "N0",
                        WidthPercent = 9,
                        Order = 7,
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
            
            SupportedFormats = new List<ReportFormat> { ReportFormat.Pdf, ReportFormat.Docx, ReportFormat.Xlsx, ReportFormat.Csv },
            DefaultFormat = ReportFormat.Pdf,
            
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
            Name = Loc.Tr("Report.BuiltIn.FirstErrors.Name"),
            Description = Loc.Tr("Report.BuiltIn.FirstErrors.Description"),
            Author = "System",
            IsBuiltIn = true,
            
            Header = new ReportHeader
            {
                Title = "First 10 Errors",
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
            
            SupportedFormats = new List<ReportFormat> { ReportFormat.Pdf, ReportFormat.Docx, ReportFormat.Csv },
            DefaultFormat = ReportFormat.Pdf,
            
            Tags = new List<string> { "errors", "troubleshooting" }
        };
    }

    /// <summary>
    /// Шаблон "Errors by User" — количество ошибок в разрезе пользователей.
    /// Демонстрирует агрегацию: GROUP BY Attachment.User + COUNT.
    /// </summary>
    private static ReportTemplate CreateErrorsByUserTemplate()
    {
        return new ReportTemplate
        {
            Id = "builtin_errors_by_user",
            Name = Loc.Tr("Report.BuiltIn.ErrorsByUser.Name"),
            Description = Loc.Tr("Report.BuiltIn.ErrorsByUser.Description"),
            Author = "System",
            IsBuiltIn = true,

            Header = new ReportHeader
            {
                Title = "Errors by User",
                Subtitle = "Aggregated error counts",
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
                // Группировка по пользователю — таблица строится "строка на группу".
                GroupByFields = new List<string> { "Attachment.User" },
                // Сортируем результат по числу ошибок (см. SortDescending = true ниже).
                SortByColumn = "Error count",
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "User",
                        DisplayName = "User",
                        PropertyPath = "Attachment.User",
                        Kind = ColumnKind.GroupKey,
                        WidthPercent = 60,
                        Order = 1,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "ErrorCount",
                        DisplayName = "Error count",
                        Kind = ColumnKind.Aggregate,
                        Aggregate = AggregateFunction.Count,
                        Format = "N0",
                        WidthPercent = 40,
                        Order = 2,
                        Alignment = TextAlignment.Right
                    }
                },
                ShowSummary = true,
                Sections = new List<ReportSection>
                {
                    new()
                    {
                        Title = "Errors by User",
                        Description = "Error count per user",
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
                    SelectedValues = [EventType.Error]
                }
            ],
            // Сортировка результата — по убыванию (по колонке Error count). Лимит по событиям не задаём.
            SortDescending = true,

            SupportedFormats = new List<ReportFormat> { ReportFormat.Pdf, ReportFormat.Docx, ReportFormat.Xlsx, ReportFormat.Csv },
            DefaultFormat = ReportFormat.Pdf,

            Tags = new List<string> { "errors", "aggregate", "by-user" }
        };
    }

    /// <summary>
    /// Шаблон "SQL by Total Execution Time" — суммарное время выполнения в разрезе SQL-текста
    /// (GROUP BY Sql + SUM(Performance.ExecuteMs), сортировка по убыванию суммы).
    /// </summary>
    private static ReportTemplate CreateSqlSumExecuteTimeTemplate()
    {
        return new ReportTemplate
        {
            Id = "builtin_sql_sum_execute_time",
            Name = Loc.Tr("Report.BuiltIn.SqlSumExecuteTime.Name"),
            Description = Loc.Tr("Report.BuiltIn.SqlSumExecuteTime.Description"),
            Author = "System",
            IsBuiltIn = true,

            Header = new ReportHeader
            {
                Title = "SQL by Total Execution Time",
                Subtitle = "Aggregated execution time per SQL statement",
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
                    }
                }
            },

            Body = new ReportBody
            {
                GroupByFields = new List<string> { "Sql" },
                SortByColumn = "Total time (ms)",
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "Sql",
                        DisplayName = "SQL",
                        PropertyPath = "Sql",
                        Kind = ColumnKind.GroupKey,
                        WidthPercent = 70,
                        Order = 1,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "TotalExecuteMs",
                        DisplayName = "Total time (ms)",
                        PropertyPath = "Performance.ExecuteMs",
                        Kind = ColumnKind.Aggregate,
                        Aggregate = AggregateFunction.Sum,
                        Format = "N0",
                        WidthPercent = 30,
                        Order = 2,
                        Alignment = TextAlignment.Right
                    }
                },
                ShowSummary = true,
                Sections = new List<ReportSection>
                {
                    new()
                    {
                        Title = "SQL by Total Execution Time",
                        Description = "Total execution time per SQL statement",
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
            SortDescending = true,

            SupportedFormats = new List<ReportFormat> { ReportFormat.Pdf, ReportFormat.Docx, ReportFormat.Xlsx, ReportFormat.Csv },
            DefaultFormat = ReportFormat.Pdf,

            Tags = new List<string> { "sql", "performance", "aggregate", "sum" }
        };
    }

    /// <summary>
    /// Шаблон "Procedures by Total Execution Time" — суммарное время выполнения в разрезе процедур
    /// (GROUP BY ProcedureName + SUM(Performance.ExecuteMs), сортировка по убыванию суммы).
    /// </summary>
    private static ReportTemplate CreateProcedureSumExecuteTimeTemplate()
    {
        return new ReportTemplate
        {
            Id = "builtin_procedure_sum_execute_time",
            Name = Loc.Tr("Report.BuiltIn.ProcedureSumExecuteTime.Name"),
            Description = Loc.Tr("Report.BuiltIn.ProcedureSumExecuteTime.Description"),
            Author = "System",
            IsBuiltIn = true,

            Header = new ReportHeader
            {
                Title = "Procedures by Total Execution Time",
                Subtitle = "Aggregated execution time per procedure",
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
                    }
                }
            },

            Body = new ReportBody
            {
                GroupByFields = new List<string> { "ProcedureName" },
                SortByColumn = "Total time (ms)",
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "ProcedureName",
                        DisplayName = "Procedure",
                        PropertyPath = "ProcedureName",
                        Kind = ColumnKind.GroupKey,
                        WidthPercent = 70,
                        Order = 1,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "TotalExecuteMs",
                        DisplayName = "Total time (ms)",
                        PropertyPath = "Performance.ExecuteMs",
                        Kind = ColumnKind.Aggregate,
                        Aggregate = AggregateFunction.Sum,
                        Format = "N0",
                        WidthPercent = 30,
                        Order = 2,
                        Alignment = TextAlignment.Right
                    }
                },
                ShowSummary = true,
                Sections = new List<ReportSection>
                {
                    new()
                    {
                        Title = "Procedures by Total Execution Time",
                        Description = "Total execution time per procedure",
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
            SortDescending = true,

            SupportedFormats = new List<ReportFormat> { ReportFormat.Pdf, ReportFormat.Docx, ReportFormat.Xlsx, ReportFormat.Csv },
            DefaultFormat = ReportFormat.Pdf,

            Tags = new List<string> { "procedures", "performance", "aggregate", "sum" }
        };
    }

    /// <summary>
    /// Шаблон "SQL by Call Count" — количество вызовов в разрезе SQL-текста
    /// (GROUP BY Sql + COUNT, сортировка по убыванию числа вызовов).
    /// </summary>
    private static ReportTemplate CreateSqlCallCountTemplate()
    {
        return new ReportTemplate
        {
            Id = "builtin_sql_call_count",
            Name = Loc.Tr("Report.BuiltIn.SqlCallCount.Name"),
            Description = Loc.Tr("Report.BuiltIn.SqlCallCount.Description"),
            Author = "System",
            IsBuiltIn = true,

            Header = new ReportHeader
            {
                Title = "SQL by Call Count",
                Subtitle = "How many times each SQL statement was executed",
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
                    }
                }
            },

            Body = new ReportBody
            {
                GroupByFields = new List<string> { "Sql" },
                SortByColumn = "Call count",
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "Sql",
                        DisplayName = "SQL",
                        PropertyPath = "Sql",
                        Kind = ColumnKind.GroupKey,
                        WidthPercent = 75,
                        Order = 1,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "CallCount",
                        DisplayName = "Call count",
                        Kind = ColumnKind.Aggregate,
                        Aggregate = AggregateFunction.Count,
                        Format = "N0",
                        WidthPercent = 25,
                        Order = 2,
                        Alignment = TextAlignment.Right
                    }
                },
                ShowSummary = true,
                Sections = new List<ReportSection>
                {
                    new()
                    {
                        Title = "SQL by Call Count",
                        Description = "Call count per SQL statement",
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
            SortDescending = true,

            SupportedFormats = new List<ReportFormat> { ReportFormat.Pdf, ReportFormat.Docx, ReportFormat.Xlsx, ReportFormat.Csv },
            DefaultFormat = ReportFormat.Pdf,

            Tags = new List<string> { "sql", "aggregate", "count", "calls" }
        };
    }

    /// <summary>
    /// Шаблон "Procedures by Call Count" — количество вызовов в разрезе процедур
    /// (GROUP BY ProcedureName + COUNT, сортировка по убыванию числа вызовов).
    /// </summary>
    private static ReportTemplate CreateProcedureCallCountTemplate()
    {
        return new ReportTemplate
        {
            Id = "builtin_procedure_call_count",
            Name = Loc.Tr("Report.BuiltIn.ProcedureCallCount.Name"),
            Description = Loc.Tr("Report.BuiltIn.ProcedureCallCount.Description"),
            Author = "System",
            IsBuiltIn = true,

            Header = new ReportHeader
            {
                Title = "Procedures by Call Count",
                Subtitle = "How many times each procedure was called",
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
                    }
                }
            },

            Body = new ReportBody
            {
                GroupByFields = new List<string> { "ProcedureName" },
                SortByColumn = "Call count",
                VisibleFields = new List<EventField>
                {
                    new()
                    {
                        Name = "ProcedureName",
                        DisplayName = "Procedure",
                        PropertyPath = "ProcedureName",
                        Kind = ColumnKind.GroupKey,
                        WidthPercent = 75,
                        Order = 1,
                        Alignment = TextAlignment.Left
                    },
                    new()
                    {
                        Name = "CallCount",
                        DisplayName = "Call count",
                        Kind = ColumnKind.Aggregate,
                        Aggregate = AggregateFunction.Count,
                        Format = "N0",
                        WidthPercent = 25,
                        Order = 2,
                        Alignment = TextAlignment.Right
                    }
                },
                ShowSummary = true,
                Sections = new List<ReportSection>
                {
                    new()
                    {
                        Title = "Procedures by Call Count",
                        Description = "Call count per procedure",
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
            SortDescending = true,

            SupportedFormats = new List<ReportFormat> { ReportFormat.Pdf, ReportFormat.Docx, ReportFormat.Xlsx, ReportFormat.Csv },
            DefaultFormat = ReportFormat.Pdf,

            Tags = new List<string> { "procedures", "aggregate", "count", "calls" }
        };
    }
}