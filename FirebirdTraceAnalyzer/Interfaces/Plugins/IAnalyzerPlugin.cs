namespace FirebirdTraceAnalyzer.Interfaces.Plugins;

/// <summary>
/// Represents a general interface of plugin for the Firebird Trace Analyzer.
/// </summary>
public interface IAnalyzerPlugin
{
    /// <summary>
    /// Gets the unique identifier of the plugin.
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Gets the name of the plugin.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the author of the plugin.
    /// </summary>
    string Author { get; }
    
    /// <summary>
    /// Gets the version of the plugin.
    /// </summary>
    string Version { get; }
}