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

    private static EventBase Attach(DateTime ts) =>
        new AttachDatabaseEvent
        {
            Timestamp = ts, TraceId = 2, HexTraceId = "0x02", EventType = EventType.AttachDatabase,
            Attachment = new AttachmentInfo
            {
                AttachmentId = 7, DatabasePath = "/db/x.fdb", User = "U", Role = "NONE",
                Charset = "WIN1251", Protocol = "TCPv4", Address = "10.0.0.1/1", Port = 3050,
                ProcessPath = null, ProcessId = null
            }
        };

    [Fact]
    public void GeneratedSorts_DoNotLeakAcrossFiles()
    {
        // Файл A (AttachDatabase) даёт больше сортируемых полей (Attachment.*), чем файл B (TraceInit).
        var fileA = new[] { Attach(T1) };
        var fileB = new[] { At(T1), At(T2) };

        // Сервис, который сначала открыл A, затем B.
        var reused = NewService();
        reused.GetAvailableSorts(fileA);
        var afterA = reused.GetAvailableSorts(fileB).Select(s => s.Id).OrderBy(x => x).ToList();

        // Эталон: свежий сервис, открывший только B (утечки быть не может).
        var fresh = NewService();
        var expected = fresh.GetAvailableSorts(fileB).Select(s => s.Id).OrderBy(x => x).ToList();

        // Открытие A не должно расширять список сортировок для B (иначе поля A утекли в дропдаун B).
        Assert.Equal(expected, afterA);
    }

    private static EventBase AtId(DateTime ts, int traceId) =>
        new TraceInitEvent
        {
            Timestamp = ts, TraceId = traceId, HexTraceId = "0x01", EventType = EventType.TraceInit,
            Session = new TraceSessionInfo { SessionId = 100 }
        };

    // 32 события с ОДИНАКОВЫМ Timestamp (все ключи равны) и разными TraceId в фиксированном
    // «перемешанном» порядке. N>16 → Array.Sort использует НЕстабильный introsort (при N≤16 —
    // insertion sort, который стабилен сам по себе и не проверял бы наш индекс-tiebreaker).
    private static EventBase[] EqualKeyInputs() =>
        Enumerable.Range(0, 32).Select(i => AtId(T1, (i * 7) % 32)).ToArray();

    [Fact]
    public void EqualKeys_PreserveInputOrder_Stable()
    {
        var svc = NewService();
        svc.RegisterCustomSort(new SortDescriptor("ts", "By time", Comparer, isDefault: false)
        {
            KeySelector = e => e.Timestamp
        });

        var input = EqualKeyInputs();
        var expected = input.Select(e => e.TraceId).ToArray();

        var asc = svc.ApplySort(input, "ts").ToList();
        Assert.Equal(expected, asc.Select(e => e.TraceId).ToArray()); // порядок входа сохранён

        // И при descending равные ключи НЕ переворачиваются (стабильность).
        var desc = svc.ApplySort(input, "ts", descending: true).ToList();
        Assert.Equal(expected, desc.Select(e => e.TraceId).ToArray());
    }

    [Fact]
    public void ComparerPath_EqualKeys_PreserveInputOrder_Stable()
    {
        // Путь кастомного Comparer (БЕЗ KeySelector) — как у плагинов. Равные ключи должны сохранять
        // исходный порядок (индекс-tiebreaker в else-ветке ApplySort), в т.ч. при descending.
        var svc = NewService();
        svc.RegisterCustomSort(new SortDescriptor("ts", "By time", Comparer, isDefault: false));

        var input = EqualKeyInputs();
        var expected = input.Select(e => e.TraceId).ToArray();

        var asc = svc.ApplySort(input, "ts").ToList();
        Assert.Equal(expected, asc.Select(e => e.TraceId).ToArray());

        var desc = svc.ApplySort(input, "ts", descending: true).ToList();
        Assert.Equal(expected, desc.Select(e => e.TraceId).ToArray());
    }

    private static EventBase WithDuration(int traceId, int? seconds)
    {
        var evt = AtId(T1, traceId);
        evt.Duration = seconds is null ? null : TimeSpan.FromSeconds(seconds.Value);
        return evt;
    }

    [Fact]
    public void DurationSort_EventsWithoutDuration_GoLast_InBothDirections()
    {
        // Сгенерированная сортировка «Session / Connection Duration»: у большинства событий длительности
        // нет — при сортировке по убыванию они не должны всплывать наверх.
        var events = new[]
        {
            WithDuration(1, null), WithDuration(2, 5), WithDuration(3, null), WithDuration(4, 60), WithDuration(5, 20)
        };
        var svc = NewService();
        svc.GetAvailableSorts(events);
        var sortId = new EventPropertyAccessor().ToSortId(nameof(EventBase.Duration));

        var desc = svc.ApplySort(events, sortId, descending: true).Select(e => e.TraceId).ToArray();
        Assert.Equal([4, 5, 2, 1, 3], desc);

        var asc = svc.ApplySort(events, sortId).Select(e => e.TraceId).ToArray();
        Assert.Equal([2, 5, 4, 1, 3], asc);
    }

    [Fact]
    public void KeySelectorNull_TakesNoPartInOrder_AndKeepsInputOrder()
    {
        // Как в примере SDK: KeySelector возвращает null для событий, к которым сортировка не относится.
        // Событий больше 16, чтобы Array.Sort пошёл нестабильным introsort и работал именно индекс-tiebreaker.
        var input = Enumerable.Range(0, 40).Select(i => AtId(T1.AddMinutes(i % 7), i)).ToArray();
        static bool HasKey(EventBase e) => e.TraceId % 3 == 0;

        var svc = NewService();
        svc.RegisterCustomSort(new SortDescriptor("k", "Key", Comparer, isDefault: false)
        {
            KeySelector = e => HasKey(e) ? e.Timestamp : (object?)null
        });

        var expectedTail = input.Where(e => !HasKey(e)).Select(e => e.TraceId).ToArray();

        foreach (var descending in new[] { false, true })
        {
            var sorted = svc.ApplySort(input, "k", descending).ToList();
            var head = sorted.TakeWhile(HasKey).ToList();

            Assert.Equal(input.Count(HasKey), head.Count); // все со значением — впереди
            Assert.Equal(expectedTail, sorted.Skip(head.Count).Select(e => e.TraceId).ToArray()); // пустые — в конце, как на входе

            var times = head.Select(e => e.Timestamp).ToList();
            var ordered = descending ? times.OrderByDescending(t => t).ToList() : times.OrderBy(t => t).ToList();
            Assert.Equal(ordered, times);
        }
    }

    private static int Comparer(EventBase a, EventBase b, bool descending)
    {
        var r = a.Timestamp.CompareTo(b.Timestamp);
        return descending ? -r : r;
    }
}
