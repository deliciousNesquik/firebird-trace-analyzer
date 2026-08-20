using FirebirdTraceAnalyzer.Services.Sorting;

namespace FirebirdTraceAnalyzer.Interfaces.Plugins;

/// <summary>
/// Represents a plugin that provides custom sorts for the Firebird Trace Analyzer.
/// </summary>
public interface ISortPlugin : IAnalyzerPlugin
{
    /// <summary>
    /// Gets the custom sorts provided by the plugin.
    /// </summary>
    IEnumerable<SortDescriptor> GetSorts();
}