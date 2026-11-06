using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;
using FirebirdTraceParser.Parsing.PostProcessing;
using Xunit;

namespace FirebirdTraceParser.Tests;

public class DurationPostProcessorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 9, 0, 0);
    private const string Db = "/db.fdb";

    // Процесс сервера в заголовке события: (pid:hex). На Classic один процесс = одно подключение.
    private static TraceInitEvent Init(int session, DateTime ts, int pid = 1, string hex = "0x1") => new()
    {
        Timestamp = ts, TraceId = pid, HexTraceId = hex, EventType = EventType.TraceInit,
        Session = new TraceSessionInfo { SessionId = session }
    };

    private static TraceFinishEvent Fini(int session, DateTime ts, int pid = 1, string hex = "0x1") => new()
    {
        Timestamp = ts, TraceId = pid, HexTraceId = hex, EventType = EventType.TraceFinish,
        Session = new TraceSessionInfo { SessionId = session }
    };

    private static AttachmentInfo Att(long id, string db) => new()
    {
        DatabasePath = db, AttachmentId = id, User = "U", Role = "R",
        Charset = "UTF8", Protocol = "TCPv4", Address = "10.0.0.1", Port = 0
    };

    private static AttachDatabaseEvent Attach(long id, DateTime ts, int pid = 1, string hex = "0x1", string db = Db) => new()
    {
        Timestamp = ts, TraceId = pid, HexTraceId = hex, EventType = EventType.AttachDatabase,
        Attachment = Att(id, db)
    };

    private static DetachDatabaseEvent Detach(long id, DateTime ts, int pid = 1, string hex = "0x1", string db = Db) => new()
    {
        Timestamp = ts, TraceId = pid, HexTraceId = hex, EventType = EventType.DetachDatabase,
        Attachment = Att(id, db)
    };

    [Fact]
    public void Trace_fini_duration_is_process_lifetime_when_its_attach_is_seen()
    {
        var fini = Fini(6719, T0.AddMilliseconds(5900));
        var events = new EventBase[] { Init(6719, T0), Attach(100, T0.AddMilliseconds(1)), fini };

        DurationPostProcessor.Apply(events);

        Assert.Equal(TimeSpan.FromMilliseconds(5900), fini.Duration);
    }

    [Fact]
    public void Trace_fini_without_matching_init_stays_null()
    {
        var fini = Fini(999, T0.AddSeconds(3));
        DurationPostProcessor.Apply(new EventBase[] { fini });
        Assert.Null(fini.Duration);
    }

    [Fact]
    public void Trace_fini_of_process_started_before_the_log_stays_null()
    {
        // Процесс жил до старта сессии: INIT записан лениво при первой активности, ATTACH не виден —
        // fini − init был бы заниженным «временем жизни», поэтому длительность не ставится.
        var fini = Fini(6719, T0.AddMinutes(15));
        var events = new EventBase[] { Init(6719, T0), fini };

        DurationPostProcessor.Apply(events);

        Assert.Null(fini.Duration);
    }

    [Fact]
    public void Classic_processes_sharing_one_session_get_their_own_durations()
    {
        // Регресс: на Classic все процессы пишут в одну SESSION_N. Раньше пара искалась по SessionId,
        // и каждый fini получал время от последнего init ЛЮБОГО процесса.
        var finiA = Fini(6719, T0.AddSeconds(10), pid: 10, hex: "0xa");
        var finiB = Fini(6719, T0.AddSeconds(4), pid: 20, hex: "0xb");
        var events = new EventBase[]
        {
            Init(6719, T0, pid: 10, hex: "0xa"),
            Attach(100, T0, pid: 10, hex: "0xa"),
            Init(6719, T0.AddSeconds(1), pid: 20, hex: "0xb"),
            Attach(200, T0.AddSeconds(1), pid: 20, hex: "0xb"),
            finiB,
            finiA
        };

        DurationPostProcessor.Apply(events);

        Assert.Equal(TimeSpan.FromSeconds(10), finiA.Duration);
        Assert.Equal(TimeSpan.FromSeconds(3), finiB.Duration);
    }

    [Fact]
    public void Connection_duration_is_detach_minus_attach()
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
    public void Same_attachment_id_in_two_databases_does_not_collide()
    {
        // В одном трейсе несколько баз, ATT-счётчик у каждой свой.
        var detach1 = Detach(100, T0.AddSeconds(5), db: "/db1.fdb");
        var detach2 = Detach(100, T0.AddSeconds(3), db: "/db2.fdb");
        var events = new EventBase[]
        {
            Attach(100, T0, db: "/db1.fdb"),
            Attach(100, T0.AddSeconds(1), db: "/db2.fdb"),
            detach2,
            detach1
        };

        DurationPostProcessor.Apply(events);

        Assert.Equal(TimeSpan.FromSeconds(5), detach1.Duration);
        Assert.Equal(TimeSpan.FromSeconds(2), detach2.Duration);
    }

    [Fact]
    public void Connection_spanning_two_files_is_paired_on_the_combined_list()
    {
        var file1 = new EventBase[] { Attach(100, T0) };
        var detach = Detach(100, T0.AddSeconds(30));
        var file2 = new EventBase[] { detach };

        DurationPostProcessor.Apply(file2);
        Assert.Null(detach.Duration); // по одному файлу пары нет

        DurationPostProcessor.Apply(file1.Concat(file2).ToList());
        Assert.Equal(TimeSpan.FromSeconds(30), detach.Duration);
    }

    [Fact]
    public void Files_loaded_out_of_order_give_the_same_result()
    {
        var file1 = new EventBase[] { Attach(100, T0) };
        var detach = Detach(100, T0.AddSeconds(30));
        var file2 = new EventBase[] { detach };

        DurationPostProcessor.Apply(file2.Concat(file1).ToList());

        Assert.Equal(TimeSpan.FromSeconds(30), detach.Duration);
    }

    [Fact]
    public void Recompute_after_closing_a_file_resets_stale_duration()
    {
        var file1 = new EventBase[] { Attach(100, T0) };
        var detach = Detach(100, T0.AddSeconds(30));
        var file2 = new EventBase[] { detach };

        DurationPostProcessor.Apply(file1.Concat(file2).ToList());
        Assert.Equal(TimeSpan.FromSeconds(30), detach.Duration);

        DurationPostProcessor.Apply(file2); // файл с ATTACH закрыт
        Assert.Null(detach.Duration);
    }

    [Fact]
    public void Compute_does_not_touch_events_until_assigned()
    {
        // Compute безопасно звать в фоне на снимке списка: он только читает события.
        var detach = Detach(100, T0.AddSeconds(2));
        var results = DurationPostProcessor.Compute(new EventBase[] { Attach(100, T0), detach });

        Assert.Null(detach.Duration);
        var single = Assert.Single(results);
        Assert.Same(detach, single.Event);
        Assert.Equal(TimeSpan.FromSeconds(2), single.Duration);

        DurationPostProcessor.Assign(results);
        Assert.Equal(TimeSpan.FromSeconds(2), detach.Duration);
    }
}
