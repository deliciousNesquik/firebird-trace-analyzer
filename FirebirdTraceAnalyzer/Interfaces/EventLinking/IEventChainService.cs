using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.EventLinking;

/// <summary>
/// Строит «цепочку» жизненного цикла для события трассировки — связанные события одного
/// подключения (attach → exec/error → detach) с сессионными закладками (trace_init/fini).
/// Читает из полного набора событий (source of truth), не завязываясь на текущие фильтры.
/// </summary>
public interface IEventChainService
{
    /// <summary>
    /// Возвращает связанные с <paramref name="focus"/> события, упорядоченные по времени.
    /// Границы подключения определяются парой attach/detach вокруг события (по времени),
    /// чтобы корректно работать при повторном использовании AttachmentId в одном файле.
    /// </summary>
    IReadOnlyList<EventBase> BuildChain(EventBase focus, IReadOnlyList<EventBase> allEvents);
}
