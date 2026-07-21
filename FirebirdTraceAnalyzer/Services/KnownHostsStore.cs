using System.Text;
using FirebirdTraceAnalyzer.Interfaces.Remote;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// TOFU-хранилище ключей SSH-хостов в файле <c>known_hosts</c> (%AppData%/FirebirdTraceAnalyzer).
/// Первый ключ хоста запоминается; при несовпадении отпечатка — отказ (защита от MITM).
/// Формат строки: <c>host\tport\tkeyName\tfingerprintSha256</c>.
/// </summary>
public sealed class KnownHostsStore : IHostKeyStore
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly string _filePath;
    private readonly object _sync = new();

    /// <param name="filePath">
    /// Путь к файлу known_hosts. Если не задан — %AppData%/FirebirdTraceAnalyzer/known_hosts
    /// (шов для тестов).
    /// </param>
    public KnownHostsStore(string? filePath = null)
    {
        _filePath = filePath ?? DefaultPath();
    }

    private static string DefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(appData, "FirebirdTraceAnalyzer", "known_hosts");
    }

    public bool Verify(string host, int port, string keyName, string fingerprintSha256)
    {
        // Пустой отпечаток трактуем как отказ: нечего доверять, лучше не подключаться.
        if (string.IsNullOrEmpty(fingerprintSha256))
        {
            Logger.Warn("Empty host key fingerprint for {Host}:{Port} — refusing", host, port);
            return false;
        }

        var key = MakeKey(host, port, keyName);

        lock (_sync)
        {
            var known = Load();

            if (known.TryGetValue(key, out var stored))
            {
                // Постоянно-временное сравнение здесь не критично (значения не секретны),
                // но Ordinal обязателен: base64 регистрозависим.
                var match = string.Equals(stored, fingerprintSha256, StringComparison.Ordinal);
                if (!match)
                    Logger.Error("Host key MISMATCH for {Host}:{Port} ({KeyName}): stored != presented — possible MITM",
                        host, port, keyName);
                return match;
            }

            // TOFU: первый ключ запоминаем и доверяем.
            known[key] = fingerprintSha256;
            Save(known);
            Logger.Info("Trusted new host key for {Host}:{Port} ({KeyName}) on first use", host, port, keyName);
            return true;
        }
    }

    private static string MakeKey(string host, int port, string keyName) =>
        $"{host}\t{port}\t{keyName}";

    private Dictionary<string, string> Load()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!File.Exists(_filePath))
            return result;

        foreach (var line in File.ReadAllLines(_filePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var parts = line.Split('\t');
            if (parts.Length != 4)
                continue;

            result[MakeKey(parts[0], int.TryParse(parts[1], out var p) ? p : 0, parts[2])] = parts[3];
        }

        return result;
    }

    private void Save(Dictionary<string, string> known)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("# FirebirdTraceAnalyzer known SSH host keys (host\\tport\\tkeyName\\tsha256). Do not edit.");
        foreach (var (compositeKey, fingerprint) in known)
            sb.Append(compositeKey).Append('\t').Append(fingerprint).Append('\n');

        File.WriteAllText(_filePath, sb.ToString());
        RestrictPermissions(_filePath);
    }

    private static void RestrictPermissions(string filePath)
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
}
