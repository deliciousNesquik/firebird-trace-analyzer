using FirebirdTraceAnalyzer.Interfaces.EventLinking;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Services.EventLinking;

/// <inheritdoc />
public sealed class EventChainService : IEventChainService
{
    private const string AttachmentIdPath = "Attachment.AttachmentId";

    private readonly IEventPropertyAccessor _propertyAccessor;

    public EventChainService(IEventPropertyAccessor propertyAccessor)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
    }

    public IReadOnlyList<EventBase> BuildChain(EventBase focus, IReadOnlyList<EventBase> allEvents)
    {
        if (focus is null)
            return [];

        // Все события того же файла (trace-сессии). Порядок исходного списка сохраняется —
        // это даёт стабильную сортировку при одинаковых временных метках.
        var sameTrace = allEvents.Where(e => e.TraceId == focus.TraceId).ToList();

        var attId = GetAttachmentId(focus);

        // --- Границы подключения по времени (attach ≤ focus ≤ detach), защита от повтора id ---
        AttachDatabaseEvent? attach = null;
        DetachDatabaseEvent? detach = null;
        var lo = DateTime.MinValue;
        var hi = DateTime.MaxValue;

        if (attId is not null)
        {
            attach = sameTrace
                .OfType<AttachDatabaseEvent>()
                .Where(e => GetAttachmentId(e) == attId && e.Timestamp <= focus.Timestamp)
                .OrderBy(e => e.Timestamp)
                .LastOrDefault();

            detach = sameTrace
                .OfType<DetachDatabaseEvent>()
                .Where(e => GetAttachmentId(e) == attId && e.Timestamp >= focus.Timestamp)
                .OrderBy(e => e.Timestamp)
                .FirstOrDefault();

            lo = attach?.Timestamp ?? DateTime.MinValue;
            hi = detach?.Timestamp ?? DateTime.MaxValue;

            if (detach is null)
            {
                var nextAttach = sameTrace
                    .OfType<AttachDatabaseEvent>()
                    .Where(e => GetAttachmentId(e) == attId && e.Timestamp > focus.Timestamp)
                    .OrderBy(e => e.Timestamp)
                    .FirstOrDefault();
                if (nextAttach is not null)
                    hi = nextAttach.Timestamp.AddTicks(-1);
            }

            if (attach is null)
            {
                var prevDetach = sameTrace
                    .OfType<DetachDatabaseEvent>()
                    .Where(e => GetAttachmentId(e) == attId && e.Timestamp < focus.Timestamp)
                    .OrderBy(e => e.Timestamp)
                    .LastOrDefault();
                if (prevDetach is not null)
                    lo = prevDetach.Timestamp.AddTicks(1);
            }
        }

        // --- События самой операции ---
        var operation = BuildOperationEvents(focus, sameTrace, attId, lo, hi);

        if (!operation.Contains(focus))
            operation.Add(focus);

        var opStart = operation.Min(e => e.Timestamp);
        var opEnd = operation.Max(e => e.Timestamp);

        // --- Сессионные закладки: ближайший trace_init до операции и trace_fini после ---
        var init = sameTrace
            .OfType<TraceInitEvent>()
            .Where(e => e.Timestamp <= opStart)
            .OrderBy(e => e.Timestamp)
            .LastOrDefault();

        var fini = sameTrace
            .OfType<TraceFinishEvent>()
            .Where(e => e.Timestamp >= opEnd)
            .OrderBy(e => e.Timestamp)
            .FirstOrDefault();

        // --- Сборка: init, attach, операция, detach, fini ---
        var result = new List<EventBase>();
        if (init is not null)
            result.Add(init);
        if (attach is not null)
            result.Add(attach);
        result.AddRange(operation);
        if (detach is not null)
            result.Add(detach);
        if (fini is not null)
            result.Add(fini);

        var ordered = result
            .Distinct()
            .OrderBy(e => e.Timestamp)
            .ToList();

        // Страховка: фокус обязан присутствовать в цепочке.
        if (!ordered.Contains(focus))
            ordered.Add(focus);

        return ordered;
    }

    /// <summary>
    /// Собирает события одной операции вокруг фокуса. Для statement/procedure/trigger — единичное
    /// выполнение (start → restart → finish/failed), ограниченное соседними выполнениями (защита
    /// от повторного использования дескриптора/имени). Для события уровня сессии (trace_init/fini)
    /// — обзор подключений. Для attach/detach/error операции нет — только сам фокус.
    /// </summary>
    private List<EventBase> BuildOperationEvents(EventBase focus, List<EventBase> sameTrace, long? attId,
        DateTime lo, DateTime hi)
    {
        var matcher = BuildOperationMatcher(focus);

        if (matcher is not null)
        {
            var candidates = sameTrace
                .Where(e => e.Timestamp >= lo && e.Timestamp <= hi && matcher(e))
                .OrderBy(e => e.Timestamp)
                .ToList();

            // Кронштейн вокруг фокуса: ближайший start ≤ focus и ближайший finish ≥ focus.
            var startEvt = candidates.LastOrDefault(e => IsOperationStart(e) && e.Timestamp <= focus.Timestamp);
            var endEvt = candidates.FirstOrDefault(e => IsOperationFinish(e) && e.Timestamp >= focus.Timestamp);

            var opLo = startEvt?.Timestamp ?? lo;
            var opHi = endEvt?.Timestamp ?? hi;

            // Нет start в этом выполнении → не заходим за предыдущий finish той же операции.
            if (startEvt is null)
            {
                var prevEnd = candidates.LastOrDefault(e => IsOperationFinish(e) && e.Timestamp < focus.Timestamp);
                if (prevEnd is not null)
                    opLo = prevEnd.Timestamp.AddTicks(1);
            }

            // Нет finish (не залогирован) → не заходим за следующий start той же операции.
            if (endEvt is null)
            {
                var nextStart = candidates.FirstOrDefault(e => IsOperationStart(e) && e.Timestamp > focus.Timestamp);
                if (nextStart is not null)
                    opHi = nextStart.Timestamp.AddTicks(-1);
            }

            return candidates.Where(e => e.Timestamp >= opLo && e.Timestamp <= opHi).ToList();
        }

        // Событие уровня сессии (trace_init / trace_fini): обзор подключений сессии.
        if (attId is null)
            return sameTrace.Where(e => e is AttachDatabaseEvent or DetachDatabaseEvent).ToList();

        // attach / detach / error — операции нет, только сам фокус (окружён рамкой).
        return [];
    }

    /// <summary>
    /// Предикат, отбирающий события той же операции, что и фокус (по естественному ключу
    /// в рамках одного подключения). Возвращает null, если у фокуса нет понятия операции.
    /// </summary>
    private static Func<EventBase, bool>? BuildOperationMatcher(EventBase focus)
    {
        switch (focus)
        {
            case StatementEventBase { StatementId: { } sid } s:
            {
                var att = s.Attachment.AttachmentId;
                return e => e is StatementEventBase se && se.StatementId == sid && se.Attachment.AttachmentId == att;
            }
            case ProcedureEventBase p when !string.IsNullOrEmpty(p.ProcedureName):
            {
                var att = p.Attachment.AttachmentId;
                var name = p.ProcedureName;
                return e => e is ProcedureEventBase pe && pe.ProcedureName == name && pe.Attachment.AttachmentId == att;
            }
            case TriggerEventBase t when !string.IsNullOrEmpty(t.TriggerName):
            {
                var att = t.Attachment.AttachmentId;
                var name = t.TriggerName;
                return e => e is TriggerEventBase te && te.TriggerName == name && te.Attachment.AttachmentId == att;
            }
            default:
                return null;
        }
    }

    private static bool IsOperationStart(EventBase e)
        => e is StatementStartEvent or ProcedureStartEvent or TriggerStartEvent;

    private static bool IsOperationFinish(EventBase e)
        => e is StatementFinishEvent or FailedStatementFinishEvent
            or ProcedureFinishEvent or FailedProcedureFinishEvent
            or TriggerFinishEvent or FailedTriggerFinishEvent;

    private long? GetAttachmentId(EventBase evt)
        => _propertyAccessor.GetValue(evt, AttachmentIdPath) as long?;
}
