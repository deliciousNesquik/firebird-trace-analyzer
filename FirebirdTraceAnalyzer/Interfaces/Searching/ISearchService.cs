using FirebirdTraceAnalyzer.Enums;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Searching;

public interface ISearchService
{
    /// <summary>
    /// Выполняет поиск по SQL, процедурам и триггерам
    /// </summary>
    IEnumerable<EventBase> Search(
        IEnumerable<EventBase> events, 
        string searchText, 
        SearchType mode);
}