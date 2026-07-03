using FirebirdTraceAnalyzer.Services.Filtering;

namespace FirebirdTraceAnalyzer.Interfaces.Plugins;

/// <summary>Интерфейс для плагинов, предоставляющих кастомные фильтры</summary>
public interface IFilterPlugin : IAnalyzerPlugin
{
    /// <summary>Возвращает кастомные фильтры, добавляемые плагином.</summary>
    IEnumerable<FilterDescriptor> GetFilters();
}
