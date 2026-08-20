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

    /// <summary>
    /// Reports [SortableField]/[FilterableField] annotations the discovery does not surface — after
    /// collection-element expansion, this means annotations nested beyond the scan depth. Logs a
    /// warning per site and returns the issues found. Run once at startup.
    /// </summary>
    IReadOnlyList<AnnotationValidationIssue> ValidateAnnotations();
}

/// <summary>
/// An annotated collection-element field the discovery did not surface — its dotted path (carrying the
/// <c>"[]"</c> marker) was absent from the discovered set, e.g. nested beyond the scan depth.
/// </summary>
/// <param name="OwnerType">The event (or nested model) type that declares the collection property.</param>
/// <param name="CollectionProperty">The name of the collection property on <paramref name="OwnerType"/>.</param>
/// <param name="ElementType">The collection's element type that carries the unreachable annotations.</param>
/// <param name="IgnoredFields">Full dotted paths (with the <c>"[]"</c> marker) of the unreachable annotated fields.</param>
public sealed record AnnotationValidationIssue(
    Type OwnerType,
    string CollectionProperty,
    Type ElementType,
    IReadOnlyList<string> IgnoredFields);