using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Reports;

/// <summary>
/// Строит из подготовленных событий и шаблона готовую к отрисовке таблицу (<see cref="ReportTable"/>).
/// Сейчас — одна строка на событие (как делают экспортёры сегодня); позже здесь же появится
/// ветка группировки/агрегации, прозрачно для экспортёров.
/// </summary>
public interface IReportProjectionService
{
    ReportTable BuildTable(ReportTemplate template, IReadOnlyList<EventBase> events);
}
