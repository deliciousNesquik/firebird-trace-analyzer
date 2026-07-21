using FirebirdTraceAnalyzer.Services;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Sorting;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// P3: ApplySort извлекает ключ один раз на событие (decorate-sort-undecorate) при наличии
/// KeySelector, иначе — прежним Comparer. Проверяем оба пути и стабильность порядка результата.
/// </summary>
public sealed class SortingServiceTests
{
    private static SortingService NewService() =>
        new(new EventPropertyAccessor(), new FieldDiscoveryService());

    private static EventBase At(DateTime ts) =>
        new TraceInitEvent
        {
            Timestamp = ts, TraceId = 1, HexTraceId = "0x01", EventType = EventType.TraceInit,
            Session = new TraceSessionInfo { SessionId = 100 }
        };

    private static readonly DateTime T1 = new(2026, 7, 21, 10, 0, 0);
    private static readonly DateTime T2 = new(2026, 7, 21, 11, 0, 0);
    private static readonly DateTime T3 = new(2026, 7, 21, 12, 0, 0);

    [Fact]
    public void KeySelectorPath_SortsAscendingAndDescending()
    {
        var svc = NewService();
        svc.RegisterCustomSort(new SortDescriptor(
            "ts", "By time", Comparer, isDefault: false)
        {
            KeySelector = evt => evt.Timestamp
        });

        var events = new[] { At(T3), At(T1), At(T2) };

        var asc = svc.ApplySort(events, "ts").ToList();
        Assert.Equal([T1, T2, T3], asc.Select(e => e.Timestamp).ToArray());

        var desc = svc.ApplySort(events, "ts", descending: true).ToList();
        Assert.Equal([T3, T2, T1], desc.Select(e => e.Timestamp).ToArray());
    }

    [Fact]
    public void ComparerFallbackPath_StillWorks_WithoutKeySelector()
    {
        var svc = NewService();
        svc.RegisterCustomSort(new SortDescriptor("ts", "By time", Comparer, isDefault: false));
        // без KeySelector

        var events = new[] { At(T3), At(T1), At(T2) };
        var asc = svc.ApplySort(events, "ts").ToList();
        Assert.Equal([T1, T2, T3], asc.Select(e => e.Timestamp).ToArray());
    }

    [Fact]
    public void UnknownSortId_ReturnsInputUnchanged()
    {
        var svc = NewService();
        var events = new[] { At(T3), At(T1) };
        Assert.Equal(events, svc.ApplySort(events, "nope").ToArray());
    }

    private static int Comparer(EventBase a, EventBase b, bool descending)
    {
        var r = a.Timestamp.CompareTo(b.Timestamp);
        return descending ? -r : r;
    }
}
