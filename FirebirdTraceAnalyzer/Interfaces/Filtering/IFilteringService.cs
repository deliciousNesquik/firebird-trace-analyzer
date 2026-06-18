using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Filtering;

public interface IFilteringService
{
    /// <summary>
    /// Получает все доступные фильтры для коллекции событий.
    /// </summary>
    IReadOnlyList<FilterDescriptor> GetAvailableFilters(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Применяет все активные фильтры к коллекции.
    /// </summary>
    IEnumerable<EventBase> ApplyFilters(IEnumerable<EventBase> events, IEnumerable<FilterDescriptor> filters);
    
    /// <summary>
    /// Регистрирует пользовательский фильтр.
    /// </summary>
    void RegisterCustomFilter(FilterDescriptor descriptor);

    /// <summary>
    /// Создаёт независимую настраиваемую копию фильтра (для дизайнера отчётов) с рабочим
    /// предикатом, привязанным к собственному состоянию копии. Нужна потому, что сервис —
    /// синглтон и кеширует дескрипторы, используемые главной формой: без копии правка фильтра
    /// в дизайнере меняла бы фильтры главной формы.
    /// </summary>
    FilterDescriptor CreateConfigurableClone(FilterDescriptor source);
}