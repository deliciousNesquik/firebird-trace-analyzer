using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// Элемент цепочки жизненного цикла: событие + признак того, что это выбранное (фокусное).
/// </summary>
public sealed class ChainItemViewModel
{
    public required EventBase Event { get; init; }

    /// <summary>Является ли это событие тем, для которого открыт инспектор (подсветка).</summary>
    public required bool IsFocused { get; init; }
}

/// <summary>
/// ViewModel окна «Инспектор события»: показывает выбранное событие и связанные с ним
/// события жизненного цикла (цепочку). Данные передаются готовыми — VM ничего не вычисляет.
/// </summary>
public sealed class EventInspectorViewModel : ViewModelBase
{
    public EventInspectorViewModel(EventBase focusedEvent, IReadOnlyList<EventBase> chain)
    {
        FocusedEvent = focusedEvent;
        Chain = chain
            .Select(e => new ChainItemViewModel { Event = e, IsFocused = ReferenceEquals(e, focusedEvent) })
            .ToList();

        Title = $"Event Inspector — {focusedEvent.EventType} @ {focusedEvent.Timestamp:yyyy-MM-dd HH:mm:ss}";
        ChainSummary = $"{Chain.Count} related event(s) in the lifecycle chain";
    }

    /// <summary>Выбранное событие (показывается крупно сверху).</summary>
    public EventBase FocusedEvent { get; }

    /// <summary>Цепочка связанных событий, упорядоченная по времени.</summary>
    public IReadOnlyList<ChainItemViewModel> Chain { get; }

    /// <summary>Заголовок окна.</summary>
    public string Title { get; }

    /// <summary>Краткое описание цепочки для подзаголовка.</summary>
    public string ChainSummary { get; }
}
