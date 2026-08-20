using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
///     Сервис для работы с удалёнными файлами
/// </summary>
public interface IRemoteFileService
{
    /// <summary>
    ///    Get a list of files in the specified remote directory.
    /// </summary>
    /// <param name="remoteDirectory">The remote directory to list files in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning a list of remote file information.</returns>
    Task<IReadOnlyList<RemoteFileInfo>> GetFilesAsync(string remoteDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///   Download a file from the remote server to the specified local directory.
    /// </summary>
    /// <param name="fileInfo">The information about the file to download.</param>
    /// <param name="localDirectory">The local directory to download the file to.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the path to the downloaded file.</returns>
    Task<string> DownloadFileAsync(RemoteFileInfo fileInfo, string localDirectory,
        IProgress<(long BytesTransferred, long TotalBytes)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///   Delete a file from the remote server.
    /// </summary>
    /// <param name="remotePath">The path to the file to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Delete multiple files from the remote server.
    /// </summary>
    /// <param name="remotePaths">The paths to the files to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteFilesAsync(IEnumerable<string> remotePaths, CancellationToken cancellationToken = default);
}