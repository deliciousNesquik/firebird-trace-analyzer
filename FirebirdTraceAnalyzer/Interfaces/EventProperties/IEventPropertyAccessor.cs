namespace FirebirdTraceAnalyzer.Interfaces.EventProperties;

/// <summary>
/// Defines an interface for accessing and comparing event properties, as well as resolving filter and sort IDs to property paths.
/// </summary>
public interface IEventPropertyAccessor
{
    /// <summary>
    /// Returns the value of the property specified by the <paramref name="propertyPath"/> from the <paramref name="target"/> object.
    /// </summary>
    /// <param name="target">The object from which to retrieve the property value.</param>
    /// <param name="propertyPath">The path to the property.</param>
    /// <returns>The value of the property, or <c>null</c> if the property is not found.</returns>
    object? GetValue(object target, string propertyPath);

    /// <summary>
    /// Compares two values for sorting purposes. Returns a negative number if <paramref name="valueA"/> is less than <paramref name="valueB"/>,
    /// zero if they are equal, and a positive number if <paramref name="valueA"/> is greater than <paramref name="valueB"/>.
    /// </summary>
    /// <param name="valueA">The first value to compare.</param>
    /// <param name="valueB">The second value to compare.</param>
    /// <returns>A negative number if <paramref name="valueA"/> is less than <paramref name="valueB"/>, zero if they are equal, and a positive number if <paramref name="valueA"/> is greater than <paramref name="valueB"/>.</returns>
    int Compare(object? valueA, object? valueB);

    /// <summary>
    /// Attempts to resolve a filter ID into a property path.
    /// </summary>
    /// <param name="filterId">The filter ID to resolve.</param>
    /// <param name="propertyPath">The resolved property path, or <c>null</c> if the ID is not recognized.</param>
    /// <returns><c>true</c> if the filter ID was resolved; otherwise, <c>false</c>.</returns>
    bool TryResolveFilterId(string filterId, out string propertyPath);

    /// <summary>
    /// Attempts to resolve a sort ID into a property path.
    /// </summary>
    /// <param name="sortId">The sort ID to resolve.</param>
    /// <param name="propertyPath">The resolved property path, or <c>null</c> if the ID is not recognized.</param>
    /// <returns><c>true</c> if the sort ID was resolved; otherwise, <c>false</c>.</returns>
    bool TryResolveSortId(string sortId, out string propertyPath);

    /// <summary>
    /// Creates a filter ID from a property path (as in <see cref="Filtering.FilteringService"/>).
    /// </summary>
    /// <param name="propertyPath">The property path to convert.</param>
    /// <returns>The filter ID.</returns>
    string ToFilterId(string propertyPath);

    /// <summary>
    /// Creates a sort ID from a property path (as in <see cref="Sorting.SortingService"/>).
    /// </summary>
    /// <param name="propertyPath">The property path to convert.</param>
    /// <returns>The sort ID.</returns>
    string ToSortId(string propertyPath);

    /// <summary>
    /// Gets all known property paths for events (from the parser model attributes).
    /// </summary>
    IReadOnlyCollection<string> KnownPropertyPaths { get; }
}
