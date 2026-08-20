using Avalonia.Platform.Storage;

namespace FirebirdTraceAnalyzer.Interfaces.Window;

/// <summary>
/// Interface for a service that provides file dialog operations, such as picking files and revealing files in the file manager.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Show a file picker dialog to select one or more trace files.
    /// </summary>
    /// <returns>The list of selected trace files otherwise empty list. <see cref="IStorageFile"/></returns>
    Task<IReadOnlyList<IStorageFile>> PickTraceFilesAsync();

    /// <summary>
    /// Show a file in file manager (Explorer on Windows, Finder on macOS, etc.).
    /// </summary>
    /// <param name="filePath">Absolute path to the file to reveal.</param>
    /// <returns>True if the file was successfully revealed, false otherwise.</returns>
    Task<bool> RevealInFileManagerAsync(string filePath);

    /// <summary>
    /// Show a file picker dialog to select a JSON file to save.
    /// </summary>
    /// <param name="suggestedName">The suggested name for the JSON file.</param>
    /// <returns>The absolute path to the selected JSON file, or null if canceled.</returns>
    Task<string?> PickJsonToSaveAsync(string suggestedName);

    /// <summary>
    /// Show a file picker dialog to select a JSON file to open.
    /// </summary>
    /// <returns>The absolute path to the selected JSON file, or null if canceled.</returns>
    Task<string?> PickJsonToOpenAsync();

    /// <summary>
    /// Show a file picker dialog to select a plugin package to install: a single DLL or a ZIP archive.
    /// </summary>
    /// <returns>The absolute path to the plugin package, or null if canceled.</returns>
    Task<string?> PickPluginPackageAsync();
}