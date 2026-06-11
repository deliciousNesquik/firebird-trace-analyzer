using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FirebirdTraceAnalyzer.Interfaces;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Сервис для безопасного хранения учётных данных.
/// Платформо-зависимое хранилище:
/// - Windows: DPAPI (<see cref="ProtectedData"/>) + файл в %APPDATA%.
/// - macOS: системный Keychain через утилиту <c>security</c>.
/// - Linux: файл с правами 0600 (НЕбезопасно; TODO: Secret Service / libsecret).
/// </summary>
public class CredentialStorageService : ICredentialStorageService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string KeychainService = "FirebirdTraceAnalyzer";

    private readonly string _storageDirectory;

    public CredentialStorageService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // На некоторых платформах ApplicationData может быть пустым — откатываемся на профиль пользователя.
        if (string.IsNullOrEmpty(appDataPath))
            appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _storageDirectory = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "Credentials");

        // Директория нужна только для файловых бэкендов (Windows/Linux); на macOS секреты идут в Keychain.
        if (!OperatingSystem.IsMacOS() && !Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
            Logger.Info("Created credentials storage directory: {Path}", _storageDirectory);
        }
    }

    public Task SavePasswordAsync(string server, string username, string password)
    {
        return Task.Run(() =>
        {
            try
            {
                var account = CreateKey(server, username);

                if (OperatingSystem.IsMacOS())
                {
                    // -U: обновить, если запись уже существует. Пароль не логируем.
                    var (exitCode, _, stderr) = RunSecurity(
                        "add-generic-password",
                        "-U",
                        "-s", KeychainService,
                        "-a", account,
                        "-w", password);

                    if (exitCode != 0)
                        throw new InvalidOperationException($"Keychain save failed (exit {exitCode}): {stderr}");
                }
                else
                {
                    var filePath = GetCredentialFilePath(account);
                    File.WriteAllText(filePath, EncryptPassword(password));
                    RestrictFilePermissions(filePath);
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

                if (OperatingSystem.IsMacOS())
                {
                    var (exitCode, stdout, _) = RunSecurity(
                        "find-generic-password",
                        "-s", KeychainService,
                        "-a", account,
                        "-w");

                    if (exitCode != 0)
                    {
                        Logger.Debug("No Keychain password for {Username}@{Server}", username, server);
                        return null;
                    }

                    // -w печатает только пароль; убираем завершающий перевод строки.
                    return stdout.TrimEnd('\r', '\n');
                }

                var filePath = GetCredentialFilePath(account);
                if (!File.Exists(filePath))
                {
                    Logger.Debug("No saved password found for {Username}@{Server}", username, server);
                    return null;
                }

                return DecryptPassword(File.ReadAllText(filePath));
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

                if (OperatingSystem.IsMacOS())
                {
                    var (exitCode, _, stderr) = RunSecurity(
                        "delete-generic-password",
                        "-s", KeychainService,
                        "-a", account);

                    // exit 44 = item not found — это не ошибка для удаления.
                    if (exitCode != 0 && exitCode != 44)
                        Logger.Warn("Keychain delete returned exit {Code}: {Err}", exitCode, stderr);
                    else
                        Logger.Info("Password deleted for {Username}@{Server}", username, server);

                    return;
                }

                var filePath = GetCredentialFilePath(account);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Info("Password deleted for {Username}@{Server}", username, server);
                }
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

            if (OperatingSystem.IsMacOS())
            {
                var (exitCode, _, _) = RunSecurity(
                    "find-generic-password",
                    "-s", KeychainService,
                    "-a", account);

                return exitCode == 0;
            }

            return File.Exists(GetCredentialFilePath(account));
        });
    }

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

    /// <summary>Запускает утилиту <c>security</c> (macOS Keychain) и возвращает результат.</summary>
    private static (int ExitCode, string StdOut, string StdErr) RunSecurity(params string[] args)
    {
        var psi = new ProcessStartInfo("security")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("Failed to start 'security' process");

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

        // Linux fallback: файл защищён правами 0600, но содержимое не шифруется надёжно.
        // TODO: интеграция с Secret Service / libsecret.
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
