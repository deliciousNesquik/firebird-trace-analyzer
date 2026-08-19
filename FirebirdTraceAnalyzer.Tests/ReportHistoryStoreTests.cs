using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services.Reports;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// T6: файловая история отчётов вынесена в ReportHistoryStore. Проверяем фильтрацию по расширению,
/// определение формата, удаление и создание каталога.
/// </summary>
public sealed class ReportHistoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fta_rh_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ReportHistoryStore NewStore() => new(new FixedReportsDirSettings(_dir));

    [Fact]
    public void ResolveDirectory_CreatesIfMissing()
    {
        Assert.False(Directory.Exists(_dir));
        var dir = NewStore().ResolveDirectory();
        Assert.Equal(_dir, dir);
        Assert.True(Directory.Exists(_dir));
    }

    [Fact]
    public void List_ReturnsOnlyReportFiles_WithFormat()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "a.pdf"), "x");
        File.WriteAllText(Path.Combine(_dir, "b.csv"), "x");
        File.WriteAllText(Path.Combine(_dir, "c.txt"), "x");   // не отчёт
        File.WriteAllText(Path.Combine(_dir, "d.log"), "x");   // не отчёт

        var entries = NewStore().List();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.FileName == "a.pdf" && e.Format == "Pdf");
        Assert.Contains(entries, e => e.FileName == "b.csv" && e.Format == "Csv");
        Assert.DoesNotContain(entries, e => e.FileName is "c.txt" or "d.log");
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "r.xlsx");
        File.WriteAllText(path, "x");

        NewStore().Delete(path);

        Assert.False(File.Exists(path));
    }

    private sealed class FixedReportsDirSettings(string dir) : ISettingsService
    {
        public string GetReportsDirectory() => dir;
        public AppSettings App => throw new NotImplementedException();
        public UiSectionSettings Ui => throw new NotImplementedException();
        public WindowSettings Window => throw new NotImplementedException();
        public string GetRemoteDownloadDirectory() => throw new NotImplementedException();
        public string GetEventStoreDirectory() => throw new NotImplementedException();
        public void Save() => throw new NotImplementedException();
        public UserSettings GetDefaults() => throw new NotImplementedException();
        public Task ExportAsync(string path, UserSettings settings) => throw new NotImplementedException();
        public Task<UserSettings> ReadFromFileAsync(string path) => throw new NotImplementedException();
    }
}
