using System.Collections.Concurrent;
using System.Reflection;
using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Services.EventProperties;

/// <inheritdoc />
public sealed class EventPropertyAccessor : IEventPropertyAccessor
{
    private const string FilterIdPrefix = "filter_";
    private const string SortIdPrefix = "field_";

    private readonly ConcurrentDictionary<(Type Type, string Path), Func<object, object?>?> _getterCache = new();
    private readonly Dictionary<string, string> _filterIdToPath;
    private readonly Dictionary<string, string> _sortIdToPath;
    private readonly HashSet<string> _knownPaths;

    public EventPropertyAccessor()
    {
        var paths = CollectKnownPropertyPaths();
        KnownPropertyPaths = paths;
        _knownPaths = paths.ToHashSet(StringComparer.Ordinal);

        _filterIdToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _sortIdToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            _filterIdToPath[ToFilterId(path)] = path;
            _sortIdToPath[ToSortId(path)] = path;
        }
    }

    public IReadOnlyCollection<string> KnownPropertyPaths { get; }

    public object? GetValue(object target, string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return null;

        var getter = _getterCache.GetOrAdd(
            (target.GetType(), propertyPath),
            static key => CreateGetter(key.Type, key.Path));

        return getter?.Invoke(target);
    }

    public int Compare(object? valueA, object? valueB)
    {
        if (valueA == null && valueB == null)
            return 0;

        if (valueA == null)
            return 1;

        if (valueB == null)
            return -1;

        if (valueA is IComparable comparableA && valueB.GetType() == valueA.GetType())
            return comparableA.CompareTo(valueB);

        return string.Compare(
            valueA.ToString(),
            valueB.ToString(),
            StringComparison.Ordinal);
    }

    public bool TryResolveFilterId(string filterId, out string propertyPath)
    {
        if (_filterIdToPath.TryGetValue(filterId, out propertyPath!))
            return true;

        var naive = TryNaiveIdToPath(filterId, FilterIdPrefix);
        if (naive.Length > 0 && _knownPaths.Contains(naive))
        {
            propertyPath = naive;
            return true;
        }

        propertyPath = string.Empty;
        return false;
    }

    public bool TryResolveSortId(string sortId, out string propertyPath)
    {
        if (_sortIdToPath.TryGetValue(sortId, out propertyPath!))
            return true;

        if (!sortId.StartsWith(SortIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            propertyPath = sortId;
            return propertyPath.Length > 0;
        }

        var naive = TryNaiveIdToPath(sortId, SortIdPrefix);
        if (naive.Length > 0 && _knownPaths.Contains(naive))
        {
            propertyPath = naive;
            return true;
        }

        propertyPath = string.Empty;
        return false;
    }

    public string ToFilterId(string propertyPath)
        => FilterIdPrefix + propertyPath.Replace(".", "_").ToLowerInvariant();

    public string ToSortId(string propertyPath)
        => SortIdPrefix + propertyPath.Replace(".", "_").ToLowerInvariant();

    private static Func<object, object?>? CreateGetter(Type rootType, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        var chain = new List<PropertyInfo>(parts.Length);
        var currentType = rootType;

        foreach (var part in parts)
        {
            var prop = currentType.GetProperty(
                part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop == null)
                return null;

            chain.Add(prop);
            currentType = prop.PropertyType;
        }

        return target =>
        {
            object? current = target;

            foreach (var prop in chain)
            {
                if (current == null)
                    return null;

                current = prop.GetValue(current);
            }

            return current;
        };
    }

    private static HashSet<string> CollectKnownPropertyPaths()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);

        var eventTypes = typeof(EventBase).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(EventBase).IsAssignableFrom(t));

        foreach (var eventType in eventTypes)
            ScanType(eventType, string.Empty, paths, depth: 0);

        return paths;
    }

    private static void ScanType(Type type, string pathPrefix, HashSet<string> paths, int depth)
    {
        if (depth > 3)
            return;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var path = string.IsNullOrEmpty(pathPrefix)
                ? prop.Name
                : $"{pathPrefix}.{prop.Name}";

            // ✅ Add ALL paths (removed attribute check)
            paths.Add(path);

            if (ShouldScanNestedType(prop.PropertyType))
                ScanType(prop.PropertyType, path, paths, depth + 1);
        }
    }

    private static bool ShouldScanNestedType(Type type)
    {
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            return false;

        if (type.IsGenericType)
            return false;

        if (type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
            return false;

        return type.IsClass &&
               type.Namespace?.StartsWith("FirebirdTraceParser", StringComparison.Ordinal) == true;
    }

    private static string TryNaiveIdToPath(string id, string prefix)
    {
        var pathPart = id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? id[prefix.Length..]
            : id;

        if (string.IsNullOrWhiteSpace(pathPart))
            return string.Empty;

        var segments = pathPart.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return string.Empty;

        return string.Join(".", segments.Select(ToPascalCaseSegment));
    }

    private static string ToPascalCaseSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment))
            return segment;

        if (segment.Length == 1)
            return segment.ToUpperInvariant();

        return char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant();
    }
}
