using FirebirdTraceAnalyzer.Services.Plugins;

namespace FirebirdTraceAnalyzer.Interfaces.Plugins;

/// <summary>
/// Represents a service for managing plugins in the Firebird Trace Analyzer application.
/// This service is responsible for discovering, loading, and managing plugins, resolving version conflicts,
/// enabling/disabling plugins, and installing/removing plugin packages.
/// </summary>
public interface IPluginManagerService
{
    /// <summary>
    /// Gets the directory path where plugins are stored.
    /// </summary>
    string PluginsDirectory { get; }

    /// <summary>
    /// Gets a read-only list of all discovered plugins, including their metadata and status.
    /// </summary>
    /// <returns>Full list of discovered plugins <see cref="PluginInfo"/></returns>
    IReadOnlyList<PluginInfo> GetPlugins();
    
    /// <summary>
    /// Scans the plugin directory and its subdirectories, loads all plugins, resolves version conflicts, and computes their statuses.
    /// </summary>
    /// <returns>List of loaded plugins</returns>
    IReadOnlyList<PluginInfo> LoadAllPlugins();
    
    /// <summary>
    /// Gets the active (enabled and non-shadowed) sorting plugins.
    /// </summary>
    /// <returns>List of active sorting plugins</returns>
    IEnumerable<ISortPlugin> GetSortPlugins();
    
    /// <summary>
    /// Gets the active (enabled and non-shadowed) filtering plugins.
    /// </summary>
    /// <returns>List of active filtering plugins</returns>
    IEnumerable<IFilterPlugin> GetFilterPlugins();

    /// <summary>
    /// Sets the enabled/disabled status of a specific plugin instance identified by its file path and ID.
    /// </summary>
    /// <param name="filePath">The file path of the plugin.</param>
    /// <param name="id">The ID of the plugin.</param>
    /// <param name="enabled">The enabled status of the plugin.</param>
    void SetEnabled(string filePath, string id, bool enabled);

    /// <summary>
    /// Gets the enabled/disabled status of a specific plugin instance identified by its file path and ID.
    /// </summary>
    /// <param name="filePath">The file path of the plugin.</param>
    /// <param name="id">The ID of the plugin.</param>
    /// <returns>The enabled status of the plugin.</returns>
    bool IsEnabled(string filePath, string id);

    /// <summary>
    /// Gets the collision groups of plugins, where each group contains plugins with the same ID but different versions.
    /// </summary>
    /// <returns>The collision groups.</returns>
    IReadOnlyList<IReadOnlyList<PluginInfo>> GetCollisionGroups();

    /// <summary>
    /// Determines whether there are any unresolved collisions among the loaded plugins.
    /// </summary>
    /// <returns>True if there are unresolved collisions, false otherwise.</returns>
    bool HasUnresolvedCollisions();

    /// <summary>
    /// Installs a plugin package from the specified source path.
    /// The package is copied to the plugins directory, and the plugin is loaded and registered.
    /// </summary>
    /// <param name="sourcePath">The path to the plugin package.</param>
    /// <returns>True if the plugin was installed successfully, false otherwise.</returns>
    bool InstallPlugin(string sourcePath);

    /// <summary>
    /// Deletes a plugin package from the specified folder path.
    /// If the plugin is currently loaded, it will be marked for deletion and removed on the next application restart.
    /// </summary>
    /// <param name="folderPath">The path to the plugin package folder.</param>
    /// <returns>A tuple indicating whether the package was deleted immediately and whether it is pending deletion.</returns>
    (bool DeletedNow, bool Pending) DeletePackage(string folderPath);
}
