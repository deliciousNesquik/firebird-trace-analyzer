using FirebirdTraceAnalyzer.Services.Shortcuts;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Round-trip shortcuts.json: отсутствие файла — валидное «всё по дефолту», порченый файл
/// бэкапится в .bak и не рушит старт, несовместимая версия не применяется, отключения (null)
/// и неизвестные id переживают сохранение. Философия совпадает с SettingsServiceTests.
/// </summary>
public sealed class ShortcutFileServiceTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "fta_shortcuts_" + Guid.NewGuid().ToString("N"));

    public ShortcutFileServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    private ShortcutFileService New() => new(_dir);

    [Fact]
    public void Load_MissingFile_EmptyWithoutWarnings()
    {
        var data = New().Load();

        Assert.Empty(data.Overrides);
        Assert.Empty(data.Warnings);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsOverridesIncludingNullsAndUnknownIds()
    {
        var service = New();
        var overrides = new Dictionary<string, string?>
        {
            ["App.OpenLocalFile"] = "Ctrl+K",
            ["App.ToggleLogsSection"] = null,                      // осознанное отключение
            ["Report.Run:dead-beef"] = "Ctrl+F5",                  // неизвестный/осиротевший id
        };

        service.Save(overrides);
        var data = service.Load();

        Assert.Empty(data.Warnings);
        Assert.Equal("Ctrl+K", data.Overrides["App.OpenLocalFile"]);
        Assert.Null(data.Overrides["App.ToggleLogsSection"]);
        Assert.Equal("Ctrl+F5", data.Overrides["Report.Run:dead-beef"]);
    }

    [Fact]
    public void Load_CorruptFile_BackedUpAndEmpty()
    {
        var service = New();
        var path = service.FilePath;
        File.WriteAllText(path, "{ this is : not valid json ]");

        var data = service.Load();

        Assert.Empty(data.Overrides);
        Assert.NotEmpty(data.Warnings);
        Assert.True(File.Exists(path + ".bak"), "порченый файл должен пережить первый Save()");
    }

    [Fact]
    public void Load_UnsupportedVersion_BackedUpAndNotApplied()
    {
        var service = New();
        File.WriteAllText(service.FilePath, """{"version":99,"shortcuts":{"App.OpenLocalFile":"Ctrl+K"}}""");

        var data = service.Load();

        Assert.Empty(data.Overrides);
        Assert.Contains(data.Warnings, w => w.Contains("version"));
        Assert.True(File.Exists(service.FilePath + ".bak"));
    }

    [Fact]
    public void Save_CreatesFileWithVersionAndIndentedJson()
    {
        var service = New();
        service.Save(new Dictionary<string, string?> { ["App.OpenLocalFile"] = "Ctrl+K" });

        var json = File.ReadAllText(service.FilePath);

        Assert.Contains("\"version\": 1", json);   // WriteIndented — файл для ручного редактирования
        Assert.Contains("App.OpenLocalFile", json);
    }

    [Fact]
    public void ResetFile_DeletesFile_MissingFileIsFine()
    {
        var service = New();
        service.Save(new Dictionary<string, string?> { ["App.OpenLocalFile"] = "Ctrl+K" });
        service.ResetFile();

        Assert.False(File.Exists(service.FilePath));
        service.ResetFile(); // повторный сброс при отсутствии файла не падает
        Assert.Empty(service.Load().Overrides);
    }

    [Fact]
    public void Load_NumberValueForGesture_TreatedAsCorrupt_BackupAndEmpty()
    {
        var service = New();
        // Число вместо строки жеста — типоневерный JSON для Dictionary<string,string?>.
        File.WriteAllText(service.FilePath, """{"version":1,"shortcuts":{"App.OpenLocalFile":42}}""");

        var data = service.Load();

        Assert.Empty(data.Overrides);
        Assert.NotEmpty(data.Warnings);
        Assert.True(File.Exists(service.FilePath + ".bak"));
    }
}
