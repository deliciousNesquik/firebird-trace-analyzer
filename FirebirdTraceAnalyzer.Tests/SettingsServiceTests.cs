using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services;
using Microsoft.Extensions.Options;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// M1: повреждённый settings.json не должен молча теряться (бэкап в .bak перед откатом на дефолты),
/// а запись обязана быть атомарной (temp + move), чтобы краш/частичная запись не усекли живой файл.
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fta_settings_" + Guid.NewGuid().ToString("N"));

    public SettingsServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    private SettingsService New() =>
        new(Options.Create(new AppSettings()), Options.Create(new UiSectionSettings()), _dir);

    [Fact]
    public void CorruptFile_IsBackedUp_NotSilentlyLost()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ this is : not valid json ]");

        var svc = New(); // Load ловит ошибку → бэкап + дефолты

        var bak = path + ".bak";
        Assert.True(File.Exists(bak));
        Assert.Contains("not valid json", File.ReadAllText(bak));

        // После Save основной файл валиден (дефолты), а .bak по-прежнему хранит оригинал — не потерян.
        svc.Save();
        var reloaded = New(); // не должен бросить — файл теперь валиден
        Assert.NotNull(reloaded.App);
        Assert.Contains("not valid json", File.ReadAllText(bak));
    }

    [Fact]
    public void Save_IsAtomic_NoTempFileLeftBehind()
    {
        var svc = New();
        svc.Save();

        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
        Assert.False(File.Exists(Path.Combine(_dir, "settings.json.tmp")));
    }
}
