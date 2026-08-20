using System;
using System.Collections.Generic;
using System.Linq;
using FirebirdTraceAnalyzer.Services;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Covers collection-element filtering: [FilterableField] attributes declared on a collection element
/// (e.g. <see cref="ErrorLines"/> inside <see cref="ErrorEvent.Errors"/>) are discovered with a "[]"
/// path marker, resolved per element, and applied with "any element matches" semantics. Also asserts
/// that <see cref="FieldDiscoveryService.ValidateAnnotations"/> no longer flags those now-reachable
/// fields.
/// </summary>
public sealed class FieldDiscoveryValidationTests
{
    private static AttachmentInfo Attachment() => new()
    {
        DatabasePath = "db.fdb", AttachmentId = 1, User = "SYSDBA", Role = "NONE",
        Charset = "UTF8", Protocol = "TCPv4", Address = "127.0.0.1", Port = 3050
    };

    private static ErrorEvent Error(params (int code, string message)[] lines) => new()
    {
        Timestamp = new DateTime(2026, 8, 20, 10, 0, 0), TraceId = 1, HexTraceId = "0x01",
        EventType = EventType.Error, Attachment = Attachment(), Component = "JStatement::fetch",
        Errors = lines.Select(l => new ErrorLines { ErrorCode = l.code, Message = l.message }).ToList()
    };

    [Fact]
    public void GetFieldsForType_DiscoversCollectionElementFields_WithBracketMarker()
    {
        var fields = new FieldDiscoveryService().GetFieldsForType(typeof(ErrorEvent));

        var code = fields.SingleOrDefault(f => f.PropertyPath == "Errors[].ErrorCode");
        var message = fields.SingleOrDefault(f => f.PropertyPath == "Errors[].Message");

        Assert.NotNull(code);
        Assert.True(code!.IsFilterable);
        Assert.Equal(FilterType.EnumMultiSelect, code.FilterType);

        Assert.NotNull(message);
        Assert.True(message!.IsFilterable);
        Assert.Equal(FilterType.TextSearch, message.FilterType);
    }

    [Fact]
    public void ValidateAnnotations_DoesNotFlagErrorLines_WhenReachableThroughCollection()
    {
        var issues = new FieldDiscoveryService().ValidateAnnotations();

        Assert.DoesNotContain(issues, i => i.ElementType == typeof(ErrorLines));
    }

    [Fact]
    public void GetValues_ResolvesEveryCollectionElement()
    {
        var accessor = new EventPropertyAccessor();
        var evt = Error((335544345, "lock conflict"), (335544321, "arithmetic exception"));

        var codes = accessor.GetValues(evt, "Errors[].ErrorCode").ToList();

        Assert.Equal(new object?[] { 335544345, 335544321 }, codes);
    }

    [Fact]
    public void ApplyFilters_EnumMultiSelectOnCollectionCode_KeepsOnlyMatchingEvents()
    {
        var svc = new FilteringService(new EventPropertyAccessor(), new FieldDiscoveryService());
        var withLock = Error((335544345, "lock conflict"));
        var withMath = Error((335544321, "arithmetic exception"));

        var filter = new FilterDescriptor(
            "err", "Error Code", FilterType.EnumMultiSelect, "Errors[].ErrorCode", _ => true) { IsActive = true };
        filter.AvailableValues.Add(new FilterValueItem(335544345, "335544345") { IsSelected = true });
        filter.AvailableValues.Add(new FilterValueItem(335544321, "335544321"));

        var result = svc.ApplyFilters(new EventBase[] { withLock, withMath }, new[] { filter }).ToList();

        Assert.Single(result);
        Assert.Same(withLock, result[0]);
    }

    [Fact]
    public void ApplyFilters_TextSearchOnCollectionMessage_MatchesAnyElement()
    {
        var svc = new FilteringService(new EventPropertyAccessor(), new FieldDiscoveryService());
        var hit = Error((1, "no permission"), (2, "deadlock detected"));
        var miss = Error((3, "arithmetic exception"));

        var filter = new FilterDescriptor(
            "msg", "Error Message", FilterType.TextSearch, "Errors[].Message", _ => true)
        {
            IsActive = true,
            SearchText = "deadlock"
        };

        var result = svc.ApplyFilters(new EventBase[] { hit, miss }, new[] { filter }).ToList();

        Assert.Single(result);
        Assert.Same(hit, result[0]);
    }

    [Fact]
    public void ScanFilterValues_CountsEachCollectionElementValue()
    {
        var svc = new FilteringService(new EventPropertyAccessor(), new FieldDiscoveryService());
        var events = new EventBase[]
        {
            Error((335544345, "a"), (335544321, "b")),
            Error((335544345, "c"))
        };

        var filter = new FilterDescriptor(
            "err", "Error Code", FilterType.EnumMultiSelect, "Errors[].ErrorCode", _ => true);

        var scan = svc.ScanFilterValues(events, new[] { filter });
        var counts = scan.MultiSelectCounts[filter.Id];

        Assert.Equal(2, counts[335544345]);
        Assert.Equal(1, counts[335544321]);
    }
}
