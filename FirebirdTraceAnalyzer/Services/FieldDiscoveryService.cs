using System.Reflection;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.Interfaces;
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

    /// <inheritdoc />
    public IReadOnlyList<AnnotationValidationIssue> ValidateAnnotations()
    {
        var issues = new List<AnnotationValidationIssue>();
        var reported = new HashSet<string>(StringComparer.Ordinal);

        var eventTypes = typeof(EventBase).Assembly.GetTypes()
            .Where(t => typeof(EventBase).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var eventType in eventTypes)
        {
            // Пути, которые обнаружение реально произвело для этого типа (включая коллекции с "[]").
            // Аннотация считается недостижимой, только если её путь сюда НЕ попал — напр. глубже
            // MaxScanDepth. Коллекции parser-моделей теперь разворачиваются, поэтому их поля здесь есть.
            var discovered = new HashSet<string>(
                GetFieldsForType(eventType).Select(f => f.PropertyPath), StringComparer.Ordinal);
            ScanForUnreachableAnnotations(eventType, string.Empty, discovered, new HashSet<Type>(), issues, reported);
        }

        foreach (var i in issues)
            Logger.Warn(
                "Filter/sort annotations on '{Element}' are IGNORED (fields: {Fields}) — reached only through " +
                "collection '{Owner}.{Prop}', and the discovery does not reach them (e.g. nested beyond the scan " +
                "depth). Move the attribute closer to the event root, or extend the scan.",
                i.ElementType.Name, string.Join(", ", i.IgnoredFields), i.OwnerType.Name, i.CollectionProperty);

        Logger.Info("Annotation validation finished: {Count} unreachable annotation site(s)", issues.Count);
        return issues;
    }

    /// <summary>
    /// Walks an event type's property graph the same way <see cref="ScanProperties"/> does (descending
    /// into single nested parser models and parser-model collections) and reports annotated fields that
    /// the discovery did not actually surface.
    /// </summary>
    /// <remarks>
    /// A collection-element annotation is only flagged when its dotted path (carrying the <c>"[]"</c>
    /// marker) is absent from <paramref name="discovered"/> — e.g. nested beyond the scan depth. Each
    /// such site is recorded as an <see cref="AnnotationValidationIssue"/>.
    /// </remarks>
    /// <param name="type">The type whose properties are inspected.</param>
    /// <param name="prefix">Dotted path prefix accumulated from parent properties (empty at the root).</param>
    /// <param name="discovered">The property paths the discovery produced for the current event type.</param>
    /// <param name="visited">Types already visited on this walk; guards against reference cycles.</param>
    /// <param name="issues">Accumulates the discovered issues.</param>
    /// <param name="reported">De-duplicates issues by "owner type + property name".</param>
    private static void ScanForUnreachableAnnotations(
        Type type, string prefix, HashSet<string> discovered,
        HashSet<Type> visited, List<AnnotationValidationIssue> issues, HashSet<string> reported)
    {
        if (!visited.Add(type))
            return;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var path = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            var elementType = TypeScanHelper.GetParserModelCollectionElementType(prop.PropertyType);
            if (elementType != null)
            {
                var annotated = new List<string>();
                CollectAnnotatedFields(elementType, $"{path}[]", new HashSet<Type>(), annotated);
                var unreachable = annotated.Where(p => !discovered.Contains(p)).ToList();
                if (unreachable.Count > 0 && reported.Add($"{type.FullName}.{prop.Name}"))
                    issues.Add(new AnnotationValidationIssue(type, prop.Name, elementType, unreachable));
                continue;
            }

            if (TypeScanHelper.ShouldScanNestedType(prop.PropertyType))
                ScanForUnreachableAnnotations(prop.PropertyType, path, discovered, visited, issues, reported);
        }
    }

    /// <summary>
    /// Collects the dotted paths of every property carrying [SortableField]/[FilterableField] on
    /// <paramref name="type"/> and its single nested parser models.
    /// </summary>
    /// <param name="type">The type to scan.</param>
    /// <param name="prefix">Dotted path prefix accumulated from parent properties (empty at the root).</param>
    /// <param name="visited">Types already visited on this walk; guards against reference cycles.</param>
    /// <param name="annotated">Accumulates the dotted paths of annotated properties.</param>
    private static void CollectAnnotatedFields(Type type, string prefix, HashSet<Type> visited, List<string> annotated)
    {
        if (!visited.Add(type))
            return;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var path = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            if (prop.GetCustomAttribute<SortableFieldAttribute>() != null ||
                prop.GetCustomAttribute<FilterableFieldAttribute>() != null)
                annotated.Add(path);

            if (TypeScanHelper.ShouldScanNestedType(prop.PropertyType))
                CollectAnnotatedFields(prop.PropertyType, path, visited, annotated);
        }
    }

    private void ScanProperties(Type type, string pathPrefix, List<DiscoveredField> results, int depth)
    {
        if (depth > MaxScanDepth)
            return;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var path = string.IsNullOrEmpty(pathPrefix)
                ? prop.Name
                : $"{pathPrefix}.{prop.Name}";

            // Контейнерный тип (модель парсера, напр. AttachmentInfo) сам по себе не поле: как колонка
            // он отобразился бы через ToString() ("...AttachmentInfo"). Разворачиваем его в под-поля и
            // НЕ добавляем сам контейнер. Листья — примитивы, строки, enum и generic-коллекции скаляров
            // с осмысленным ToString (напр. "Codes") — добавляем как обычно.
            if (TypeScanHelper.ShouldScanNestedType(prop.PropertyType) && depth < MaxScanDepth)
            {
                ScanProperties(prop.PropertyType, path, results, depth + 1);
                continue;
            }

            // Коллекция parser-моделей (напр. IReadOnlyList<ErrorLines>) — не лист: разворачиваем её
            // элемент в под-поля с маркером "[]" в пути ("Errors[].ErrorCode"), сам контейнер полем не
            // делаем. Резолвинг значения для такого пути — по каждому элементу (см. GetValues), а фильтр
            // работает по семантике «совпал любой элемент».
            var elementType = TypeScanHelper.GetParserModelCollectionElementType(prop.PropertyType);
            if (elementType != null && depth < MaxScanDepth)
            {
                ScanProperties(elementType, $"{path}[]", results, depth + 1);
                continue;
            }

            var sortableAttr = prop.GetCustomAttribute<SortableFieldAttribute>();
            var filterableAttr = prop.GetCustomAttribute<FilterableFieldAttribute>();

            // Определяем  категорию
            var category = sortableAttr?.Category ?? filterableAttr?.Category ?? "General";
            var displayName = sortableAttr?.DisplayName ?? filterableAttr?.DisplayName ?? FormatPropertyName(prop.Name);

            var field = new DiscoveredField
            {
                PropertyPath = path,
                DisplayName = displayName,
                PropertyType = prop.PropertyType,
                Category = category,
                IsSortable = sortableAttr != null,
                IsDefaultSort = sortableAttr?.IsDefault ?? false,
                IsFilterable = filterableAttr != null,
                FilterType = filterableAttr?.FilterType,
                FilterDisplayOrder = filterableAttr?.DisplayOrder ?? 100,
                SortDisplayOrder = sortableAttr?.DisplayOrder ?? 100,
                Format = null,
                PropertyInfo = prop,
                DeclaringType = type
            };

            results.Add(field);
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
}