using FirebirdTraceAnalyzer.Services.Sorting;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Sorting;

/// <summary>
/// Defines the interface for a sorting service that provides functionality to sort events based on various criteria.
/// </summary>
public interface ISortingService
{
    /// <summary>
    /// Gets the list of available sort descriptors for the given events.
    /// </summary>
    /// <param name="events">The list of events to find available sort.</param>
    /// <returns>The list of available sort descriptors.</returns>
    IReadOnlyList<SortDescriptor> GetAvailableSorts(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Registers a custom sort descriptor.
    /// </summary>
    /// <param name="descriptor">The sort descriptor to register.</param>
    void RegisterCustomSort(SortDescriptor descriptor);
    
    /// <summary>
    /// Applies the specified sort to the given events.
    /// </summary>
    /// <param name="events">The events to sort.</param>
    /// <param name="sortId">The ID of the sort to apply.</param>
    /// <param name="descending">Indicates whether to sort in descending order.</param>
    /// <returns>The sorted list of events.</returns>
    IEnumerable<EventBase> ApplySort(IEnumerable<EventBase> events, string sortId, bool descending = false);
}