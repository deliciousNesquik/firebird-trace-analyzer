using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Filtering;

/// <summary>
/// Defines the interface for a filtering service that provides methods for managing and applying filters to events.
/// </summary>
public interface IFilteringService
{
    /// <summary>
    /// Gets a list of available filters based on the provided events.
    /// This method analyzes the events and returns a collection of filter
    /// descriptors that can be applied to the events.
    /// </summary>
    /// <param name="events">The events to analyze.</param>
    /// <returns>A list of available filter descriptors.</returns>
    IReadOnlyList<FilterDescriptor> GetAvailableFilters(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Applies the specified filters to the provided events and returns the filtered events.
    /// This method evaluates each event against the filter descriptors and returns
    /// only those events that match the filter criteria.
    /// </summary>
    /// <param name="events">The events to filter.</param>
    /// <param name="filters">The filters to apply.</param>
    /// <returns>The filtered events.</returns>
    IEnumerable<EventBase> ApplyFilters(IEnumerable<EventBase> events, IEnumerable<FilterDescriptor> filters);

    /// <summary>
    /// Scans the provided events and filters to determine the current state of filter values.
    /// This method analyzes the events and filters to identify the counts, new values,
    /// and range boundaries for each filter descriptor.
    /// </summary>
    /// <param name="events">The events to scan.</param>
    /// <param name="filters">The filters to scan.</param>
    /// <returns>The scan results.</returns>
    FilterValueScan ScanFilterValues(IReadOnlyList<EventBase> events, IReadOnlyList<FilterDescriptor> filters);

    /// <summary>
    /// Applies the filter values from the provided scan to the specified filters.
    /// </summary>
    /// <param name="filters">The filters to update.</param>
    /// <param name="scan">The scan results containing the filter values.</param>
    void ApplyFilterValues(IReadOnlyList<FilterDescriptor> filters, FilterValueScan scan);
    
    /// <summary>
    /// Registers a custom filter descriptor with the filtering service.
    /// </summary>
    /// <param name="descriptor">The filter descriptor to register.</param>
    void RegisterCustomFilter(FilterDescriptor descriptor);

    /// <summary>
    /// Creates an independent configurable clone of the provided filter descriptor.
    /// </summary>
    /// <param name="source">The filter descriptor to clone.</param>
    /// <returns>The cloned filter descriptor.</returns>
    FilterDescriptor CreateConfigurableClone(FilterDescriptor source);
}