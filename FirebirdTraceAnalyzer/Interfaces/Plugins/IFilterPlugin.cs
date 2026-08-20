using FirebirdTraceAnalyzer.Services.Filtering;

namespace FirebirdTraceAnalyzer.Interfaces.Plugins;

/// <summary>
/// Represents a plugin that provides custom filters for the Firebird Trace Analyzer.
/// </summary>
public interface IFilterPlugin : IAnalyzerPlugin
{
    /// <summary>
    /// Gets the custom filters provided by the plugin.
    /// </summary>
    IEnumerable<FilterDescriptor> GetFilters();
}
