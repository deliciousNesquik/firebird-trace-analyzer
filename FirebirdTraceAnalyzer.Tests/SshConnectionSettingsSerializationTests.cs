using System.Text.Json;
using FirebirdTraceAnalyzer.Enums;
using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// S2: секреты (пароль и парольная фраза ключа) НЕ должны попадать в сериализованный профиль
/// (ssh_profiles.json). Проверяем оба поля и что несекретные поля сохраняются.
/// </summary>
public sealed class SshConnectionSettingsSerializationTests
{
    [Fact]
    public void Serialize_OmitsPasswordAndPassphrase_KeepsRest()
    {
        var settings = new SshConnectionSettings
        {
            Hostname = "db.example.local",
            Port = 2222,
            Username = "trace",
            AuthMethod = AuthenticationMethod.PrivateKey,
            Password = "sup3r-secret",
            PrivateKeyPath = "/home/u/.ssh/id_ed25519",
            KeyPassphrase = "top-secret-passphrase",
            RemoteDirectory = "/var/log/firebird"
        };

        var json = JsonSerializer.Serialize(settings);

        Assert.DoesNotContain("sup3r-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("top-secret-passphrase", json, StringComparison.Ordinal);
        // Несекретные поля на месте.
        Assert.Contains("db.example.local", json, StringComparison.Ordinal);
        Assert.Contains("id_ed25519", json, StringComparison.Ordinal);

        // Round-trip: секреты пусты, остальное восстановлено.
        var back = JsonSerializer.Deserialize<SshConnectionSettings>(json)!;
        Assert.Null(back.Password);
        Assert.Null(back.KeyPassphrase);
        Assert.Equal("db.example.local", back.Hostname);
        Assert.Equal(2222, back.Port);
    }
}
