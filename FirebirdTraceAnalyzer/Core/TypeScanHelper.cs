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

        return type.Namespace?.StartsWith("FirebirdTraceParser", StringComparison.Ordinal) == true;
    }
}
