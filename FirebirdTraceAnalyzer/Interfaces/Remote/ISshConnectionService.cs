using FirebirdTraceAnalyzer.Models;
using Renci.SshNet;

namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
/// Сервис для управления SSH подключениями
/// </summary>
public interface ISshConnectionService : IDisposable
{
    /// <summary>Активно ли соединение</summary>
    bool IsConnected { get; }
    
    /// <summary>Текущие настройки подключения</summary>
    SshConnectionSettings? CurrentSettings { get; }
    
    /// <summary>Подключиться к серверу</summary>
    Task ConnectAsync(SshConnectionSettings settings, CancellationToken cancellationToken = default);
    
    /// <summary>Отключиться от сервера</summary>
    void Disconnect();
    
    /// <summary>Проверить существование файла</summary>
    Task<bool> FileExistsAsync(string remotePath, CancellationToken cancellationToken = default);
    
    /// <summary>Проверить существование директории</summary>
    Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken cancellationToken = default);
    
    /// <summary>Проверить права на чтение</summary>
    Task<bool> CanReadAsync(string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Активный SFTP-клиент (или <c>null</c>, если не подключены). Через абстракцию — чтобы
    /// зависимые сервисы (RemoteFileService) не кастовали к конкретному классу и были тестируемы.
    /// </summary>
    ISftpClient? GetSftpClient();
}