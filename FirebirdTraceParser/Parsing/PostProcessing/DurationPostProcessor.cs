using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceParser.Parsing.PostProcessing;

/// <summary>
/// Пост-проход после разбора файла: вычисляет производные длительности по парам событий.
/// <list type="bullet">
///   <item>Длительность trace‑сессии → <see cref="EventBase.Duration"/> у <see cref="TraceFinishEvent"/> = fini − init (по SessionId).</item>
///   <item>Время жизни подключения → <see cref="EventBase.Duration"/> у <see cref="DetachDatabaseEvent"/> = detach − attach (по AttachmentId).</item>
/// </list>
/// Если пара неполная (нет начала в этом файле) — длительность остаётся <c>null</c>. Поле производное:
/// пересчитывается при каждой сборке полного списка событий файла (после парса и после чтения из
/// хранилища), поэтому не требует персистентности. Один проход вперёд корректен, т.к. события в
/// трейсе идут в хронологическом порядке (init/attach раньше своих fini/detach). Повторно
/// использованные id обрабатываются по правилу «последнее начало до конца».
/// </summary>
public static class DurationPostProcessor
{
    public static void Apply(IReadOnlyList<EventBase> events)
    {
        if (events is null || events.Count == 0)
            return;

        var sessionStart = new Dictionary<int, DateTime>();
        var attachStart = new Dictionary<long, DateTime>();

        foreach (var evt in events)
        {
            switch (evt)
            {
                case TraceInitEvent init:
                    sessionStart[init.Session.SessionId] = init.Timestamp;
                    break;

                case TraceFinishEvent fini
                    when sessionStart.TryGetValue(fini.Session.SessionId, out var startedAt):
                    var sessionDuration = fini.Timestamp - startedAt;
                    if (sessionDuration >= TimeSpan.Zero)
                        fini.Duration = sessionDuration;
                    break;

                case AttachDatabaseEvent attach:
                    attachStart[attach.Attachment.AttachmentId] = attach.Timestamp;
                    break;

                case DetachDatabaseEvent detach
                    when attachStart.TryGetValue(detach.Attachment.AttachmentId, out var attachedAt):
                    var connectionDuration = detach.Timestamp - attachedAt;
                    if (connectionDuration >= TimeSpan.Zero)
                        detach.Duration = connectionDuration;
                    break;
            }
        }
    }
}
