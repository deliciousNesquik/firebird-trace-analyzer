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
    /// Считает значения/счётчики/диапазоны фильтров по событиям (тяжёлый O(N)-проход).
    /// Ничего не пишет в дескрипторы — безопасно вызывать в фоновом потоке.
    /// </summary>
    FilterValueScan ScanFilterValues(IReadOnlyList<EventBase> events, IReadOnlyList<FilterDescriptor> filters);

    /// <summary>
    /// Применяет результат <see cref="ScanFilterValues"/> к дескрипторам (счётчики, новые значения,
    /// границы диапазонов). Пишет в UI-привязанные коллекции — вызывать только на UI-потоке.
    /// </summary>
    void ApplyFilterValues(IReadOnlyList<FilterDescriptor> filters, FilterValueScan scan);
    
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