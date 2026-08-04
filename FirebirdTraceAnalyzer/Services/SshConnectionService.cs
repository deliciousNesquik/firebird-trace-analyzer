using FirebirdTraceAnalyzer.Interfaces.Remote;
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

    /// <summary>Таймаут отдельной SFTP-операции: «зависший» (без данных) трансфер прервётся, а не повиснет навсегда.</summary>
    private static readonly TimeSpan SftpOperationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Размер буфера SFTP (256 КБ): компромисс скорость/память для крупных трейс-файлов.</summary>
    private const uint SftpBufferSize = 256 * 1024;

    private readonly object _syncLock = new();
    private readonly IHostKeyStore _hostKeyStore;
    private SftpClient? _sftpClient;
    private Renci.SshNet.AuthenticationMethod? _authMethod;
    private PrivateKeyFile? _privateKeyFile;
    private bool _disposed;

    public SshConnectionService(IHostKeyStore hostKeyStore)
    {
        _hostKeyStore = hostKeyStore ?? throw new ArgumentNullException(nameof(hostKeyStore));
    }

    public bool IsConnected => _sftpClient?.IsConnected == true;
    public SshConnectionSettings? CurrentSettings { get; private set; }

    /// <summary>Получить SFTP клиента (для использования в RemoteFileService)</summary>
    public ISftpClient? GetSftpClient() => _sftpClient;

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

            // Для листинга/скачивания/удаления достаточно одного SFTP-клиента
            _sftpClient = new SftpClient(connectionInfo)
            {
                // Чтобы зависший трансфер не блокировал процесс навсегда
                OperationTimeout = SftpOperationTimeout,

                // Увеличенный буфер заметно ускоряет скачивание крупных файлов: меньше
                // SFTP round-trip'ов, чем при дефолтных 32 КБ.
                BufferSize = SftpBufferSize
            };

            // Проверка ключа хоста: SSH.NET по умолчанию принимает ЛЮБОЙ ключ сервера.
            // Сверяем отпечаток с known_hosts (TOFU): первый ключ запоминаем, при несовпадении —
            // отказ. Без этой подписки соединение уязвимо к подмене сервера (MITM).
            _sftpClient.HostKeyReceived += (_, e) =>
            {
                e.CanTrust = _hostKeyStore.Verify(settings.Hostname, settings.Port,
                    e.HostKeyName, e.FingerPrintSHA256);
                if (!e.CanTrust)
                    Logger.Error("Host key rejected for {Host}:{Port} — refusing connection (possible MITM)",
                        settings.Hostname, settings.Port);
            };

            // Отменяемое подключение (SSH.NET 2025.x): токен реально прерывает Connect. Task.Run(Connect)
            // отменял лишь ЗАПУСК задачи, а блокирующий Connect висел до ConnectionTimeout (~30с), из-за
            // чего кнопка «Отмена» не срабатывала.
            await _sftpClient.ConnectAsync(cancellationToken);

            if (!_sftpClient.IsConnected)
                throw new SshConnectionException("Failed to establish SFTP connection");

            Logger.Info("SFTP connection established");

            // После аутентификации секреты больше не нужны — не держим их в долгоживущем
            // CurrentSettings (сервис — singleton), чтобы пароль не висел в памяти всю сессию.
            CurrentSettings = settings with { Password = null, KeyPassphrase = null };
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
            if (_sftpClient is null && _authMethod is null && _privateKeyFile is null)
                return;

            try
            {
                _sftpClient?.Disconnect();
                _sftpClient?.Dispose();
                _sftpClient = null;

                // Auth-метод (хранит пароль/ключ) — IDisposable, освобождаем явно, чтобы
                // секрет не висел в памяти до GC.
                _authMethod?.Dispose();
                _authMethod = null;

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

                // Бит владельца (OwnerCanRead) — это права ВЛАДЕЛЬЦА объекта, а не эффективный доступ
                // подключённого пользователя (напр. /var/log/firebird root:0750 → OwnerCanRead=true,
                // но чужой пользователь читать не может). Проверяем реальной операцией.
                if (file.IsDirectory)
                {
                    // Открытие каталога на листинг завершится SftpPermissionDeniedException, если доступа нет.
                    using var e = _sftpClient.ListDirectory(remotePath).GetEnumerator();
                    e.MoveNext();
                }
                else
                {
                    using var stream = _sftpClient.OpenRead(remotePath);
                }

                return true;
            }
            catch (Renci.SshNet.Common.SftpPermissionDeniedException)
            {
                return false;
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

    private ConnectionInfo CreatePasswordConnection(SshConnectionSettings settings)
    {
        var authMethod = new PasswordAuthenticationMethod(settings.Username, settings.Password!);

        // Сохраняем ссылку: auth-метод хранит пароль, освобождаем в Disconnect().
        _authMethod = authMethod;

        return new ConnectionInfo(
            settings.Hostname,
            settings.Port,
            settings.Username,
            authMethod);
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

        var authMethod = new PrivateKeyAuthenticationMethod(settings.Username, keyFile);
        _authMethod = authMethod;

        return new ConnectionInfo(
            settings.Hostname,
            settings.Port,
            settings.Username,
            authMethod);
    }

    public void Dispose()
    {
        if (_disposed) return;

        Disconnect();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}