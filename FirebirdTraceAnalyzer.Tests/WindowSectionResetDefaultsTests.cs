using System.Text.Json;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services;
using Microsoft.Extensions.Options;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Регрессия на баг «Window → Reset не сбрасывает секции». Причина была двойная:
///  1. заводской дефолт должен быть «всё включено, кроме Logs» (appsettings.json);
///  2. Reset обязан брать ЗАВОДСКИЕ значения из <see cref="ISettingsService.GetDefaults"/>,
///     а не перечитывать сохранённые (они совпадают с текущим состоянием — сброс был no-op).
/// Эти тесты фиксируют оба контракта, на которые опирается команда сброса.
/// </summary>
public sealed class WindowSectionResetDefaultsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fta_reset_" + Guid.NewGuid().ToString("N"));

    public WindowSectionResetDefaultsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void AppSettingsJson_SectionDefaults_AllOnExceptLogs()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        Assert.True(File.Exists(path), $"appsettings.json not found at {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var sections = doc.RootElement.GetProperty("UI").GetProperty("Sections");

        Assert.True(sections.GetProperty("Files").GetBoolean());
        Assert.True(sections.GetProperty("Search").GetBoolean());
        Assert.True(sections.GetProperty("Events").GetBoolean());
        Assert.True(sections.GetProperty("Statistics").GetBoolean());
        Assert.False(sections.GetProperty("Logs").GetBoolean());
    }

    [Fact]
    public void GetDefaults_ReturnsFactoryUi_ImmuneToLiveMutation()
    {
        var factoryUi = new UiSectionSettings
        {
            Files = true, Search = true, Events = true, Statistics = true, Logs = false,
        };
        var svc = new SettingsService(Options.Create(new AppSettings()), Options.Create(factoryUi), _dir);

        // Пользователь переключает секции (живое состояние = сохранённое) и сохраняет.
        svc.Ui.Logs = true;
        svc.Ui.Files = false;
        svc.Ui.Search = false;
        svc.Save();

        // GetDefaults обязан вернуть ЗАВОДСКИЕ значения, а не изменённые/сохранённые — на этом
        // держится корректный сброс в MainWindowViewModel.GoToFactorySettingsSection.
        var defaults = svc.GetDefaults().Ui;
        Assert.True(defaults.Files);
        Assert.True(defaults.Search);
        Assert.True(defaults.Events);
        Assert.True(defaults.Statistics);
        Assert.False(defaults.Logs);
    }
}
