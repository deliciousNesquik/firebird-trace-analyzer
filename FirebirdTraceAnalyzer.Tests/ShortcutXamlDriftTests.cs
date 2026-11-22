using System.Text.RegularExpressions;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Services.Shortcuts;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Drift-guard: каждый id действия, встречающийся в MainMenuView.axaml (hk:Shortcut.Action=...),
/// обязан существовать в реестре. Опечатка в XAML иначе не ловится ничем — attached property
/// молча даёт «нет жеста», и пункт меню остаётся без акселератора.
/// </summary>
public sealed class ShortcutXamlDriftTests
{
    private static string FindRepoFile(params string[] relative)
    {
        // Идём вверх от бин-каталога тестов до корня репозитория.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FirebirdTrace.sln")))
            dir = dir.Parent!;

        var path = Path.Combine(dir!.FullName, Path.Combine(relative));
        Assert.True(File.Exists(path), $"Файл не найден: {path}");
        return path;
    }

    [Fact]
    public void MainMenuView_XamlActionIds_AllExistInCatalog()
    {
        var xaml = File.ReadAllText(
            FindRepoFile("FirebirdTraceAnalyzer", "UserControls", "MainMenuView.axaml"));

        var ids = Regex.Matches(xaml, @"hk:Shortcut\.Action=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(ids);

        var registry = new ShortcutRegistry(ShortcutPlatform.Windows);

        foreach (var id in ids)
            Assert.True(registry.FindAction(id) is not null,
                $"hk:Shortcut.Action=\"{id}\" из MainMenuView.axaml отсутствует в ShortcutCatalog");
    }

    [Fact]
    public void Catalog_CoversAllExpectedCoreActions()
    {
        // Страховка от случайного сокращения каталога: 15 статичных действий + Search.Focus.
        var registry = new ShortcutRegistry(ShortcutPlatform.Windows);
        var coreIds = registry.Actions.Where(a => a.Owner == ShortcutCatalog.OwnerCore).Select(a => a.Id).ToList();

        var expected = new[]
        {
            "App.OpenLocalFile", "App.OpenRemoteFile", "App.CancelLoading",
            "App.ReparseAllFiles", "App.ReparseSelectedFiles",
            "App.CloseAllFiles", "App.CloseSelectedFiles",
            "App.ToggleFilesSection", "App.ToggleSearchSection", "App.ToggleEventsSection",
            "App.ToggleStatisticsSection", "App.ToggleLogsSection", "App.ResetLayout",
            "Search.Focus", "App.OpenSettings",
        };

        foreach (var id in expected)
            Assert.Contains(id, coreIds);
    }
}
