using System.Diagnostics;
using System.Text.RegularExpressions;
using FirebirdTraceAnalyzer.Enums;
using FirebirdTraceAnalyzer.Interfaces.Searching;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Searching;

public sealed class SearchService : ISearchService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public IEnumerable<EventBase> Search(
        IEnumerable<EventBase> events,
        string searchText,
        SearchType mode)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            Logger.Debug("The search query is empty, returning all events");
            return events;
        }

        var sw = Stopwatch.StartNew();

        var results = mode switch
        {
            SearchType.Classic => SearchClassic(events, searchText),
            SearchType.Regex => SearchRegex(events, searchText),
            _ => events
        };

        var resultList = results.ToList();

        Logger.Info("Search '{Query}' ({Mode}) finish at {Elapsed} ms, find: {Count}",
            searchText, mode, sw.ElapsedMilliseconds, resultList.Count);

        return resultList;
    }

    private IEnumerable<EventBase> SearchClassic(IEnumerable<EventBase> events, string searchText)
    {
        var query = searchText.Trim();
        const StringComparison comparison = StringComparison.OrdinalIgnoreCase;

        foreach (var evt in events)
            switch (evt)
            {
                // Поиск в SQL
                case StatementEventBase stmtEvt when
                    !string.IsNullOrWhiteSpace(stmtEvt.Sql) &&
                    stmtEvt.Sql.Contains(query, comparison):

                // Поиск в процедурах
                case ProcedureEventBase procEvt when
                    !string.IsNullOrWhiteSpace(procEvt.ProcedureName) &&
                    procEvt.ProcedureName.Contains(query, comparison):
                    yield return evt;
                    continue;

                // Поиск в триггерах
                case TriggerEventBase triggerEvt when
                    !string.IsNullOrWhiteSpace(triggerEvt.TriggerName) &&
                    triggerEvt.TriggerName.Contains(query, comparison):
                    yield return evt;
                    break;
            }
    }

    private IEnumerable<EventBase> SearchRegex(IEnumerable<EventBase> events, string pattern)
    {
        Regex regex;

        try
        {
            // Без RegexOptions.Compiled: паттерн создаётся заново на каждый поиск и используется
            // одним проходом. Compiled оправдан лишь для долгоживущих часто переиспользуемых регэкспов
            // (по гайдлайну .NET), иначе одноразовая генерация IL — чистые накладные.
            regex = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
        }
        catch (ArgumentException ex)
        {
            Logger.Error(ex, "Invalid regular expression: {Pattern}", pattern);
            yield break;
        }

        foreach (var evt in events)
        {
            bool isMatch;
            switch (evt)
            {
                // Поиск в SQL
                case StatementEventBase stmtEvt
                    when !string.IsNullOrWhiteSpace(stmtEvt.Sql):
                    try
                    {
                        isMatch = regex.IsMatch(stmtEvt.Sql);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        Logger.Warn("Timeout when searching for regex for an event {EventType}", evt.EventType);
                        break;
                    }

                    if (isMatch)
                        yield return evt;

                    isMatch = false;
                    continue;

                // Поиск в процедурах
                case ProcedureEventBase procEvt
                    when !string.IsNullOrWhiteSpace(procEvt.ProcedureName):
                    try
                    {
                        isMatch = regex.IsMatch(procEvt.ProcedureName);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        Logger.Warn("Timeout when searching for regex for an event {EventType}", evt.EventType);
                        break;
                    }

                    if (isMatch)
                        yield return evt;

                    isMatch = false;
                    continue;

                // Поиск в триггерах
                case TriggerEventBase triggerEvt
                    when !string.IsNullOrWhiteSpace(triggerEvt.TriggerName):
                    try
                    {
                        isMatch = regex.IsMatch(triggerEvt.TriggerName);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        Logger.Warn("Timeout when searching for regex for an event {EventType}", evt.EventType);
                        break;
                    }

                    if (isMatch)
                        yield return evt;

                    isMatch = false;
                    break;
            }
        }
    }
}