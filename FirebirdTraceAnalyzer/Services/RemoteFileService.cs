using System.Diagnostics;
using FirebirdTraceAnalyzer.Interfaces.Remote;
using FirebirdTraceAnalyzer.Models;
using NLog;
using Renci.SshNet;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Сервис для работы с удалёнными файлами через SFTP
/// </summary>
public class RemoteFileService : IRemoteFileService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>Минимальный интервал между отчётами прогресса (мс): SSH.NET зовёт колбэк на каждый
    /// буфер (тысячи раз), без троттлинга это заваливает UI-поток.</summary>
    private const long ProgressThrottleMs = 100;

    private readonly ISshConnectionService _connectionService;

    public RemoteFileService(ISshConnectionService connectionService)
    {
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
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

                // SFTP отдаёт только числовой UID владельца. Пытаемся один раз получить карту
                // uid → имя из /etc/passwd; если не вышло — оставим числовой id.
                var ownerNames = TryLoadOwnerNames(sftpClient);

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
                            // Имя владельца из /etc/passwd, иначе — числовой UID
                            Owner = ResolveOwner(f.Attributes.UserId, ownerNames)
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

    /// <summary>
    ///     Пытается прочитать /etc/passwd с сервера и построить карту uid → имя пользователя.
    ///     Возвращает null, если файл недоступен (не Linux, нет прав и т.п.) — тогда показываем UID.
    /// </summary>
    private static Dictionary<int, string>? TryLoadOwnerNames(ISftpClient sftpClient)
    {
        try
        {
            var passwd = sftpClient.ReadAllText("/etc/passwd");
            var map = new Dictionary<int, string>();

            foreach (var line in passwd.Split('\n'))
            {
                // Формат строки: name:x:uid:gid:gecos:home:shell
                var parts = line.Split(':');
                if (parts.Length >= 3 && int.TryParse(parts[2], out var uid) && parts[0].Length > 0)
                    map[uid] = parts[0];
            }

            return map.Count > 0 ? map : null;
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Could not read /etc/passwd; owner will be shown as numeric UID");
            return null;
        }
    }

    private static string ResolveOwner(int uid, Dictionary<int, string>? ownerNames)
        => ownerNames != null && ownerNames.TryGetValue(uid, out var name)
            ? name
            : uid.ToString();

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
            // Имя приходит с сервера (листинг SFTP) — недоверенные данные. Санитизируем и проверяем,
            // что итоговый путь не вышел за localDirectory (защита от '../' и разделителей пути).
            var localPath = ResolveSafeLocalPath(localDirectory, fileInfo.FileName);
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

                var throttle = Stopwatch.StartNew();
                sftpClient.DownloadFile(fileInfo.FullPath, fileStream, bytesTransferred =>
                {
                    // Троттлинг: не чаще одного отчёта в ProgressThrottleMs.
                    if (throttle.ElapsedMilliseconds < ProgressThrottleMs)
                        return;

                    throttle.Restart();
                    progress?.Report(((long)bytesTransferred, fileInfo.Size));
                });

                // Гарантируем финальный 100%-отчёт (последний колбэк мог быть отсечён троттлингом).
                progress?.Report((fileInfo.Size, fileInfo.Size));

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

    /// <summary>
    /// Строит безопасный локальный путь для скачиваемого файла: берёт только компонент имени
    /// (обрезая любые каталоги/'../') и проверяет, что результат остаётся внутри
    /// <paramref name="localDirectory"/>. Бросает <see cref="InvalidOperationException"/> при попытке
    /// выхода за каталог (path traversal через подконтрольное серверу имя файла).
    /// </summary>
    public static string ResolveSafeLocalPath(string localDirectory, string remoteFileName)
    {
        var safeName = Path.GetFileName(remoteFileName);

        // Пустое имя, "." / ".." и т.п. после GetFileName недопустимы.
        if (string.IsNullOrWhiteSpace(safeName) || safeName is "." or "..")
            throw new InvalidOperationException($"Unsafe remote file name: '{remoteFileName}'");

        var root = Path.GetFullPath(localDirectory);
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        var fullPath = Path.GetFullPath(Path.Combine(root, safeName));

        if (!fullPath.StartsWith(rootWithSep, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unsafe remote file name: '{remoteFileName}'");

        return fullPath;
    }
}