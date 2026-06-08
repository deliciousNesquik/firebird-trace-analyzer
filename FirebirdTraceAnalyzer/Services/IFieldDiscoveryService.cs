using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Сервис для обнаружения доступных полей в событиях трассировки
/// </summary>
public interface IFieldDiscoveryService
{
    /// <summary>
    /// Получает все общие поля (пересечение) для указанных событий
    /// </summary>
    IReadOnlyList<DiscoveredField> GetCommonFields(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Получает все поля для конкретного типа события
    /// </summary>
    IReadOnlyList<DiscoveredField> GetFieldsForType(Type eventType);
    
    /// <summary>
    /// Получает только сортируемые поля
    /// </summary>
    IReadOnlyList<DiscoveredField> GetSortableFields(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Получает только фильтруемые поля
    /// </summary>
    IReadOnlyList<DiscoveredField> GetFilterableFields(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Получает все доступные поля (объединение всех типов)
    /// </summary>
    IReadOnlyList<DiscoveredField> GetAllAvailableFields(IEnumerable<EventBase> events);
    
    /// <summary>
    /// Очищает кэш обнаруженных полей
    /// </summary>
    void ClearCache();
}