namespace FirebirdTraceAnalyzer.Core;

public static class TypeScanHelper
{
    /// <summary>
    /// Determines whether to scan a nested type when traversing event models.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>true if the type should be scanned; otherwise, false.</returns>
    public static bool ShouldScanNestedType(Type type)
    {
        // Enums, structs and generics also live under FirebirdTraceParser (e.g. EventType,
        // ParsingResult<T>), so these guards must precede the namespace check.
        if (!type.IsClass || type.IsGenericType)
            return false;

        return IsParserModelClass(type);
    }

    /// <summary>
    /// Returns the element type when <paramref name="type"/> is an array or a non-string generic
    /// enumerable (<see cref="IEnumerable{T}"/>); otherwise <c>null</c>.
    /// </summary>
    /// <param name="type">The candidate collection type.</param>
    /// <returns>The element type, or <c>null</c> when the type is not a (non-string) collection.</returns>
    public static Type? GetCollectionElementType(Type type)
    {
        if (type == typeof(string))
            return null;
        if (type.IsArray)
            return type.GetElementType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];
        foreach (var i in type.GetInterfaces())
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return i.GetGenericArguments()[0];
        return null;
    }

    /// <summary>Determines whether the type is a class declared in the FirebirdTraceParser model namespace.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><c>true</c> for parser-model classes; otherwise <c>false</c>.</returns>
    public static bool IsParserModelClass(Type type) =>
        type.IsClass && type.Namespace?.StartsWith("FirebirdTraceParser", StringComparison.Ordinal) == true;

    /// <summary>
    /// Returns the element type of a collection whose elements are parser models (e.g. the
    /// <c>ErrorLines</c> element type of <c>IReadOnlyList&lt;ErrorLines&gt;</c>); otherwise <c>null</c>.
    /// Such collections are expanded into per-element fields (paths carry the <c>"[]"</c> marker),
    /// unlike scalar/enum collections which stay leaves.
    /// </summary>
    /// <param name="type">The candidate collection type.</param>
    /// <returns>The parser-model element type, or <c>null</c> when the type is not such a collection.</returns>
    public static Type? GetParserModelCollectionElementType(Type type)
    {
        var elementType = GetCollectionElementType(type);
        return elementType != null && IsParserModelClass(elementType) ? elementType : null;
    }
}
