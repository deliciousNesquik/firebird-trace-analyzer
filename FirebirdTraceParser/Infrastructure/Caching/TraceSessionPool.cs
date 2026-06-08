using System.Collections.Concurrent;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceParser.Infrastructure.Caching;

public static class TraceSessionPool
{
    private static readonly ConcurrentDictionary<int, TraceSessionInfo> Pool = new();

    public static TraceSessionInfo Intern(int sessionId) => Pool.GetOrAdd(sessionId, id => new TraceSessionInfo { SessionId = id });

    public static void Reset() => Pool.Clear();
}