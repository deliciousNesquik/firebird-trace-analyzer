using FirebirdTraceAnalyzer.Models;
using Renci.SshNet;

namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
/// Defines an interface for managing SSH connections and performing remote file operations.
/// </summary>
public interface ISshConnectionService : IDisposable
{
    /// <summary>
    /// Returns true if the SSH connection is currently established; otherwise, false.
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Returns the current SSH connection settings if connected; otherwise, null.
    /// </summary>
    SshConnectionSettings? CurrentSettings { get; }
    
    /// <summary>
    /// Connect to the SSH server using the provided settings.
    /// </summary>
    /// <param name="settings">The SSH connection settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConnectAsync(SshConnectionSettings settings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from the SSH server.
    /// </summary>
    void Disconnect();
    
    /// <summary>
    /// Check if a file exists on the remote server.
    /// </summary>
    /// <param name="remotePath">The path to the file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning true if the file exists; otherwise, false.</returns>
    Task<bool> FileExistsAsync(string remotePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a directory exists on the remote server.
    /// </summary>
    /// <param name="remotePath">The path to the directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning true if the directory exists; otherwise, false.</returns>
    Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a file can be read from the remote server.
    /// </summary>
    /// <param name="remotePath">The path to the file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning true if the file can be read; otherwise, false.</returns>
    Task<bool> CanReadAsync(string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the SFTP client for the current SSH connection.
    /// </summary>
    /// <returns>The SFTP client, or <c>null</c> if not connected.</returns>
    ISftpClient? GetSftpClient();
}