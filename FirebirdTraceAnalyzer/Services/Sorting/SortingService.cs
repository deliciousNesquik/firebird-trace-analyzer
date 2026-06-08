using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Sorting;

public sealed class SortingService : ISortingService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IEventPropertyAccessor _propertyAccessor;
    private readonly IFieldDiscoveryService _fieldDiscovery;

    private readonly Dictionary<string, SortDescriptor> _customSorts = new();

    private List<SortDescriptor>? _lastGeneratedSorts;
    private HashSet<Type>? _lastEventTypes;

    public SortingService(
        IEventPropertyAccessor propertyAccessor,
        IFieldDiscoveryService fieldDiscovery)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
        _fieldDiscovery = fieldDiscovery ?? throw new ArgumentNullException(nameof(fieldDiscovery));
    }

    public void RegisterCustomSort(SortDescriptor descriptor)
    {
        _customSorts[descriptor.Id] = descriptor;
        Logger.Info("Registered sort: {DisplayName}", descriptor.DisplayName);
    }

    public IReadOnlyList<SortDescriptor> GetAvailableSorts(IEnumerable<EventBase> events)
    {
        var eventList = events.ToList();

        if (eventList.Count == 0)
        {
            return _customSorts.Values
                .OrderBy(s => s.Priority)
                .ToList();
        }

        var currentEventTypes = eventList
            .Select(e => e.GetType())
            .ToHashSet();

        if (_lastEventTypes != null &&
            _lastGeneratedSorts != null &&
            currentEventTypes.SetEquals(_lastEventTypes))
        {
            Logger.Debug("Event types haven't changed, we'll reuse sorting");
            return _lastGeneratedSorts;
        }

        Logger.Info("Event types have changed, we are generating new sortings");

        var availableSorts = new List<SortDescriptor>(_customSorts.Values);

        // Используем новый сервис для получения сортируемых полей
        var sortableFields = _fieldDiscovery.GetSortableFields(eventList);

        foreach (var field in sortableFields)
        {
            var sortId = _propertyAccessor.ToSortId(field.PropertyPath);

            if (_customSorts.ContainsKey(sortId))
                continue;

            var descriptor = CreateFieldSort(field);
            availableSorts.Add(descriptor);
            _customSorts.Add(sortId, descriptor);
        }

        var result = availableSorts
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Priority)
            .ToList();

        _lastGeneratedSorts = result;
        _lastEventTypes = currentEventTypes;

        return result;
    }

    private SortDescriptor CreateFieldSort(DiscoveredField field)
    {
        return new SortDescriptor(
            _propertyAccessor.ToSortId(field.PropertyPath),
            field.DisplayName,
            CreatePropertyComparer(field.PropertyPath),
            false, // isDefault
            field.Category);
    }

    private Func<EventBase, EventBase, bool, int> CreatePropertyComparer(string propertyPath)
    {
        return (a, b, descending) =>
        {
            var valueA = _propertyAccessor.GetValue(a, propertyPath);
            var valueB = _propertyAccessor.GetValue(b, propertyPath);
            var result = _propertyAccessor.Compare(valueA, valueB);

            return descending ? -result : result;
        };
    }

    public IEnumerable<EventBase> ApplySort(
        IEnumerable<EventBase> events,
        string sortId,
        bool descending = false)
    {
        if (!_customSorts.TryGetValue(sortId, out var descriptor))
        {
            Logger.Warn("Sort is not found: {SortId}", sortId);
            return events;
        }

        var sorted = events.ToList();
        sorted.Sort((a, b) => descriptor.Comparer(a, b, descending));

        Logger.Info("Sorting applied: {DisplayName}, descending={Descending}",
            descriptor.DisplayName, descending);

        return sorted;
    }
}