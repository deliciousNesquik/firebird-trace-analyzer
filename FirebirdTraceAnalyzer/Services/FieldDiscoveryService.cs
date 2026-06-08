using System.Reflection;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

public sealed class FieldDiscoveryService : IFieldDiscoveryService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // Кэш полей по типу события
    private readonly Dictionary<Type, List<DiscoveredField>> _fieldCache = new();
    
    // Кэш общих полей по набору типов
    private readonly Dictionary<string, List<DiscoveredField>> _commonFieldsCache = new();

    private const int MaxScanDepth = 3;

    public IReadOnlyList<DiscoveredField> GetCommonFields(IEnumerable<EventBase> events)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0)
            return Array.Empty<DiscoveredField>();

        var eventTypes = eventList
            .Select(e => e.GetType())
            .Distinct()
            .OrderBy(t => t.FullName)
            .ToList();

        if (eventTypes.Count == 0)
            return Array.Empty<DiscoveredField>();

        // Создаём ключ кэша на основе типов
        var cacheKey = string.Join("|", eventTypes.Select(t => t.FullName));
        
        if (_commonFieldsCache.TryGetValue(cacheKey, out var cached))
        {
            Logger.Debug("Returning cached common fields for {TypeCount} type(s)", eventTypes.Count);
            return cached;
        }

        // Получаем поля для каждого типа
        var fieldsByType = eventTypes
            .Select(GetFieldsForType)
            .ToList();

        if (fieldsByType.Count == 0)
            return Array.Empty<DiscoveredField>();

        // Находим пересечение по PropertyPath
        var commonPaths = fieldsByType
            .Skip(1)
            .Aggregate(
                new HashSet<string>(fieldsByType[0].Select(f => f.PropertyPath)),
                (common, typeFields) =>
                {
                    common.IntersectWith(typeFields.Select(f => f.PropertyPath));
                    return common;
                });

        var commonFields = fieldsByType[0]
            .Where(f => commonPaths.Contains(f.PropertyPath))
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Priority)
            .ThenBy(f => f.DisplayName)
            .ToList();

        _commonFieldsCache[cacheKey] = commonFields;

        Logger.Info("Discovered {Count} common field(s) from {TypeCount} event type(s)",
            commonFields.Count, eventTypes.Count);

        return commonFields;
    }

    public IReadOnlyList<DiscoveredField> GetFieldsForType(Type eventType)
    {
        if (_fieldCache.TryGetValue(eventType, out var cached))
            return cached;

        var fields = new List<DiscoveredField>();
        ScanProperties(eventType, string.Empty, fields, depth: 0);

        var sortedFields = fields
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Priority)
            .ThenBy(f => f.DisplayName)
            .ToList();

        _fieldCache[eventType] = sortedFields;

        Logger.Debug("Discovered {Count} field(s) for type {TypeName}",
            sortedFields.Count, eventType.Name);

        return sortedFields;
    }

    public IReadOnlyList<DiscoveredField> GetSortableFields(IEnumerable<EventBase> events)
    {
        return GetCommonFields(events)
            .Where(f => f.IsSortable)
            .ToList();
    }

    public IReadOnlyList<DiscoveredField> GetFilterableFields(IEnumerable<EventBase> events)
    {
        return GetCommonFields(events)
            .Where(f => f.IsFilterable)
            .ToList();
    }

    public IReadOnlyList<DiscoveredField> GetAllAvailableFields(IEnumerable<EventBase> events)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0)
            return Array.Empty<DiscoveredField>();

        var eventTypes = eventList
            .Select(e => e.GetType())
            .Distinct()
            .ToList();

        // Объединяем все поля из всех типов
        var allFields = new Dictionary<string, DiscoveredField>();

        foreach (var eventType in eventTypes)
        {
            var typeFields = GetFieldsForType(eventType);
            
            foreach (var field in typeFields)
            {
                if (!allFields.ContainsKey(field.PropertyPath))
                {
                    allFields[field.PropertyPath] = field;
                }
            }
        }

        var result = allFields.Values
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Priority)
            .ThenBy(f => f.DisplayName)
            .ToList();

        Logger.Info("Discovered {Count} total field(s) from {TypeCount} event type(s)",
            result.Count, eventTypes.Count);

        return result;
    }

    public void ClearCache()
    {
        _fieldCache.Clear();
        _commonFieldsCache.Clear();
        Logger.Info("Field discovery cache cleared");
    }

    private void ScanProperties(Type type, string pathPrefix, List<DiscoveredField> results, int depth)
    {
        if (depth > MaxScanDepth)
            return;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var sortableAttr = prop.GetCustomAttribute<SortableFieldAttribute>();
            var filterableAttr = prop.GetCustomAttribute<FilterableFieldAttribute>();

            var path = string.IsNullOrEmpty(pathPrefix)
                ? prop.Name
                : $"{pathPrefix}.{prop.Name}";

            // Определяем приоритет и категорию
            var priority = sortableAttr?.Priority ?? filterableAttr?.Priority ?? 100;
            var category = sortableAttr?.Category ?? filterableAttr?.Category ?? "General";
            var displayName = sortableAttr?.DisplayName ?? filterableAttr?.DisplayName ?? FormatPropertyName(prop.Name);

            var field = new DiscoveredField
            {
                PropertyPath = path,
                DisplayName = displayName,
                PropertyType = prop.PropertyType,
                Category = category,
                Priority = priority,
                IsSortable = sortableAttr != null,
                IsFilterable = filterableAttr != null,
                FilterType = filterableAttr?.FilterType,
                Format = null,
                PropertyInfo = prop,
                DeclaringType = type
            };

            results.Add(field);

            // Сканируем вложенные типы
            if (ShouldScanNestedType(prop.PropertyType))
            {
                ScanProperties(prop.PropertyType, path, results, depth + 1);
            }
        }
    }
    
    /// <summary>
    /// Форматирует имя свойства для отображения (PascalCase → Pascal Case).
    /// </summary>
    private static string FormatPropertyName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;

        // Добавляем пробелы перед заглавными буквами
        var formatted = System.Text.RegularExpressions.Regex.Replace(
            propertyName,
            "([A-Z])",
            " $1"
        ).Trim();

        return formatted;
    }

    private static bool ShouldScanNestedType(Type type)
    {
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            return false;

        if (type.IsGenericType)
            return false;

        if (type.Namespace?.StartsWith("System") == true)
            return false;

        return type.IsClass &&
               type.Namespace?.StartsWith("FirebirdTraceParser") == true;
    }
}