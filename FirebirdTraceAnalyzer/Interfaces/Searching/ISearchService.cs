using FirebirdTraceAnalyzer.Enums;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Searching;

/// <summary>
/// Defines a service for searching through events based on specified criteria.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches the given events based on the provided search text and search mode.
    /// </summary>
    /// <param name="events">The events to search through.</param>
    /// <param name="searchQuery">The text to search for.</param>
    /// <param name="mode">The search mode to use.</param>
    /// <returns>An enumerate of events that match the search criteria.</returns>
    IEnumerable<EventBase> Search(
        IEnumerable<EventBase> events, 
        string searchQuery, 
        SearchType mode);
}