using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;
using FirebirdTraceParser.Parsing.PostProcessing;
using Xunit;

namespace FirebirdTraceParser.Tests;

public class DurationPostProcessorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 9, 0, 0);

    private static TraceInitEvent Init(int session, DateTime ts) => new()
    {
        Timestamp = ts, TraceId = 1, HexTraceId = "0x1", EventType = EventType.TraceInit,
        Session = new TraceSessionInfo { SessionId = session }
    };

    private static TraceFinishEvent Fini(int session, DateTime ts) => new()
    {
        Timestamp = ts, TraceId = 1, HexTraceId = "0x1", EventType = EventType.TraceFinish,
        Session = new TraceSessionInfo { SessionId = session }
    };

    private static AttachmentInfo Att(long id) => new()
    {
        DatabasePath = "/db.fdb", AttachmentId = id, User = "U", Role = "R",
        Charset = "UTF8", Protocol = "TCPv4", Address = "10.0.0.1", Port = 0
    };

    private static AttachDatabaseEvent Attach(long id, DateTime ts) => new()
    {
        Timestamp = ts, TraceId = 1, HexTraceId = "0x1", EventType = EventType.AttachDatabase,
        Attachment = Att(id)
    };

    private static DetachDatabaseEvent Detach(long id, DateTime ts) => new()
    {
        Timestamp = ts, TraceId = 1, HexTraceId = "0x1", EventType = EventType.DetachDatabase,
        Attachment = Att(id)
    };

    [Fact]
    public void Session_duration_is_fini_minus_init_by_session_id()
    {
        var fini = Fini(6719, T0.AddMilliseconds(5900));
        var events = new EventBase[] { Init(6719, T0), fini };

        DurationPostProcessor.Apply(events);

        Assert.Equal(TimeSpan.FromMilliseconds(5900), fini.Duration);
    }

    [Fact]
    public void Session_without_matching_init_stays_null()
    {
        var fini = Fini(999, T0.AddSeconds(3));
        DurationPostProcessor.Apply(new EventBase[] { fini });
        Assert.Null(fini.Duration);
    }

    [Fact]
    public void Connection_duration_is_detach_minus_attach_by_attachment_id()
    {
        var detach = Detach(9002153, T0.AddMilliseconds(2500));
        var events = new EventBase[] { Attach(9002153, T0), detach };

        DurationPostProcessor.Apply(events);

        Assert.Equal(TimeSpan.FromMilliseconds(2500), detach.Duration);
    }

    [Fact]
    public void Detach_without_matching_attach_stays_null()
    {
        var detach = Detach(123, T0.AddSeconds(1));
        DurationPostProcessor.Apply(new EventBase[] { detach });
        Assert.Null(detach.Duration);
    }

    [Fact]
    public void Reused_attachment_id_uses_last_attach_before_each_detach()
    {
        var firstDetach = Detach(100, T0.AddSeconds(1));
        var secondDetach = Detach(100, T0.AddSeconds(5));
        var events = new EventBase[]
        {
            Attach(100, T0),
            firstDetach,
            Attach(100, T0.AddSeconds(2)),
            secondDetach
        };

        DurationPostProcessor.Apply(events);

        Assert.Equal(TimeSpan.FromSeconds(1), firstDetach.Duration);
        Assert.Equal(TimeSpan.FromSeconds(3), secondDetach.Duration);
    }

    [Fact]
    public void Pairs_are_matched_by_id_not_by_order()
    {
        // Две сессии вперемешку: длительности считаются по своему SessionId.
        var finiA = Fini(1, T0.AddSeconds(10));
        var finiB = Fini(2, T0.AddSeconds(4));
        var events = new EventBase[]
        {
            Init(1, T0),
            Init(2, T0.AddSeconds(1)),
            finiB,
            finiA
        };

        DurationPostProcessor.Apply(events);

        Assert.Equal(TimeSpan.FromSeconds(10), finiA.Duration);
        Assert.Equal(TimeSpan.FromSeconds(3), finiB.Duration);
    }
}
