using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Defines a service for discovering fields from trace events.
/// </summary>
public interface IFieldDiscoveryService
{
    /// <summary>
    /// Retrieves all common fields (intersection) for the specified events.
    /// </summary>
    /// <param name="events">The events for which to retrieve common fields.</param>
    /// <returns>The list of common fields.</returns>
    IReadOnlyList<DiscoveredField> GetCommonFields(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Retrieves all fields for the specified event type.
    /// </summary>
    /// <param name="eventType">The type of the event for which to retrieve fields.</param>
    /// <returns>The list of fields.</returns>
    IReadOnlyList<DiscoveredField> GetFieldsForType(Type eventType);
    
    /// <summary>
    /// Retrieves all sortable fields for the specified events.
    /// </summary>
    /// <param name="events">The events for which to retrieve sortable fields.</param>
    /// <returns>The list of sortable fields.</returns>
    IReadOnlyList<DiscoveredField> GetSortableFields(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Retrieves all filterable fields for the specified events.
    /// </summary>
    /// <param name="events">The events for which to retrieve filterable fields.</param>
    /// <returns>The list of filterable fields.</returns>
    IReadOnlyList<DiscoveredField> GetFilterableFields(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Retrieves all available fields for the specified events.
    /// </summary>
    /// <param name="events">The events for which to retrieve available fields.</param>
    /// <returns>The list of available fields.</returns>
    IReadOnlyList<DiscoveredField> GetAllAvailableFields(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Clears the cached field discovery results.
    /// </summary>
    void ClearCache();
}