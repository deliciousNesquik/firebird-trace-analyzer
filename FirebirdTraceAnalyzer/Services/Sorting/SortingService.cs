using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceAnalyzer.Interfaces.Sorting;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Sorting;

public sealed class SortingService : ISortingService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IEventPropertyAccessor _propertyAccessor;
    private readonly IFieldDiscoveryService _fieldDiscovery;

    // Только явно зарегистрированные (плагины/пользователь) — постоянные, не зависят от набора файлов.
    private readonly Dictionary<string, SortDescriptor> _customSorts = new();

    // Сгенерированные по полям текущего набора событий — пересобираются при смене типов, НЕ подмешиваются
    // в _customSorts, иначе сортировки прошлого файла копятся и всплывают в дропдауне следующего.
    private readonly Dictionary<string, SortDescriptor> _generatedSorts = new();

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
        // Словарь ключуется по Id: повторная регистрация с тем же Id перезапишет прежнюю сортировку.
        // Предупреждаем, чтобы коллизия Id (например, между плагинами) не терялась молча.
        if (_customSorts.TryGetValue(descriptor.Id, out var existing))
            Logger.Warn(
                "Sort with Id '{Id}' overwrites existing one: '{Old}' -> '{New}'",
                descriptor.Id, existing.DisplayName, descriptor.DisplayName);

        _customSorts[descriptor.Id] = descriptor;
        Logger.Info("Registered sort: {DisplayName}", descriptor.DisplayName);
    }

    public IReadOnlyList<SortDescriptor> GetAvailableSorts(IEnumerable<EventBase> events)
    {
        var eventList = events.ToList();

        if (eventList.Count == 0)
        {
            return _customSorts.Values
                .OrderBy(s => s.DisplayOrder)
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

        // Пересобираем генерируемые сортировки для ТЕКУЩИХ типов начисто — прежние (для другого файла) выкидываем.
        _generatedSorts.Clear();

        // Используем новый сервис для получения сортируемых полей
        var sortableFields = _fieldDiscovery.GetSortableFields(eventList);

        foreach (var field in sortableFields)
        {
            var sortId = _propertyAccessor.ToSortId(field.PropertyPath);

            if (_customSorts.ContainsKey(sortId))
                continue; // зарегистрированная кастомная сортировка имеет приоритет

            _generatedSorts[sortId] = CreateFieldSort(field);
        }

        var result = _customSorts.Values
            .Concat(_generatedSorts.Values)
            .OrderBy(s => s.Category)
            .ThenBy(s => s.DisplayOrder)
            .ToList();

        _lastGeneratedSorts = result;
        _lastEventTypes = currentEventTypes;

        return result;
    }

    private SortDescriptor CreateFieldSort(DiscoveredField field)
    {
        var path = field.PropertyPath;
        return new SortDescriptor(
            _propertyAccessor.ToSortId(path),
            field.DisplayName,
            CreatePropertyComparer(path),
            field.IsDefaultSort,
            field.Category,
            field.SortDisplayOrder)
        {
            // Ключ достаём один раз на событие (см. ApplySort) — избегаем рефлексии на каждом сравнении.
            KeySelector = evt => _propertyAccessor.GetValue(evt, path)
        };
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
        if (!_customSorts.TryGetValue(sortId, out var descriptor) &&
            !_generatedSorts.TryGetValue(sortId, out descriptor))
        {
            Logger.Warn("Sort is not found: {SortId}", sortId);
            return events;
        }

        var sorted = events.ToList();

        if (descriptor.KeySelector is { } keySelector)
        {
            // decorate-sort-undecorate: извлекаем ключ ОДИН раз на событие, затем сортируем по ключам,
            // вместо пересчёта геттера через рефлексию на каждом из O(N·logN) сравнений.
            var keyed = new (object? Key, EventBase Event)[sorted.Count];
            for (var i = 0; i < sorted.Count; i++)
                keyed[i] = (keySelector(sorted[i]), sorted[i]);

            Array.Sort(keyed, (a, b) =>
            {
                var result = _propertyAccessor.Compare(a.Key, b.Key);
                return descending ? -result : result;
            });

            for (var i = 0; i < keyed.Length; i++)
                sorted[i] = keyed[i].Event;
        }
        else
        {
            sorted.Sort((a, b) => descriptor.Comparer(a, b, descending));
        }

        Logger.Info("Sorting applied: {DisplayName}, descending={Descending}",
            descriptor.DisplayName, descending);

        return sorted;
    }
}