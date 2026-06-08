using System.Collections.Concurrent;

namespace FirebirdTraceParser.Infrastructure.Caching;

public static class StringPool
{
    private static readonly ConcurrentDictionary<string, string> Pool = new();

    public static string Intern(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return Pool.GetOrAdd(value, s => s);
    }

    public static void Reset() => Pool.Clear();
}