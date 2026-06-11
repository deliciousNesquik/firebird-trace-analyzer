using FirebirdTraceAnalyzer.Enums;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;
using NLog;
using Renci.SshNet;
using Renci.SshNet.Common;
using AuthenticationMethod = FirebirdTraceAnalyzer.Enums.AuthenticationMethod;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Реализация SSH подключения с использованием SSH.NET
/// </summary>
public class SshConnectionService : ISshConnectionService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly object _syncLock = new();
    private SshClient? _sshClient;
    private SftpClient? _sftpClient;
    private PrivateKeyFile? _privateKeyFile;
    private bool _disposed;

    public bool IsConnected => _sshClient?.IsConnected == true && _sftpClient?.IsConnected == true;
    public SshConnectionSettings? CurrentSettings { get; private set; }

    /// <summary>Получить SFTP клиента (для использования в RemoteFileService)</summary>
    internal SftpClient? GetSftpClient() => _sftpClient;

    public async Task ConnectAsync(SshConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.IsValid(out var errorMessage))
            throw new ArgumentException($"Invalid settings: {errorMessage}");

        // Отключаемся от предыдущего соединения
        Disconnect();

        Logger.Info("Connecting to {Hostname}:{Port} as {Username}", 
            settings.Hostname, settings.Port, settings.Username);

        try
        {
            // Создаём ConnectionInfo в зависимости от метода аутентификации
            ConnectionInfo connectionInfo = settings.AuthMethod switch
            {
                AuthenticationMethod.Password => CreatePasswordConnection(settings),
                AuthenticationMethod.PrivateKey => CreatePrivateKeyConnection(settings),
                _ => throw new NotSupportedException($"Authentication method not supported: {settings.AuthMethod}")
            };

            connectionInfo.Timeout = TimeSpan.FromSeconds(settings.ConnectionTimeout);

            // Создаём SSH клиента
            _sshClient = new SshClient(connectionInfo);
            
            // Подключаемся (синхронно, т.к. SSH.NET не имеет async версии Connect)
            await Task.Run(() => _sshClient.Connect(), cancellationToken);

            if (!_sshClient.IsConnected)
                throw new SshConnectionException("Failed to establish SSH connection");

            Logger.Info("SSH connection established");

            // Создаём SFTP клиента
            _sftpClient = new SftpClient(connectionInfo);
            
            await Task.Run(() => _sftpClient.Connect(), cancellationToken);

            if (!_sftpClient.IsConnected)
                throw new SshConnectionException("Failed to establish SFTP connection");

            Logger.Info("SFTP connection established");

            CurrentSettings = settings;
        }
        catch (SshAuthenticationException ex)
        {
            Logger.Error(ex, "Authentication failed");
            Disconnect();
            throw new InvalidOperationException("Authentication failed. Check credentials.", ex);
        }
        catch (SshConnectionException ex)
        {
            Logger.Error(ex, "Connection failed");
            Disconnect();
            throw new InvalidOperationException($"Connection failed: {ex.Message}", ex);
        }
        catch (OperationCanceledException)
        {
            Logger.Info("Connection cancelled");
            Disconnect();
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error during connection");
            Disconnect();
            throw new InvalidOperationException($"Connection error: {ex.Message}", ex);
        }
    }

    public void Disconnect()
    {
        // Сериализуем: метод зовётся из нескольких мест (catch в ConnectAsync, VM, finally в UI).
        // lock делает его идемпотентным и защищает от двойного dispose/гонок.
        lock (_syncLock)
        {
            if (_sftpClient is null && _sshClient is null && _privateKeyFile is null)
                return;

            try
            {
                _sftpClient?.Disconnect();
                _sftpClient?.Dispose();
                _sftpClient = null;

                _sshClient?.Disconnect();
                _sshClient?.Dispose();
                _sshClient = null;

                // Освобождаем ключевой материал, чтобы он не висел в памяти до GC
                _privateKeyFile?.Dispose();
                _privateKeyFile = null;

                CurrentSettings = null;

                Logger.Info("Disconnected from server");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error during disconnect");
            }
        }
    }

    public Task<bool> FileExistsAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        
        return Task.Run(() =>
        {
            try
            {
                return _sftpClient!.Exists(remotePath) && !_sftpClient.Get(remotePath).IsDirectory;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error checking file existence: {Path}", remotePath);
                return false;
            }
        }, cancellationToken);
    }

    public Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        
        return Task.Run(() =>
        {
            try
            {
                return _sftpClient!.Exists(remotePath) && _sftpClient.Get(remotePath).IsDirectory;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error checking directory existence: {Path}", remotePath);
                return false;
            }
        }, cancellationToken);
    }

    public Task<bool> CanReadAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        
        return Task.Run(() =>
        {
            try
            {
                var file = _sftpClient!.Get(remotePath);
                // Проверяем права на чтение (owner read = 0400)
                return file.Attributes.OwnerCanRead;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error checking read permissions: {Path}", remotePath);
                return false;
            }
        }, cancellationToken);
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to server");
    }

    private static ConnectionInfo CreatePasswordConnection(SshConnectionSettings settings)
    {
        return new ConnectionInfo(
            settings.Hostname,
            settings.Port,
            settings.Username,
            new PasswordAuthenticationMethod(settings.Username, settings.Password!));
    }

    private ConnectionInfo CreatePrivateKeyConnection(SshConnectionSettings settings)
    {
        if (!File.Exists(settings.PrivateKeyPath))
            throw new FileNotFoundException($"Private key not found: {settings.PrivateKeyPath}");

        var keyFile = string.IsNullOrWhiteSpace(settings.KeyPassphrase)
            ? new PrivateKeyFile(settings.PrivateKeyPath)
            : new PrivateKeyFile(settings.PrivateKeyPath, settings.KeyPassphrase);

        // Сохраняем ссылку: ключ нужен на время аутентификации, освобождаем в Disconnect().
        _privateKeyFile = keyFile;

        return new ConnectionInfo(
            settings.Hostname,
            settings.Port,
            settings.Username,
            new PrivateKeyAuthenticationMethod(settings.Username, keyFile));
    }

    public void Dispose()
    {
        if (_disposed) return;

        Disconnect();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}