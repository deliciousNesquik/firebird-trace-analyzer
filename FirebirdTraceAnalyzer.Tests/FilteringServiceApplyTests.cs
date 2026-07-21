using FirebirdTraceAnalyzer.Services;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// P1/P2: ApplyFilters компилирует предикаты один раз за проход. Проверяем корректность —
/// особенно диапазон дат со СТРОКОВЫМИ границами (после TwoWay-биндинга TextBox), который
/// раньше требовал парсинга на каждом событии, а наивное "is DateTime" молча сломало бы фильтр.
/// </summary>
public sealed class FilteringServiceApplyTests
{
    private static FilteringService NewService() =>
        new(new EventPropertyAccessor(), new FieldDiscoveryService());

    private static EventBase Init(DateTime ts) =>
        new TraceInitEvent
        {
            Timestamp = ts, TraceId = 1, HexTraceId = "0x01", EventType = EventType.TraceInit,
            Session = new TraceSessionInfo { SessionId = 100 }
        };

    private static EventBase Finish(DateTime ts) =>
        new TraceFinishEvent
        {
            Timestamp = ts, TraceId = 1, HexTraceId = "0x01", EventType = EventType.TraceFinish,
            Session = new TraceSessionInfo { SessionId = 100 }
        };

    [Fact]
    public void NoActiveFilters_ReturnsAll()
    {
        var svc = NewService();
        var events = new[] { Init(new DateTime(2026, 7, 21, 10, 0, 0)) };
        Assert.Equal(events, svc.ApplyFilters(events, Array.Empty<FilterDescriptor>()).ToArray());
    }

    [Fact]
    public void DateTimeRange_WithStringBounds_FiltersCorrectly()
    {
        var svc = NewService();
        var t10 = new DateTime(2026, 7, 21, 10, 0, 0);
        var t11 = new DateTime(2026, 7, 21, 11, 0, 0);
        var t12 = new DateTime(2026, 7, 21, 12, 0, 0);
        var events = new[] { Init(t10), Init(t11), Init(t12) };

        var filter = new FilterDescriptor("ts", "TS", FilterType.DateTimeRange, "Timestamp", _ => true)
        {
            IsActive = true,
            // Границы — СТРОКИ (как их кладёт TwoWay TextBox через ToString()).
            CurrentMinValue = new DateTime(2026, 7, 21, 10, 30, 0).ToString(),
            CurrentMaxValue = new DateTime(2026, 7, 21, 11, 30, 0).ToString()
        };

        var result = svc.ApplyFilters(events, new[] { filter }).ToList();
        Assert.Single(result);
        Assert.Equal(t11, result[0].Timestamp);
    }

    [Fact]
    public void DateTimeRange_WithDateTimeBounds_AlsoWorks()
    {
        var svc = NewService();
        var t10 = new DateTime(2026, 7, 21, 10, 0, 0);
        var t12 = new DateTime(2026, 7, 21, 12, 0, 0);
        var events = new[] { Init(t10), Init(t12) };

        var filter = new FilterDescriptor("ts", "TS", FilterType.DateTimeRange, "Timestamp", _ => true)
        {
            IsActive = true,
            CurrentMinValue = new DateTime(2026, 7, 21, 11, 0, 0), // boxed DateTime
            CurrentMaxValue = new DateTime(2026, 7, 21, 13, 0, 0)
        };

        var result = svc.ApplyFilters(events, new[] { filter }).ToList();
        Assert.Single(result);
        Assert.Equal(t12, result[0].Timestamp);
    }

    [Fact]
    public void DateTimeRange_UnparseableBound_DoesNotThrow_AndIsIgnored()
    {
        var svc = NewService();
        var events = new[] { Init(new DateTime(2026, 7, 21, 10, 0, 0)) };

        var filter = new FilterDescriptor("ts", "TS", FilterType.DateTimeRange, "Timestamp", _ => true)
        {
            IsActive = true,
            CurrentMinValue = "not-a-date",   // абсурд: не должно ронять фильтрацию
            CurrentMaxValue = null
        };

        var result = svc.ApplyFilters(events, new[] { filter }).ToList();
        Assert.Single(result); // граница проигнорирована, событие проходит
    }

    [Fact]
    public void EnumMultiSelect_IncludeAndExclude()
    {
        var svc = NewService();
        var events = new[]
        {
            Init(new DateTime(2026, 7, 21, 10, 0, 0)),   // EventType.TraceInit
            Finish(new DateTime(2026, 7, 21, 10, 1, 0)), // EventType.TraceFinish
        };

        // include TraceInit only
        var include = new FilterDescriptor("et", "ET", FilterType.EnumMultiSelect, "EventType", _ => true) { IsActive = true };
        include.AvailableValues.Add(new FilterValueItem(EventType.TraceInit, "init") { IsSelected = true });
        include.AvailableValues.Add(new FilterValueItem(EventType.TraceFinish, "finish"));
        var inc = svc.ApplyFilters(events, new[] { include }).ToList();
        Assert.Single(inc);
        Assert.Equal(EventType.TraceInit, inc[0].EventType);

        // exclude TraceInit
        var exclude = new FilterDescriptor("et", "ET", FilterType.EnumMultiSelect, "EventType", _ => true) { IsActive = true };
        exclude.AvailableValues.Add(new FilterValueItem(EventType.TraceInit, "init") { IsExcluded = true });
        exclude.AvailableValues.Add(new FilterValueItem(EventType.TraceFinish, "finish"));
        var exc = svc.ApplyFilters(events, new[] { exclude }).ToList();
        Assert.Single(exc);
        Assert.Equal(EventType.TraceFinish, exc[0].EventType);
    }

    [Fact]
    public void EmptyEnumSelection_PassesEverything()
    {
        var svc = NewService();
        var events = new[] { Init(new DateTime(2026, 7, 21, 10, 0, 0)), Finish(new DateTime(2026, 7, 21, 10, 1, 0)) };
        var filter = new FilterDescriptor("et", "ET", FilterType.EnumMultiSelect, "EventType", _ => true) { IsActive = true };
        filter.AvailableValues.Add(new FilterValueItem(EventType.TraceInit, "init"));
        Assert.Equal(2, svc.ApplyFilters(events, new[] { filter }).Count());
    }
}
