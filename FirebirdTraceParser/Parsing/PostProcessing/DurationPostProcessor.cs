using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceParser.Parsing.PostProcessing;

/// <summary>
/// Пост-проход по ОБЩЕМУ списку событий (все загруженные файлы вместе): вычисляет производные
/// длительности по парам событий в <see cref="EventBase.Duration"/>.
/// <list type="bullet">
///   <item><see cref="TraceFinishEvent"/> — время жизни серверного процесса в trace‑сессии: fini − init
///   одного процесса (заголовок <c>(pid:hex)</c> + SessionId). На Classic все процессы пишут в одну
///   SESSION_N, поэтому пара ищется по процессу, а не по номеру сессии. Длительность ставится, только если
///   между init и fini виден ATTACH этого процесса: иначе процесс жил до начала лога (init пишется лениво,
///   при первой активности в новой сессии), и fini − init занизил бы время жизни.</item>
///   <item><see cref="DetachDatabaseEvent"/> — время жизни подключения: detach − attach (база + ATT; в одном
///   трейсе бывает несколько баз со своими счётчиками ATT), в том числе когда пара лежит в разных файлах.</item>
/// </list>
/// События обходятся по времени (стабильно; уже упорядоченный список не пересортировывается), завершённая
/// пара сразу отсекается из рабочего состояния. Результат возвращается для КАЖДОГО TraceFinish/Detach,
/// включая null, — повторный расчёт после закрытия файла стирает устаревшие значения.
/// </summary>
public static class DurationPostProcessor
{
    /// <summary>Вычисляет длительности и сразу записывает их в события.</summary>
    public static void Apply(IReadOnlyList<EventBase> events) => Assign(Compute(events));

    /// <summary>
    /// Вычисляет длительности, не меняя события: безопасно вызывать в фоне на снимке списка,
    /// пока UI читает текущие значения. Записать результат — <see cref="Assign"/>.
    /// </summary>
    public static IReadOnlyList<DurationResult> Compute(IReadOnlyList<EventBase> events)
    {
        if (events is null || events.Count == 0)
            return [];

        // OrderBy стабилен: при равном времени сохраняется порядок файла (init раньше attach своего процесса).
        var ordered = IsChronological(events) ? events : events.OrderBy(e => e.Timestamp).ToList();

        var processes = new Dictionary<(int TraceId, string HexTraceId), ProcessStart>();
        var attaches = new Dictionary<(string DatabasePath, long AttachmentId), DateTime>();
        var results = new List<DurationResult>();

        foreach (var evt in ordered)
        {
            switch (evt)
            {
                case TraceInitEvent init:
                    processes[(init.TraceId, init.HexTraceId)] =
                        new ProcessStart(init.Session.SessionId, init.Timestamp, AttachSeen: false);
                    break;

                case AttachDatabaseEvent attach:
                    var processKey = (attach.TraceId, attach.HexTraceId);
                    if (processes.TryGetValue(processKey, out var process))
                        processes[processKey] = process with { AttachSeen = true };

                    attaches[(attach.Attachment.DatabasePath, attach.Attachment.AttachmentId)] = attach.Timestamp;
                    break;

                case TraceFinishEvent fini:
                    results.Add(new DurationResult(fini, ProcessLifetime(processes, fini)));
                    break;

                case DetachDatabaseEvent detach:
                    results.Add(new DurationResult(detach, ConnectionLifetime(attaches, detach)));
                    break;
            }
        }

        return results;
    }

    /// <summary>Записывает вычисленные <see cref="Compute"/> длительности (включая сброс в null).</summary>
    public static void Assign(IReadOnlyList<DurationResult> results)
    {
        foreach (var result in results)
            result.Event.Duration = result.Duration;
    }

    private static TimeSpan? ProcessLifetime(
        Dictionary<(int TraceId, string HexTraceId), ProcessStart> processes, TraceFinishEvent fini)
    {
        var key = (fini.TraceId, fini.HexTraceId);
        if (!processes.TryGetValue(key, out var start) || start.SessionId != fini.Session.SessionId)
            return null;

        processes.Remove(key);
        return start.AttachSeen ? NonNegative(fini.Timestamp - start.StartedAt) : null;
    }

    private static TimeSpan? ConnectionLifetime(
        Dictionary<(string DatabasePath, long AttachmentId), DateTime> attaches, DetachDatabaseEvent detach)
    {
        var key = (detach.Attachment.DatabasePath, detach.Attachment.AttachmentId);
        if (!attaches.Remove(key, out var attachedAt))
            return null;

        return NonNegative(detach.Timestamp - attachedAt);
    }

    private static TimeSpan? NonNegative(TimeSpan value) => value >= TimeSpan.Zero ? value : null;

    private static bool IsChronological(IReadOnlyList<EventBase> events)
    {
        for (var i = 1; i < events.Count; i++)
            if (events[i].Timestamp < events[i - 1].Timestamp)
                return false;

        return true;
    }

    private readonly record struct ProcessStart(int SessionId, DateTime StartedAt, bool AttachSeen);
}

/// <summary>Вычисленная длительность для события (null — пару честно посчитать нельзя).</summary>
public readonly record struct DurationResult(EventBase Event, TimeSpan? Duration);
