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

        // Событие уровня сессии (trace_init / trace_fini) не принадлежит одному подключению —
        // показываем обзор сессии: её подключения (attach/detach) + сами закладки.
        if (attId is null)
        {
            var session = sameTrace
                .Where(e => e is TraceInitEvent or TraceFinishEvent
                            or AttachDatabaseEvent or DetachDatabaseEvent)
                .ToList();

            if (!session.Contains(focus))
                session.Add(focus);

            return session.OrderBy(e => e.Timestamp).ToList();
        }

        // --- Единая цепочка = подключение: границы по времени (attach ≤ focus ≤ detach) ---
        // Границы через attach/detach защищают от повторного использования AttachmentId в файле.
        var attach = sameTrace
            .OfType<AttachDatabaseEvent>()
            .Where(e => GetAttachmentId(e) == attId && e.Timestamp <= focus.Timestamp)
            .OrderBy(e => e.Timestamp)
            .LastOrDefault();

        var detach = sameTrace
            .OfType<DetachDatabaseEvent>()
            .Where(e => GetAttachmentId(e) == attId && e.Timestamp >= focus.Timestamp)
            .OrderBy(e => e.Timestamp)
            .FirstOrDefault();

        var lo = attach?.Timestamp ?? DateTime.MinValue;
        var hi = detach?.Timestamp ?? DateTime.MaxValue;

        // Нет detach → не заходим за следующий attach с тем же id.
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

        // Нет attach → не заходим раньше предыдущего detach с тем же id.
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

        // Вся активность подключения в границах окна (attach, все запросы/процедуры/триггеры/
        // ошибки, detach — всё с тем же AttachmentId).
        var connection = sameTrace
            .Where(e => GetAttachmentId(e) == attId && e.Timestamp >= lo && e.Timestamp <= hi)
            .ToList();

        var chainStart = connection.Count > 0 ? connection.Min(e => e.Timestamp) : focus.Timestamp;
        var chainEnd = connection.Count > 0 ? connection.Max(e => e.Timestamp) : focus.Timestamp;

        // Сессионные закладки: ближайший trace_init до подключения и trace_fini после.
        var init = sameTrace
            .OfType<TraceInitEvent>()
            .Where(e => e.Timestamp <= chainStart)
            .OrderBy(e => e.Timestamp)
            .LastOrDefault();

        var fini = sameTrace
            .OfType<TraceFinishEvent>()
            .Where(e => e.Timestamp >= chainEnd)
            .OrderBy(e => e.Timestamp)
            .FirstOrDefault();

        var result = new List<EventBase>();
        if (init is not null)
            result.Add(init);
        result.AddRange(connection);
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

    private long? GetAttachmentId(EventBase evt)
        => _propertyAccessor.GetValue(evt, AttachmentIdPath) as long?;
}
