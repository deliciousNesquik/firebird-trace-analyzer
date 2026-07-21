using System.Text.Json;
using FirebirdTraceAnalyzer.Interfaces.Remote;
using FirebirdTraceAnalyzer.Models;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Файловое хранилище SSH-профилей (%AppData%/FirebirdTraceAnalyzer/ssh_profiles.json).
/// Секреты в профилях не сохраняются (см. [JsonIgnore] в <see cref="SshConnectionSettings"/>).
/// </summary>
public sealed class SshProfileStore : ISshProfileStore
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public string FilePath { get; }

    /// <param name="filePath">Путь к файлу профилей. Если не задан — путь по умолчанию (шов для тестов).</param>
    public SshProfileStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath();
    }

    private static string DefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "FirebirdTraceAnalyzer", "ssh_profiles.json");
    }

    public IReadOnlyList<SshConnectionProfile> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return [];

            var json = File.ReadAllText(FilePath);
            var profiles = JsonSerializer.Deserialize<List<SshConnectionProfile>>(json);
            return profiles ?? [];
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading SSH profiles from {Path}", FilePath);
            return [];
        }
    }

    public async Task SaveAsync(IEnumerable<SshConnectionProfile> profiles)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(profiles.ToList(), WriteOptions);
        await File.WriteAllTextAsync(FilePath, json);
        Logger.Debug("SSH profiles saved to {Path}", FilePath);
    }
}
