using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
/// Сервис для работы с удалёнными файлами
/// </summary>
public interface IRemoteFileService
{
    /// <summary>Получить список файлов из директории</summary>
    Task<IReadOnlyList<RemoteFileInfo>> GetFilesAsync(
        string remoteDirectory,
        CancellationToken cancellationToken = default);
    
    /// <summary>Скачать файл с прогрессом</summary>
    Task<string> DownloadFileAsync(
        RemoteFileInfo fileInfo,
        string localDirectory,
        IProgress<(long BytesTransferred, long TotalBytes)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Удалить файл на сервере</summary>
    Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken = default);
    
    /// <summary>Удалить несколько файлов на сервере</summary>
    Task DeleteFilesAsync(IEnumerable<string> remotePaths, CancellationToken cancellationToken = default);
}