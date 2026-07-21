using FirebirdTraceAnalyzer.Enums;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// A4: персистентность SSH-профилей вынесена в SshProfileStore. Проверяем round-trip, устойчивость
/// к отсутствию/битому файлу и что секреты не попадают в файл (совместно с [JsonIgnore]).
/// </summary>
public sealed class SshProfileStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "fta_profiles_" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void MissingFile_ReturnsEmpty()
    {
        Assert.Empty(new SshProfileStore(_path).Load());
    }

    [Fact]
    public async Task SaveThenLoad_RoundTrips_WithoutSecrets()
    {
        var store = new SshProfileStore(_path);
        var profile = new SshConnectionProfile
        {
            Name = "prod",
            Settings = new SshConnectionSettings
            {
                Hostname = "db.local", Port = 2222, Username = "trace",
                AuthMethod = AuthenticationMethod.Password, Password = "secret-pw"
            }
        };

        await store.SaveAsync([profile]);

        // Файл не содержит секрета (JsonIgnore на Password).
        Assert.DoesNotContain("secret-pw", await File.ReadAllTextAsync(_path), StringComparison.Ordinal);

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.Equal("prod", loaded[0].Name);
        Assert.Equal("db.local", loaded[0].Settings.Hostname);
        Assert.Equal(2222, loaded[0].Settings.Port);
        Assert.Null(loaded[0].Settings.Password);
    }

    [Fact]
    public void CorruptFile_ReturnsEmpty_NoThrow()
    {
        File.WriteAllText(_path, "{ this is not valid json ]");
        var ex = Record.Exception(() => new SshProfileStore(_path).Load());
        Assert.Null(ex);
        Assert.Empty(new SshProfileStore(_path).Load());
    }
}
