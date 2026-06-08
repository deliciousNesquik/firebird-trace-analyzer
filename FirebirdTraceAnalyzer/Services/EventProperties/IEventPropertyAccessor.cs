namespace FirebirdTraceAnalyzer.Services.EventProperties;

/// <summary>
/// Единый доступ к значениям свойств событий по пути (например, <c>Attachment.User</c>)
/// и преобразование идентификаторов фильтров/сортировок в пути свойств.
/// </summary>
public interface IEventPropertyAccessor
{
    /// <summary>Возвращает значение свойства по пути от корневого объекта события.</summary>
    object? GetValue(object target, string propertyPath);

    /// <summary>Сравнивает два значения для сортировки (null в конец).</summary>
    int Compare(object? valueA, object? valueB);

    /// <summary>
    /// Преобразует ID фильтра (<c>filter_performance_executems</c>) в путь свойства.
    /// </summary>
    bool TryResolveFilterId(string filterId, out string propertyPath);

    /// <summary>
    /// Преобразует ID сортировки (<c>field_timestamp</c>) в путь свойства.
    /// </summary>
    bool TryResolveSortId(string sortId, out string propertyPath);

    /// <summary>Создаёт ID фильтра из пути свойства (как в <see cref="Filtering.FilteringService"/>).</summary>
    string ToFilterId(string propertyPath);

    /// <summary>Создаёт ID сортировки из пути свойства (как в <see cref="Sorting.SortingService"/>).</summary>
    string ToSortId(string propertyPath);

    /// <summary>Все известные пути свойств событий (из атрибутов модели парсера).</summary>
    IReadOnlyCollection<string> KnownPropertyPaths { get; }
}
