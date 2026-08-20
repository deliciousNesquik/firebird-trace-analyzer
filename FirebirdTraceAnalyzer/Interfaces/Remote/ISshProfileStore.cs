using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
/// Represents a store for SSH connection profiles, providing methods to load and save profiles from/to a JSON file.
/// </summary>
public interface ISshProfileStore
{
    /// <summary>
    /// File path to the JSON file where SSH connection profiles are stored.
    /// This property is read-only and provides the location of the profile storage file.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Loads the SSH connection profiles from the JSON file.
    /// </summary>
    /// <returns>The list of loaded SSH connection profiles otherwise null.</returns>
    IReadOnlyList<SshConnectionProfile> Load();

    /// <summary>
    /// Saves/rewrite and create the provided SSH connection profiles to the JSON file.
    /// </summary>
    /// <param name="profiles">The list of SSH connection profiles to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveAsync(IEnumerable<SshConnectionProfile> profiles);
}
