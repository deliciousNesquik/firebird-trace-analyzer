using FirebirdTraceAnalyzer.Services.Sorting;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Sorting;

public interface ISortingService
{
    /// <summary>
    /// Получает все доступные сортировки для текущей коллекции событий.
    /// </summary>
    IReadOnlyList<SortDescriptor> GetAvailableSorts(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Регистрирует пользовательскую сортировку.
    /// </summary>
    void RegisterCustomSort(SortDescriptor descriptor);
    
    /// <summary>
    /// Применяет сортировку к коллекции.
    /// </summary>
    IEnumerable<EventBase> ApplySort(IEnumerable<EventBase> events, string sortId, bool descending = false);
}