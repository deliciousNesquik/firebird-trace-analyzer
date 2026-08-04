using System.Text;
using FirebirdTraceAnalyzer.Interfaces.Remote;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// TOFU-хранилище ключей SSH-хостов в файле <c>known_hosts</c> (%AppData%/FirebirdTraceAnalyzer).
/// Доверие ключуется по <c>(host, port)</c>: первый ключ хоста запоминается, а любой другой
/// отпечаток ИЛИ алгоритм для уже известного хоста трактуется как отказ (защита от MITM —
/// иначе активный посредник, навязав другой тип ключа, обошёл бы проверку через TOFU-ветку).
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

        var key = MakeKey(host, port);
        // Алгоритм входит в СРАВНИВАЕМОЕ значение (не в ключ доверия): смена типа ключа на
        // известном хосте — это несовпадение и отказ, а не «новый хост».
        var presented = $"{keyName}\t{fingerprintSha256}";

        lock (_sync)
        {
            var known = Load();

            if (known.TryGetValue(key, out var stored))
            {
                // Постоянно-временное сравнение здесь не критично (значения не секретны),
                // но Ordinal обязателен: base64 регистрозависим.
                var match = string.Equals(stored, presented, StringComparison.Ordinal);
                if (!match)
                    Logger.Error("Host key MISMATCH for {Host}:{Port}: stored != presented ({KeyName}) — possible MITM",
                        host, port, keyName);
                return match;
            }

            // TOFU: первый ключ хоста (алгоритм + отпечаток) запоминаем и доверяем.
            known[key] = presented;
            Save(known);
            Logger.Info("Trusted new host key for {Host}:{Port} ({KeyName}) on first use", host, port, keyName);
            return true;
        }
    }

    private static string MakeKey(string host, int port) =>
        $"{host}\t{port}";

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

            // Ключ доверия — (host, port); значение — алгоритм + отпечаток.
            result[MakeKey(parts[0], int.TryParse(parts[1], out var p) ? p : 0)] = $"{parts[2]}\t{parts[3]}";
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
        // compositeKey = host\tport, algoAndFingerprint = keyName\tsha256 → строка из 4 полей.
        foreach (var (compositeKey, algoAndFingerprint) in known)
            sb.Append(compositeKey).Append('\t').Append(algoAndFingerprint).Append('\n');

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
