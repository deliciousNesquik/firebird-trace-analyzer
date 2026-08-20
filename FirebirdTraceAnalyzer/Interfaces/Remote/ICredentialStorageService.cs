namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
/// Interface for a service that handles credential storage, including saving,
/// retrieving, and deleting passwords for given servers and usernames.
/// </summary>
public interface ICredentialStorageService
{
    /// <summary>
    /// Save password for a given server and username
    /// </summary>
    /// <param name="server">The server for which to save the password.</param>
    /// <param name="username">The username for which to save the password.</param>
    /// <param name="password">The password to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SavePasswordAsync(string server, string username, string password);
    
    /// <summary>
    /// Gets the password for a given server and username.
    /// </summary>
    /// <param name="server">The server for which to get the password.</param>
    /// <param name="username">The username for which to get the password.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<string?> GetPasswordAsync(string server, string username);
    
    /// <summary>
    /// Deletes the password for a given server and username.
    /// </summary>
    /// <param name="server">The server for which to delete the password.</param>
    /// <param name="username">The username for which to delete the password.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeletePasswordAsync(string server, string username);
    
    /// <summary>
    /// Checks if a password exists for a given server and username.
    /// </summary>
    /// <param name="server">The server for which to check the password.</param>
    /// <param name="username">The username for which to check the password.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<bool> HasPasswordAsync(string server, string username);
}