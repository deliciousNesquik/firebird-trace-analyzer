using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FirebirdTraceAnalyzer.Interfaces.Remote;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Сервис для безопасного хранения учётных данных.
/// Бэкенд выбирается один раз под текущую ОС/окружение (<see cref="StorageBackend"/>):
/// - Windows: DPAPI (<see cref="ProtectedData"/>) + файл в %APPDATA%.
/// - macOS: системный Keychain через утилиту <c>security</c>.
/// - Linux: Secret Service (gnome-keyring / KWallet) через утилиту <c>secret-tool</c>.
/// - Фолбэк (нет keyring/secret-tool): файл с правами 0600 и слабой обфускацией — НЕбезопасно.
/// </summary>
public class CredentialStorageService : ICredentialStorageService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string KeychainService = "FirebirdTraceAnalyzer";

    /// <summary>Выбранный бэкенд хранения секретов.</summary>
    private enum StorageBackend
    {
        /// <summary>Windows DPAPI + файл в %APPDATA%.</summary>
        WindowsDpapi,

        /// <summary>macOS Keychain через утилиту <c>security</c>.</summary>
        MacKeychain,

        /// <summary>Linux Secret Service (gnome-keyring / KWallet) через утилиту <c>secret-tool</c>.</summary>
        LinuxSecretService,

        /// <summary>Фолбэк: файл 0600 со слабой обфускацией (нет надёжного шифрования).</summary>
        EncryptedFile
    }

    private readonly StorageBackend _backend;
    private readonly string _storageDirectory;

    public CredentialStorageService()
    {
        _backend = DetectBackend();

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // На некоторых платформах ApplicationData может быть пустым — откатываемся на профиль пользователя.
        if (string.IsNullOrEmpty(appDataPath))
            appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _storageDirectory = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "Credentials");

        // Директория нужна только файловым бэкендам (Windows DPAPI и фолбэк).
        if (_backend is StorageBackend.WindowsDpapi or StorageBackend.EncryptedFile
            && !Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
            Logger.Info("Created credentials storage directory: {Path}", _storageDirectory);
        }

        Logger.Info("Credential storage backend: {Backend}", _backend);

        if (_backend == StorageBackend.EncryptedFile)
            Logger.Warn("Secure credential store unavailable; passwords will be stored with file " +
                        "permissions only (no strong encryption). Install libsecret (secret-tool) and run " +
                        "a keyring daemon to enable the Secret Service backend.");
    }

    /// <summary>Определяет бэкенд под текущую ОС. Secret Service доступен только при наличии <c>secret-tool</c>.</summary>
    private static StorageBackend DetectBackend()
    {
        if (OperatingSystem.IsWindows())
            return StorageBackend.WindowsDpapi;

        if (OperatingSystem.IsMacOS())
            return StorageBackend.MacKeychain;

        if (OperatingSystem.IsLinux() && IsCommandAvailable("secret-tool"))
            return StorageBackend.LinuxSecretService;

        return StorageBackend.EncryptedFile;
    }

    public Task SavePasswordAsync(string server, string username, string password)
    {
        return Task.Run(() =>
        {
            try
            {
                var account = CreateKey(server, username);

                switch (_backend)
                {
                    case StorageBackend.MacKeychain:
                        SaveMac(account, password);
                        break;
                    case StorageBackend.LinuxSecretService:
                        SaveSecretService(account, password);
                        break;
                    default:
                        SaveFile(account, password);
                        break;
                }

                Logger.Info("Password saved for {Username}@{Server}", username, server);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving password");
                throw;
            }
        });
    }

    public Task<string?> GetPasswordAsync(string server, string username)
    {
        return Task.Run<string?>(() =>
        {
            try
            {
                var account = CreateKey(server, username);

                return _backend switch
                {
                    StorageBackend.MacKeychain => GetMac(account),
                    StorageBackend.LinuxSecretService => GetSecretService(account),
                    _ => GetFile(account)
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving password");
                return null;
            }
        });
    }

    public Task DeletePasswordAsync(string server, string username)
    {
        return Task.Run(() =>
        {
            try
            {
                var account = CreateKey(server, username);

                switch (_backend)
                {
                    case StorageBackend.MacKeychain:
                        DeleteMac(account);
                        break;
                    case StorageBackend.LinuxSecretService:
                        DeleteSecretService(account);
                        break;
                    default:
                        DeleteFile(account);
                        break;
                }

                Logger.Info("Password deleted for {Username}@{Server}", username, server);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting password");
                throw;
            }
        });
    }

    public Task<bool> HasPasswordAsync(string server, string username)
    {
        return Task.Run(() =>
        {
            var account = CreateKey(server, username);

            return _backend switch
            {
                StorageBackend.MacKeychain => HasMac(account),
                StorageBackend.LinuxSecretService => HasSecretService(account),
                _ => File.Exists(GetCredentialFilePath(account))
            };
        });
    }

    #region macOS Keychain (утилита security)

    private static void SaveMac(string account, string password)
    {
        // -U: обновить, если запись уже существует. Пароль не логируем.
        var (exitCode, _, stderr) = RunTool("security", null,
            "add-generic-password",
            "-U",
            "-s", KeychainService,
            "-a", account,
            "-w", password);

        if (exitCode != 0)
            throw new InvalidOperationException($"Keychain save failed (exit {exitCode}): {stderr}");
    }

    private static string? GetMac(string account)
    {
        var (exitCode, stdout, _) = RunTool("security", null,
            "find-generic-password",
            "-s", KeychainService,
            "-a", account,
            "-w");

        if (exitCode != 0)
            return null;

        // -w печатает только пароль; убираем завершающий перевод строки, добавленный утилитой.
        return stdout.TrimEnd('\r', '\n');
    }

    private static void DeleteMac(string account)
    {
        var (exitCode, _, stderr) = RunTool("security", null,
            "delete-generic-password",
            "-s", KeychainService,
            "-a", account);

        // exit 44 = item not found — это не ошибка для удаления.
        if (exitCode != 0 && exitCode != 44)
            Logger.Warn("Keychain delete returned exit {Code}: {Err}", exitCode, stderr);
    }

    private static bool HasMac(string account)
    {
        var (exitCode, _, _) = RunTool("security", null,
            "find-generic-password",
            "-s", KeychainService,
            "-a", account);

        return exitCode == 0;
    }

    #endregion

    #region Linux Secret Service (утилита secret-tool)

    // Секрет передаётся через stdin (pipe), а НЕ через argv — не виден в списке процессов (ps/argv).
    private static void SaveSecretService(string account, string password)
    {
        var (exitCode, _, stderr) = RunTool("secret-tool", password,
            "store",
            "--label=" + KeychainService,
            "service", KeychainService,
            "account", account);

        if (exitCode != 0)
            throw new InvalidOperationException($"Secret Service save failed (exit {exitCode}): {stderr}");
    }

    private static string? GetSecretService(string account)
    {
        var (exitCode, stdout, _) = RunTool("secret-tool", null,
            "lookup",
            "service", KeychainService,
            "account", account);

        // secret-tool lookup печатает секрет БЕЗ завершающего перевода строки, поэтому не триммим.
        return exitCode == 0 ? stdout : null;
    }

    private static void DeleteSecretService(string account)
    {
        var (exitCode, _, stderr) = RunTool("secret-tool", null,
            "clear",
            "service", KeychainService,
            "account", account);

        if (exitCode != 0)
            Logger.Warn("secret-tool clear returned exit {Code}: {Err}", exitCode, stderr);
    }

    private static bool HasSecretService(string account)
    {
        var (exitCode, _, _) = RunTool("secret-tool", null,
            "lookup",
            "service", KeychainService,
            "account", account);

        return exitCode == 0;
    }

    #endregion

    #region Файловый бэкенд (Windows DPAPI / фолбэк)

    private void SaveFile(string account, string password)
    {
        var filePath = GetCredentialFilePath(account);
        File.WriteAllText(filePath, EncryptPassword(password));
        RestrictFilePermissions(filePath);
    }

    private string? GetFile(string account)
    {
        var filePath = GetCredentialFilePath(account);
        if (!File.Exists(filePath))
            return null;

        return DecryptPassword(File.ReadAllText(filePath));
    }

    private void DeleteFile(string account)
    {
        var filePath = GetCredentialFilePath(account);
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    #endregion

    private static string CreateKey(string server, string username)
    {
        var combined = $"{server}:{username}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash);
    }

    private string GetCredentialFilePath(string key)
    {
        return Path.Combine(_storageDirectory, $"{key}.cred");
    }

    /// <summary>Есть ли исполняемый файл <paramref name="command"/> в каталогах PATH.</summary>
    private static bool IsCommandAvailable(string command)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
            return false;

        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            try
            {
                if (File.Exists(Path.Combine(dir, command)))
                    return true;
            }
            catch
            {
                // Некорректная запись в PATH — пропускаем.
            }
        }

        return false;
    }

    /// <summary>
    /// Запускает внешнюю утилиту хранилища секретов и возвращает результат.
    /// Если <paramref name="stdinSecret"/> задан — секрет уходит через stdin (pipe), не через argv.
    /// </summary>
    private static (int ExitCode, string StdOut, string StdErr) RunTool(
        string fileName, string? stdinSecret, params string[] args)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdinSecret is not null,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException($"Failed to start '{fileName}' process");

        if (stdinSecret is not null)
        {
            // Пишем ровно секрет без завершающего перевода строки и закрываем поток (EOF).
            process.StandardInput.Write(stdinSecret);
            process.StandardInput.Close();
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }

    /// <summary>Ограничивает доступ к файлу учётных данных владельцем (0600) на Unix.</summary>
    private static void RestrictFilePermissions(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to restrict permissions on {Path}", filePath);
        }
    }

    private static string EncryptPassword(string password)
    {
        if (OperatingSystem.IsWindows())
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var encryptedBytes = ProtectedData.Protect(
                passwordBytes,
                null,
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encryptedBytes);
        }

        // Фолбэк без keyring: файл защищён правами 0600, но содержимое надёжно НЕ шифруется.
        Logger.Warn("Storing credential with file permissions only (no strong encryption) on this OS");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
    }

    private static string DecryptPassword(string encryptedPassword)
    {
        if (OperatingSystem.IsWindows())
        {
            var encryptedBytes = Convert.FromBase64String(encryptedPassword);
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedPassword));
    }
}
