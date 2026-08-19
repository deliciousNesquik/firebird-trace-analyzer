using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.EventLinking;

/// <summary>
/// Defines a service for building a chain of related events based on a focus event and a list of all events.
/// </summary>
public interface IEventChainService
{
    
    /// <summary>
    /// Builds a chain of related events for the given focus event, ordered by time.
    /// The chain is determined by the attach/detach pair surrounding the focus event,
    /// ensuring correct handling of reused AttachmentIds within a single trace file.
    /// </summary>
    /// <param name="focus">The focus event for which to build the chain.</param>
    /// <param name="allEvents">The list of all events to consider.</param>
    /// <returns>The list of related events ordered by time.</returns>
    IReadOnlyList<EventBase> BuildChain(EventBase focus, IReadOnlyList<EventBase> allEvents);
}
