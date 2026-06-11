using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;
using NLog;
using Renci.SshNet.Sftp;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Сервис для работы с удалёнными файлами через SFTP
/// </summary>
public class RemoteFileService : IRemoteFileService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly SshConnectionService _connectionService;

    public RemoteFileService(ISshConnectionService connectionService)
    {
        _connectionService = connectionService as SshConnectionService 
            ?? throw new ArgumentException("Must be SshConnectionService", nameof(connectionService));
    }

    public Task<IReadOnlyList<RemoteFileInfo>> GetFilesAsync(
        string remoteDirectory, 
        CancellationToken cancellationToken = default)
    {
        var sftpClient = _connectionService.GetSftpClient();
        
        if (sftpClient == null || !sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP client not connected");

        return Task.Run(() =>
        {
            try
            {
                Logger.Info("Fetching files from directory: {Directory}", remoteDirectory);

                var files = new List<RemoteFileInfo>();

                foreach (var f in sftpClient.ListDirectory(remoteDirectory))
                {
                    try
                    {
                        if (f.IsDirectory || !IsTraceFile(f.Name))
                            continue;

                        files.Add(new RemoteFileInfo
                        {
                            FileName = f.Name,
                            FullPath = f.FullName,
                            Size = f.Length,
                            LastModified = f.LastWriteTime,
                            Permissions = new Permissions(
                                f.Attributes.OwnerCanRead,
                                f.Attributes.OwnerCanWrite,
                                f.Attributes.OwnerCanExecute,
                                f.Attributes.GroupCanRead,
                                f.Attributes.GroupCanWrite,
                                f.Attributes.GroupCanExecute,
                                f.Attributes.OthersCanRead,
                                f.Attributes.OthersCanWrite,
                                f.Attributes.OthersCanExecute
                            ),
                            // SFTP (v3) отдаёт числовой UID владельца, не имя
                            Owner = f.Attributes.UserId.ToString()
                        });
                    }
                    catch (Exception ex)
                    {
                        // Изолируем сбой по одной записи, чтобы не потерять весь листинг
                        Logger.Warn(ex, "Skipping file with unreadable attributes: {Name}", f.Name);
                    }
                }

                var ordered = files
                    .OrderByDescending(f => f.LastModified)
                    .ToList();

                Logger.Info("Found {Count} trace files", ordered.Count);

                return (IReadOnlyList<RemoteFileInfo>)ordered;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error fetching files from {Directory}", remoteDirectory);
                throw new InvalidOperationException($"Failed to fetch files: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    public Task<string> DownloadFileAsync(
        RemoteFileInfo fileInfo,
        string localDirectory,
        IProgress<(long BytesTransferred, long TotalBytes)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sftpClient = _connectionService.GetSftpClient();
        
        if (sftpClient == null || !sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP client not connected");

        return Task.Run(() =>
        {
            var localPath = Path.Combine(localDirectory, fileInfo.FileName);
            FileStream? fileStream = null;

            // Отмену реализуем через закрытие выходного потока: DownloadFile прервётся
            // исключением, которое мы поймаем ниже. НЕ бросаем исключение внутри колбэка
            // прогресса — SSH.NET вызывает его на внутреннем потоке, и throw там роняет процесс.
            using var registration = cancellationToken.Register(() =>
            {
                try { fileStream?.Dispose(); }
                catch { /* ignore */ }
            });

            try
            {
                Logger.Info("Downloading {FileName} to {LocalPath}", fileInfo.FileName, localPath);

                fileStream = File.Create(localPath);

                sftpClient.DownloadFile(fileInfo.FullPath, fileStream, bytesTransferred =>
                {
                    progress?.Report(((long)bytesTransferred, fileInfo.Size));
                });

                Logger.Info("Download completed: {FileName}", fileInfo.FileName);

                return localPath;
            }
            catch (Exception ex)
            {
                // Закрываем поток, затем удаляем частично скачанный файл
                fileStream?.Dispose();
                TryDeletePartialFile(localPath);

                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.Info("Download cancelled: {FileName}", fileInfo.FileName);
                    throw new OperationCanceledException(cancellationToken);
                }

                Logger.Error(ex, "Error downloading file: {FileName}", fileInfo.FileName);
                throw new InvalidOperationException($"Failed to download {fileInfo.FileName}: {ex.Message}", ex);
            }
            finally
            {
                fileStream?.Dispose();
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> DownloadFilesAsync(
        IEnumerable<RemoteFileInfo> files,
        string localDirectory,
        IProgress<(int FileIndex, int TotalFiles, long BytesTransferred, long TotalBytes)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fileList = files.ToList();
        var downloadedPaths = new List<string>();

        for (var i = 0; i < fileList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = fileList[i];
            var fileIndex = i;

            var fileProgress = new Progress<(long BytesTransferred, long TotalBytes)>(p =>
            {
                progress?.Report((fileIndex, fileList.Count, p.BytesTransferred, p.TotalBytes));
            });

            var localPath = await DownloadFileAsync(file, localDirectory, fileProgress, cancellationToken);
            downloadedPaths.Add(localPath);
        }

        return downloadedPaths;
    }

    public Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        var sftpClient = _connectionService.GetSftpClient();
        
        if (sftpClient == null || !sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP client not connected");

        return Task.Run(() =>
        {
            try
            {
                Logger.Info("Deleting remote file: {Path}", remotePath);
                sftpClient.DeleteFile(remotePath);
                Logger.Info("File deleted: {Path}", remotePath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting file: {Path}", remotePath);
                throw new InvalidOperationException($"Failed to delete {remotePath}: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    public async Task DeleteFilesAsync(IEnumerable<string> remotePaths, CancellationToken cancellationToken = default)
    {
        foreach (var path in remotePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DeleteFileAsync(path, cancellationToken);
        }
    }

    private static void TryDeletePartialFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to delete partial file: {Path}", path);
        }
    }

    private static bool IsTraceFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".log" or ".trace" or ".trc" or ".txt";
    }
}