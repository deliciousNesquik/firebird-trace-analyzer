namespace FirebirdTraceAnalyzer.Interfaces.Plugins;

/// <summary>Интерфейс для плагинов, предоставляющих кастомные фильтры (на будущее)</summary>
public interface IFilterPlugin : IAnalyzerPlugin
{
    // Заготовка под будущую систему фильтрации
    // IEnumerable<FilterDescriptor> GetFilters();
}