using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceAnalyzer.Interfaces.Filtering;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Filtering;

public sealed class FilteringService : IFilteringService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IEventPropertyAccessor _propertyAccessor;
    private readonly IFieldDiscoveryService _fieldDiscovery;

    private readonly Dictionary<string, FilterDescriptor> _customFilters = new();

    private List<FilterDescriptor>? _lastGeneratedFilters;
    private HashSet<Type>? _lastEventTypes;

    public FilteringService(
        IEventPropertyAccessor propertyAccessor,
        IFieldDiscoveryService fieldDiscovery)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
        _fieldDiscovery = fieldDiscovery ?? throw new ArgumentNullException(nameof(fieldDiscovery));
    }

    public void RegisterCustomFilter(FilterDescriptor descriptor)
    {
        // Словарь ключуется по Id: повторная регистрация с тем же Id перезапишет прежний фильтр.
        // Предупреждаем, чтобы коллизия Id (например, между плагинами) не терялась молча.
        if (_customFilters.TryGetValue(descriptor.Id, out var existing))
            Logger.Warn(
                "Filter with Id '{Id}' overwrites existing one: '{Old}' -> '{New}'",
                descriptor.Id, existing.DisplayName, descriptor.DisplayName);

        _customFilters[descriptor.Id] = descriptor;
        Logger.Info("Register filter: {DisplayName}", descriptor.DisplayName);
    }

    public IReadOnlyList<FilterDescriptor> GetAvailableFilters(IEnumerable<EventBase> events)
    {
        var eventList = events.ToList();
        
        if (eventList.Count == 0)
        {
            return _customFilters.Values
                .OrderBy(f => f.DisplayOrder)
                .ToList();
        }

        var currentEventTypes = eventList
            .Select(e => e.GetType())
            .ToHashSet();

        if (_lastEventTypes != null && 
            _lastGeneratedFilters != null && 
            currentEventTypes.SetEquals(_lastEventTypes))
        {
            Logger.Debug("Event types haven't changed, we'll reuse filters");
            // Значения/счётчики/диапазоны обновляются отдельно (ScanFilterValues + ApplyFilterValues):
            // тяжёлый O(N)-скан идёт в фоне, здесь возвращаем только структуру.
            return _lastGeneratedFilters;
        }

        Logger.Info("Event types have changed, we are generating new filters");

        var availableFilters = new List<FilterDescriptor>(_customFilters.Values);

        // Используем новый сервис для получения фильтруемых полей
        var filterableFields = _fieldDiscovery.GetFilterableFields(eventList);

        foreach (var field in filterableFields)
        {
            var filterId = _propertyAccessor.ToFilterId(field.PropertyPath);

            if (_customFilters.ContainsKey(filterId))
                continue;

            var descriptor = CreateFieldFilter(field, eventList);
            if (descriptor != null)
            {
                availableFilters.Add(descriptor);
            }
        }

        var result = availableFilters
            .OrderBy(f => f.Category)
            .ThenBy(f => f.DisplayOrder)
            .ToList();

        _lastGeneratedFilters = result;
        _lastEventTypes = currentEventTypes;

        return result;
    }

    // Скан (фон): только читает события и PropertyPath/FilterType дескрипторов, ничего не пишет в UI.
    public FilterValueScan ScanFilterValues(IReadOnlyList<EventBase> events, IReadOnlyList<FilterDescriptor> filters)
    {
        var scan = new FilterValueScan();

        foreach (var filter in filters)
        {
            switch (filter.FilterType)
            {
                case FilterType.EnumMultiSelect or FilterType.StringMultiSelect:
                {
                    // Для строк считаем OrdinalIgnoreCase — как CreateStringFilter и предикат, иначе
                    // 'isql' и 'ISQL' расходятся: счётчик занижается и появляется строка-дубль.
                    var valueCounts = new Dictionary<object, int>(MultiSelectComparer(filter.FilterType));
                    foreach (var evt in events)
                    {
                        var value = _propertyAccessor.GetValue(evt, filter.PropertyPath);
                        if (value != null)
                        {
                            valueCounts.TryGetValue(value, out var count);
                            valueCounts[value] = count + 1;
                        }
                    }
                    scan.MultiSelectCounts[filter.Id] = valueCounts;
                    break;
                }
                case FilterType.NumericRange or FilterType.DateTimeRange:
                {
                    IComparable? min = null, max = null;
                    foreach (var evt in events)
                    {
                        if (_propertyAccessor.GetValue(evt, filter.PropertyPath) is IComparable value)
                        {
                            if (min == null || value.CompareTo(min) < 0) min = value;
                            if (max == null || value.CompareTo(max) > 0) max = value;
                        }
                    }
                    if (min != null && max != null)
                        scan.Ranges[filter.Id] = (min, max);
                    break;
                }
            }
        }

        return scan;
    }

    // Применение (UI): пишет счётчики/новые значения/границы в дескрипторы. Только на UI-потоке.
    public void ApplyFilterValues(IReadOnlyList<FilterDescriptor> filters, FilterValueScan scan)
    {
        foreach (var filter in filters)
        {
            switch (filter.FilterType)
            {
                case FilterType.EnumMultiSelect or FilterType.StringMultiSelect:
                {
                    if (!scan.MultiSelectCounts.TryGetValue(filter.Id, out var valueCounts))
                        break;

                    foreach (var item in filter.AvailableValues)
                        item.Count = valueCounts.GetValueOrDefault(item.Value, 0);

                    var existingValues = filter.AvailableValues.Select(v => v.Value)
                        .ToHashSet(MultiSelectComparer(filter.FilterType));
                    foreach (var (value, count) in valueCounts.Where(kv => !existingValues.Contains(kv.Key)))
                    {
                        var displayName = value is Enum ? GetEnumDisplayName(value) : value.ToString()!;
                        filter.AvailableValues.Add(new FilterValueItem(value, displayName, count));
                    }
                    break;
                }
                case FilterType.NumericRange or FilterType.DateTimeRange:
                {
                    if (scan.Ranges.TryGetValue(filter.Id, out var range))
                    {
                        filter.MinValue = range.Min;
                        filter.MaxValue = range.Max;
                        filter.CurrentMinValue ??= filter.MinValue;
                        filter.CurrentMaxValue ??= filter.MaxValue;
                    }
                    break;
                }
            }
        }
    }

    public IEnumerable<EventBase> ApplyFilters(
        IEnumerable<EventBase> events,
        IEnumerable<FilterDescriptor> filters)
    {
        var activeFilters = filters.Where(f => f.IsActive).ToList();

        if (activeFilters.Count == 0)
            return events;

        Logger.Info("Apply {Count} activity filter(s)", activeFilters.Count);

        // Состояние фильтров (наборы значений, границы диапазонов) постоянно в течение одного прохода.
        // Компилируем предикат ОДИН раз на фильтр, а не пересобираем HashSet-ы и не парсим границы дат
        // на КАЖДОМ событии (на миллионах событий это были миллионы лишних аллокаций/парсингов).
        var compiled = new Func<EventBase, bool>[activeFilters.Count];
        for (var i = 0; i < activeFilters.Count; i++)
            compiled[i] = CompilePredicate(activeFilters[i]);

        return events.Where(evt =>
        {
            foreach (var predicate in compiled)
                if (!predicate(evt))
                    return false;
            return true;
        });
    }

    /// <summary>
    /// Строит предикат фильтра с предвычисленным состоянием (наборы значений/границы диапазона).
    /// Для стандартных полевых фильтров логика повторяет Check*-методы, но без пересчёта на каждое
    /// событие. Кастомные/плагинные фильтры сохраняют свой оригинальный предикат.
    /// </summary>
    private Func<EventBase, bool> CompilePredicate(FilterDescriptor filter)
    {
        // Кастомные фильтры (в т.ч. из плагинов) имеют произвольную логику — не подменяем её.
        if (_customFilters.ContainsKey(filter.Id))
            return filter.FilterPredicate;

        var path = filter.PropertyPath;
        switch (filter.FilterType)
        {
            case FilterType.EnumMultiSelect or FilterType.Boolean:
            {
                var included = filter.AvailableValues.Where(v => v.IsSelected).Select(v => v.Value).ToHashSet();
                var excluded = filter.AvailableValues.Where(v => v.IsExcluded).Select(v => v.Value).ToHashSet();
                if (included.Count == 0 && excluded.Count == 0)
                    return static _ => true;
                return evt =>
                {
                    var value = _propertyAccessor.GetValue(evt, path);
                    if (included.Count > 0 && (value == null || !included.Contains(value)))
                        return false;
                    return value == null || !excluded.Contains(value);
                };
            }
            case FilterType.StringMultiSelect:
            {
                var included = filter.AvailableValues.Where(v => v.IsSelected).Select(v => v.Value.ToString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var excluded = filter.AvailableValues.Where(v => v.IsExcluded).Select(v => v.Value.ToString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (included.Count == 0 && excluded.Count == 0)
                    return static _ => true;
                return evt =>
                {
                    var value = _propertyAccessor.GetValue(evt, path)?.ToString();
                    if (included.Count > 0 && (value == null || !included.Contains(value)))
                        return false;
                    return value == null || !excluded.Contains(value);
                };
            }
            case FilterType.DateTimeRange:
            {
                // Границы приходят из TwoWay-биндинга TextBox → это может быть строка ЛИБО DateTime.
                // Приводим ОДИН раз здесь, а не парсим строку на каждом событии.
                var hasMin = TryCoerceDateTime(filter.CurrentMinValue, out var min);
                var hasMax = TryCoerceDateTime(filter.CurrentMaxValue, out var max);
                return evt =>
                {
                    if (_propertyAccessor.GetValue(evt, path) is not DateTime dt)
                        return false;
                    if (hasMin && dt < min) return false;
                    if (hasMax && dt > max) return false;
                    return true;
                };
            }
            case FilterType.NumericRange:
            {
                // Границы приходят из TwoWay-биндинга TextBox → могут быть строкой. Приводим ОДИН раз
                // к типу поля (MinValue/MaxValue хранят исходные значения нужного типа), как в DateTimeRange.
                var targetType = (filter.MinValue ?? filter.MaxValue)?.GetType();
                var min = CoerceComparable(filter.CurrentMinValue, targetType);
                var max = CoerceComparable(filter.CurrentMaxValue, targetType);
                return evt =>
                {
                    if (_propertyAccessor.GetValue(evt, path) is not IComparable value)
                        return false;
                    if (min != null && value.CompareTo(min) < 0) return false;
                    if (max != null && value.CompareTo(max) > 0) return false;
                    return true;
                };
            }
            case FilterType.TextSearch:
            {
                var query = filter.SearchText;
                if (string.IsNullOrWhiteSpace(query))
                    return static _ => true;
                return evt =>
                {
                    var value = _propertyAccessor.GetValue(evt, path)?.ToString();
                    return value != null && value.Contains(query, StringComparison.OrdinalIgnoreCase);
                };
            }
            default:
                return filter.FilterPredicate;
        }
    }

    /// <summary>
    /// Приводит границу числового диапазона к типу поля. Граница может прийти СТРОКОЙ из
    /// TwoWay-биндинга TextBox — тогда <c>value.CompareTo(bound)</c> бросил бы ArgumentException
    /// (напр. <c>int.CompareTo("100")</c>). Непарсимая граница → <c>null</c> (трактуем как «не задана»),
    /// чтобы один кривой ввод не ронял весь конвейер фильтров.
    /// </summary>
    private static IComparable? CoerceComparable(object? bound, Type? targetType)
    {
        if (bound is null || targetType is null)
            return null;
        if (bound.GetType() == targetType)
            return bound as IComparable;
        try
        {
            return Convert.ChangeType(bound, targetType, CultureInfo.CurrentCulture) as IComparable;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Компаратор ключей мультиселекта: для строкового фильтра — OrdinalIgnoreCase (как при создании
    /// фильтра и в предикате), для остальных (enum) — по умолчанию (<c>null</c>).
    /// </summary>
    private static IEqualityComparer<object>? MultiSelectComparer(FilterType type) =>
        type == FilterType.StringMultiSelect ? ObjectOrdinalIgnoreCaseComparer.Instance : null;

    /// <summary>Сравнивает object-ключи как OrdinalIgnoreCase для строк (для остальных типов — по умолчанию).</summary>
    private sealed class ObjectOrdinalIgnoreCaseComparer : IEqualityComparer<object>
    {
        public static readonly ObjectOrdinalIgnoreCaseComparer Instance = new();

        public new bool Equals(object? x, object? y) =>
            x is string sx && y is string sy
                ? string.Equals(sx, sy, StringComparison.OrdinalIgnoreCase)
                : object.Equals(x, y);

        public int GetHashCode(object obj) =>
            obj is string s ? StringComparer.OrdinalIgnoreCase.GetHashCode(s) : obj.GetHashCode();
    }

    /// <summary>Приводит границу диапазона (DateTime, либо строка из TextBox) к <see cref="DateTime"/>.</summary>
    private static bool TryCoerceDateTime(object? boxed, out DateTime result)
    {
        switch (boxed)
        {
            case DateTime dt:
                result = dt;
                return true;
            case null:
                result = default;
                return false;
            default:
                // Явная культура (та же, что рендерит TextBox) — чтобы парсинг не зависел от культуры
                // потока; непарсимое → false (граница не применяется, без падения конвейера).
                return DateTime.TryParse(boxed.ToString(), CultureInfo.CurrentCulture, DateTimeStyles.None, out result);
        }
    }

    public FilterDescriptor CreateConfigurableClone(FilterDescriptor source)
    {
        var clone = new FilterDescriptor(
            source.Id,
            source.DisplayName,
            source.FilterType,
            source.PropertyPath,
            _ => true,
            source.Category,
            source.DisplayOrder)
        {
            MinValue = source.MinValue,
            MaxValue = source.MaxValue,
            CurrentMinValue = source.MinValue,
            CurrentMaxValue = source.MaxValue,
            SearchText = source.SearchText
        };

        foreach (var value in source.AvailableValues)
            clone.AvailableValues.Add(new FilterValueItem(value.Value, value.DisplayName, value.Count));

        clone.InitializeFilteredValues();

        // Предикат привязан к состоянию КОПИИ — та же логика, что у живых фильтров главной формы.
        switch (clone.FilterType)
        {
            case FilterType.EnumMultiSelect:
                clone.UpdatePredicate(evt => CheckEnumFilter(evt, clone.PropertyPath, clone.AvailableValues));
                break;

            case FilterType.StringMultiSelect:
                clone.UpdatePredicate(evt => CheckStringFilter(evt, clone.PropertyPath, clone.AvailableValues));
                break;

            case FilterType.NumericRange:
                clone.UpdatePredicate(evt =>
                {
                    if (_propertyAccessor.GetValue(evt, clone.PropertyPath) is not IComparable value)
                        return false;

                    // Границы могут быть строкой из TextBox — приводим к типу значения, чтобы CompareTo не бросал.
                    var min = CoerceComparable(clone.CurrentMinValue, value.GetType());
                    var max = CoerceComparable(clone.CurrentMaxValue, value.GetType());

                    if (min != null && value.CompareTo(min) < 0)
                        return false;

                    if (max != null && value.CompareTo(max) > 0)
                        return false;

                    return true;
                });
                break;

            case FilterType.DateTimeRange:
                clone.UpdatePredicate(evt =>
                {
                    if (_propertyAccessor.GetValue(evt, clone.PropertyPath) is not DateTime dateTime)
                        return false;

                    if (DateTime.TryParse(clone.CurrentMinValue?.ToString(), out var min) && dateTime < min)
                        return false;

                    if (DateTime.TryParse(clone.CurrentMaxValue?.ToString(), out var max) && dateTime > max)
                        return false;

                    return true;
                });
                break;

            case FilterType.TextSearch:
                clone.UpdatePredicate(evt => CheckTextSearchFilter(evt, clone));
                break;
        }

        return clone;
    }

    private FilterDescriptor? CreateFieldFilter(DiscoveredField field, List<EventBase> events)
    {
        var filterId = _propertyAccessor.ToFilterId(field.PropertyPath);

        var filterType = field.FilterType ?? DetermineFilterType(field.PropertyType);

        return filterType switch
        {
            FilterType.EnumMultiSelect => CreateEnumFilter(filterId, field, events),
            FilterType.StringMultiSelect => CreateStringFilter(filterId, field, events),
            FilterType.NumericRange => CreateNumericRangeFilter(filterId, field, events),
            FilterType.DateTimeRange => CreateDateTimeRangeFilter(filterId, field, events),
            // Boolean переиспользует путь мультиселекта: две галки (True/False). Дескриптор помечается
            // EnumMultiSelect, поэтому UpdateFilterValues и CreateConfigurableClone подхватывают его без
            // отдельных веток. Отдельный контрол-переключатель — при желании, отдельным заходом.
            FilterType.Boolean => CreateEnumFilter(filterId, field, events),
            FilterType.TextSearch => CreateTextSearchFilter(filterId, field),
            _ => null
        };
    }

    private FilterType DetermineFilterType(Type propertyType)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlyingType.IsEnum)
            return FilterType.EnumMultiSelect;

        if (underlyingType == typeof(string))
            return FilterType.StringMultiSelect;

        if (underlyingType == typeof(DateTime))
            return FilterType.DateTimeRange;

        if (underlyingType == typeof(bool))
            return FilterType.Boolean;

        if (IsNumericType(underlyingType))
            return FilterType.NumericRange;

        return FilterType.TextSearch;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) ||
               type == typeof(decimal) || type == typeof(double) ||
               type == typeof(float) || type == typeof(short) ||
               type == typeof(byte);
    }

    #region Create Filters

    private FilterDescriptor CreateEnumFilter(string id, DiscoveredField field, List<EventBase> events)
    {
        var valueCounts = new Dictionary<object, int>();

        foreach (var evt in events)
        {
            var value = _propertyAccessor.GetValue(evt, field.PropertyPath);
            if (value != null)
            {
                valueCounts.TryGetValue(value, out var count);
                valueCounts[value] = count + 1;
            }
        }

        var availableValues = new ObservableCollection<FilterValueItem>();
        foreach (var (value, count) in valueCounts.OrderBy(kv => kv.Key.ToString()))
        {
            var displayName = GetEnumDisplayName(value);
            availableValues.Add(new FilterValueItem(value, displayName, count));
        }

        var descriptor = new FilterDescriptor(
            id,
            field.DisplayName,
            FilterType.EnumMultiSelect,
            field.PropertyPath,
            evt => CheckEnumFilter(evt, field.PropertyPath, availableValues),
            field.Category,
            field.FilterDisplayOrder);

        foreach (var item in availableValues)
            descriptor.AvailableValues.Add(item);

        descriptor.InitializeFilteredValues();

        return descriptor;
    }
    
    private FilterDescriptor CreateStringFilter(string id, DiscoveredField field, List<EventBase> events)
    {
        var valueCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var evt in events)
        {
            var value = _propertyAccessor.GetValue(evt, field.PropertyPath)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                valueCounts.TryGetValue(value, out var count);
                valueCounts[value] = count + 1;
            }
        }

        var availableValues = new ObservableCollection<FilterValueItem>();
        foreach (var (value, count) in valueCounts.OrderByDescending(kv => kv.Value).Take(100))
        {
            availableValues.Add(new FilterValueItem(value, value, count));
        }

        var descriptor = new FilterDescriptor(
            id,
            field.DisplayName,
            FilterType.StringMultiSelect,
            field.PropertyPath,
            evt => CheckStringFilter(evt, field.PropertyPath, availableValues),
            field.Category,
            field.FilterDisplayOrder);

        foreach (var item in availableValues)
            descriptor.AvailableValues.Add(item);

        descriptor.InitializeFilteredValues();
        
        return descriptor;
    }

    private FilterDescriptor CreateNumericRangeFilter(string id, DiscoveredField field, List<EventBase> events)
    {
        var values = events
            .Select(evt => _propertyAccessor.GetValue(evt, field.PropertyPath))
            .Where(v => v != null)
            .Cast<IComparable>()
            .ToList();

        if (values.Count == 0)
            return null!;

        var min = values.Min();
        var max = values.Max();

        var descriptor = new FilterDescriptor(
            id,
            field.DisplayName,
            FilterType.NumericRange,
            field.PropertyPath,
            evt => true,
            field.Category,
            field.FilterDisplayOrder)
        {
            MinValue = min,
            MaxValue = max,
            CurrentMinValue = min,
            CurrentMaxValue = max
        };

        descriptor.UpdatePredicate(evt =>
        {
            var value = _propertyAccessor.GetValue(evt, descriptor.PropertyPath) as IComparable;
            if (value == null)
                return false;

            // Границы могут быть строкой из TextBox — приводим к типу значения, чтобы CompareTo не бросал.
            var currentMin = CoerceComparable(descriptor.CurrentMinValue, value.GetType());
            var currentMax = CoerceComparable(descriptor.CurrentMaxValue, value.GetType());

            if (currentMin != null && value.CompareTo(currentMin) < 0)
                return false;

            if (currentMax != null && value.CompareTo(currentMax) > 0)
                return false;

            return true;
        });

        return descriptor;
    }

    private FilterDescriptor CreateDateTimeRangeFilter(string id, DiscoveredField field, List<EventBase> events)
    {
        var values = events
            .Select(evt => _propertyAccessor.GetValue(evt, field.PropertyPath))
            .Where(v => v != null)
            .Cast<DateTime>()
            .ToList();

        if (values.Count == 0)
            return null!;

        var min = values.Min();
        var max = values.Max();

        var descriptor = new FilterDescriptor(
            id,
            field.DisplayName,
            FilterType.DateTimeRange,
            field.PropertyPath,
            evt => false,
            field.Category,
            field.FilterDisplayOrder)
        {
            MinValue = min,
            MaxValue = max,
            CurrentMinValue = min,
            CurrentMaxValue = max
        };

        descriptor.UpdatePredicate(evt =>
        {
            var value = _propertyAccessor.GetValue(evt, descriptor.PropertyPath);
            if (value is not DateTime dateTime)
                return false;

            // Граница может быть DateTime либо строкой (TwoWay-биндинг TextBox). Не бросаем на
            // непарсимом значении — просто не ограничиваем по этой границе.
            if (TryCoerceDateTime(descriptor.CurrentMinValue, out var currentMin) && dateTime < currentMin)
                return false;

            if (TryCoerceDateTime(descriptor.CurrentMaxValue, out var currentMax) && dateTime > currentMax)
                return false;

            return true;
        });

        return descriptor;
    }

    // TextSearch: значения не перечисляем (высокая кардинальность — SQL, сообщения ошибок).
    // Предикат читает descriptor.SearchText вживую и матчит по подстроке (Contains).
    private FilterDescriptor CreateTextSearchFilter(string id, DiscoveredField field)
    {
        var descriptor = new FilterDescriptor(
            id,
            field.DisplayName,
            FilterType.TextSearch,
            field.PropertyPath,
            _ => true,
            field.Category,
            field.FilterDisplayOrder);

        descriptor.UpdatePredicate(evt => CheckTextSearchFilter(evt, descriptor));

        return descriptor;
    }

    #endregion

    #region Check Filters

    private bool CheckTextSearchFilter(EventBase evt, FilterDescriptor descriptor)
    {
        var query = descriptor.SearchText;
        if (string.IsNullOrWhiteSpace(query))
            return true; // пустой запрос ничего не отсекает

        var value = _propertyAccessor.GetValue(evt, descriptor.PropertyPath)?.ToString();
        return value != null && value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool CheckEnumFilter(EventBase evt, string propertyPath, ObservableCollection<FilterValueItem> availableValues)
    {
        var included = availableValues.Where(v => v.IsSelected).Select(v => v.Value).ToHashSet();
        var excluded = availableValues.Where(v => v.IsExcluded).Select(v => v.Value).ToHashSet();

        if (included.Count == 0 && excluded.Count == 0)
            return true;

        var value = _propertyAccessor.GetValue(evt, propertyPath);

        // Включённые заданы — значение должно быть среди них.
        if (included.Count > 0 && (value == null || !included.Contains(value)))
            return false;

        // Исключённые — значение не должно быть среди них.
        return value == null || !excluded.Contains(value);
    }

    private bool CheckStringFilter(EventBase evt, string propertyPath, ObservableCollection<FilterValueItem> availableValues)
    {
        var included = availableValues.Where(v => v.IsSelected).Select(v => v.Value.ToString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var excluded = availableValues.Where(v => v.IsExcluded).Select(v => v.Value.ToString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (included.Count == 0 && excluded.Count == 0)
            return true;

        var value = _propertyAccessor.GetValue(evt, propertyPath)?.ToString();

        if (included.Count > 0 && (value == null || !included.Contains(value)))
            return false;

        return value == null || !excluded.Contains(value);
    }

    #endregion

    private string GetEnumDisplayName(object enumValue)
    {
        var type = enumValue.GetType();
        var memberInfo = type.GetMember(enumValue.ToString()!).FirstOrDefault();

        if (memberInfo != null)
        {
            var descAttr = memberInfo.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            if (descAttr != null)
                return descAttr.Description;
        }

        return enumValue.ToString()!;
    }
}