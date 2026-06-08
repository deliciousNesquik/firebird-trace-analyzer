using FirebirdTraceAnalyzer.Services.Sorting;

namespace FirebirdTraceAnalyzer.Interfaces.Plugins;

/// <summary>Интерфейс для плагинов, предоставляющих кастомные сортировки</summary>
public interface ISortPlugin : IAnalyzerPlugin
{
    IEnumerable<SortDescriptor> GetSorts();
}