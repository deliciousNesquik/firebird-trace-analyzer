namespace FirebirdTraceAnalyzer.Core;

/// <summary>
/// Правило обхода вложенных типов при рефлексии по моделям событий.
/// Единая копия для <c>EventPropertyAccessor</c> и <c>FieldDiscoveryService</c>.
/// </summary>
public static class TypeScanHelper
{
    /// <summary>
    /// Нужно ли углубляться в тип (класс модели парсера), либо это лист (примитив, строка,
    /// enum, generic или системный тип).
    /// </summary>
    public static bool ShouldScanNestedType(Type type)
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
}
