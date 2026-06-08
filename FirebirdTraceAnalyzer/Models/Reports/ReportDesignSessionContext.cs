using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Данные текущей сессии анализа для превью и генерации отчётов в дизайнере.
/// </summary>
public sealed class ReportDesignSessionContext
{
    public required IReadOnlyList<EventBase> SourceEvents { get; init; }

    public required IReadOnlyList<TraceFileInfoModel> Files { get; init; }

    public required long TotalEventsCount { get; init; }
}
