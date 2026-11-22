using System.Collections.ObjectModel;
using Avalonia.Input;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.Shortcuts;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Реестр: дефолты каталога, оверрайды (победа/отключение/мусор), конфлюкт-детекция при
/// загрузке, динамические действия отчётов и осиротевшие оверрайды. Платформа инжектится —
/// тесты детерминированы на любом CI.
/// </summary>
public sealed class ShortcutRegistryTests
{
    private static ShortcutRegistry NewMac() => new(ShortcutPlatform.MacOS);
    private static ShortcutRegistry NewWin() => new(ShortcutPlatform.Windows);

    [Fact]
    public void Resolve_Default_CatalogGesturesResolvePerPlatform()
    {
        var mac = NewMac().Resolve("App.OpenLocalFile");
        var win = NewWin().Resolve("App.OpenLocalFile");

        Assert.NotNull(mac);
        Assert.Equal(Key.O, mac.Key);
        Assert.Equal(KeyModifiers.Meta, mac.KeyModifiers);

        Assert.NotNull(win);
        Assert.Equal(KeyModifiers.Control, win.KeyModifiers);
    }

    [Fact]
    public void Resolve_SearchFocus_IsRegisteredWithDefault()
    {
        // Search.Focus — не пункт меню, но обязано быть в реестре для Ctrl+F через Matches.
        var action = NewWin().FindAction("Search.Focus");

        Assert.NotNull(action);
        Assert.Equal("Primary+F", action.DefaultGesture);
    }

    [Fact]
    public void Resolve_UnknownAction_ReturnsNull()
    {
        Assert.Null(NewWin().Resolve("No.Such.Action"));
    }

    [Fact]
    public void Resolve_OverrideWins()
    {
        var registry = NewWin();
        Assert.True(registry.SetOverride("App.OpenLocalFile", "Ctrl+Shift+F9"));

        var gesture = registry.Resolve("App.OpenLocalFile");

        Assert.NotNull(gesture);
        Assert.Equal(Key.F9, gesture.Key);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, gesture.KeyModifiers);
    }

    [Fact]
    public void Resolve_DisabledOverride_ReturnsNull()
    {
        var registry = NewWin();
        Assert.True(registry.SetOverride("App.ToggleLogsSection", null));

        Assert.Null(registry.Resolve("App.ToggleLogsSection"));
        Assert.Null(registry.Overrides["App.ToggleLogsSection"]);
    }

    [Fact]
    public void Resolve_InvalidOverride_FallsBackToDefault()
    {
        var registry = NewWin();
        registry.LoadOverrides(new Dictionary<string, string?> { ["App.OpenLocalFile"] = "Ctrl+NotAKey" });

        Assert.Equal("Primary+O", registry.ResolveSpec("App.OpenLocalFile")!.ToString());
        Assert.Contains(registry.LoadWarnings, w => w.Contains("App.OpenLocalFile"));
    }

    [Fact]
    public void ClearOverride_ReturnsActionToDefault()
    {
        var registry = NewWin();
        registry.SetOverride("App.OpenLocalFile", "Ctrl+K");
        registry.ClearOverride("App.OpenLocalFile");

        Assert.DoesNotContain("App.OpenLocalFile", registry.Overrides.Keys);
        Assert.Equal("Primary+O", registry.ResolveSpec("App.OpenLocalFile")!.ToString());
    }

    [Fact]
    public void LoadOverrides_SameGestureTwice_LoserDroppedWithWarning()
    {
        var registry = NewWin();
        registry.LoadOverrides(new Dictionary<string, string?>
        {
            ["App.OpenLocalFile"] = "Primary+K",
            ["App.OpenRemoteFile"] = "Cmd+K", // физический cmd на записи — тот же Primary+K
        });

        Assert.Equal("Primary+K", registry.ResolveSpec("App.OpenLocalFile")!.ToString());
        // Проигравший вообще не получает оверрайд → дефолт.
        Assert.Equal("Primary+Shift+O", registry.ResolveSpec("App.OpenRemoteFile")!.ToString());
        Assert.Contains(registry.LoadWarnings, w => w.Contains("App.OpenRemoteFile") && w.Contains("dropped"));
    }

    [Fact]
    public void LoadOverrides_PhysicalCtrlVersusPrimary_AreDistinctCanonicals_NoDrop()
    {
        // По дизайну Ctrl — осознанный физический выбор и с Primary не схлопывается
        // (на macOS это разные жесты: Ctrl+K и ⌘K). Физическую коллизию на конкретной
        // платформе ловит FindConflicting, а не загрузчик файла.
        var registry = NewWin();
        registry.LoadOverrides(new Dictionary<string, string?>
        {
            ["App.OpenLocalFile"] = "Ctrl+K",
            ["App.OpenRemoteFile"] = "Cmd+K",
        });

        Assert.Equal("Ctrl+K", registry.ResolveSpec("App.OpenLocalFile")!.ToString());
        Assert.Equal("Primary+K", registry.ResolveSpec("App.OpenRemoteFile")!.ToString());
        Assert.DoesNotContain(registry.LoadWarnings, w => w.Contains("dropped"));
    }

    [Fact]
    public void LoadOverrides_OverrideLandsOnOtherActionsDefault_WarnsAboutPhysicalCollision()
    {
        // Самая вероятная ошибка ручной правки: перенести биндинг на жест, занятый ЧУЖИМ
        // дефолтом. Каноничные строки разные, физический — один; оверрайды не мутируем,
        // но предупреждение обязано появиться (иначе побеждал бы молча «первый в дереве»).
        var registry = NewWin();
        registry.LoadOverrides(new Dictionary<string, string?>
        {
            ["App.ToggleFilesSection"] = "Primary+O", // дефолт App.OpenLocalFile
        });

        Assert.Equal("Primary+O", registry.ResolveSpec("App.ToggleFilesSection")!.ToString());
        Assert.Contains(registry.LoadWarnings,
            w => w.Contains("Physical gesture") && w.Contains("App.OpenLocalFile") && w.Contains("App.ToggleFilesSection"));
    }

    [Fact]
    public void LoadOverrides_UnknownId_RetainedButNotResolved()
    {
        var registry = NewWin();
        registry.LoadOverrides(new Dictionary<string, string?> { ["Report.Run:dead-beef"] = "Ctrl+F5" });

        Assert.Null(registry.Resolve("Report.Run:dead-beef"));   // не разрешается
        Assert.Equal("Ctrl+F5", registry.Overrides["Report.Run:dead-beef"]); // но переживает сохранение
        Assert.Contains(registry.LoadWarnings, w => w.Contains("retained"));
    }

    [Fact]
    public void SetOverride_LockedAction_Rejected()
    {
        var registry = NewWin();

        Assert.False(registry.SetOverride("App.OpenSettings", "Ctrl+P"));
        Assert.DoesNotContain("App.OpenSettings", registry.Overrides.Keys);
    }

    [Fact]
    public void ResetAll_ClearsEverything()
    {
        var registry = NewWin();
        registry.SetOverride("App.OpenLocalFile", "Ctrl+K");
        registry.SetOverride("App.OpenRemoteFile", null);
        registry.ResetAll();

        Assert.Empty(registry.Overrides);
        Assert.Equal("Primary+O", registry.ResolveSpec("App.OpenLocalFile")!.ToString());
    }

    [Fact]
    public void FindConflicting_ReturnsOtherActionWithSameResolvedGesture()
    {
        var registry = NewWin();

        var conflict = registry.FindConflicting("Ctrl+Shift+O", "App.OpenLocalFile");

        Assert.NotNull(conflict);
        Assert.Equal("App.OpenRemoteFile", conflict.Id);
    }

    [Fact]
    public void Changed_RaisedOnOverrideAndReset()
    {
        var registry = NewWin();
        var count = 0;
        registry.Changed += (_, _) => count++;

        registry.SetOverride("App.OpenLocalFile", "Ctrl+K");
        registry.ClearOverride("App.OpenLocalFile");
        registry.ResetAll();

        Assert.Equal(2, count); // ResetAll при пустых оверрайдах не уведомляет
    }

    // --- Динамические действия отчётов ---------------------------------------------------------

    [Fact]
    public void ReportSource_ActionsAppear_WithUserNameAsTitle()
    {
        var builtIn = new ObservableCollection<ReportTemplate>
        {
            new() { Id = "tpl-1", Name = "Slow queries" },
        };
        var custom = new ObservableCollection<ReportTemplate>
        {
            new() { Id = "tpl-2", Name = "Мой отчёт" },
        };
        var registry = new ShortcutRegistry(ShortcutPlatform.Windows);
        registry.AddSource(new ReportShortcutSource(builtIn, custom));

        var first = registry.FindAction("Report.Run:tpl-1");
        var second = registry.FindAction("Report.Run:tpl-2");

        Assert.NotNull(first);
        Assert.Null(first.DefaultGesture);          // отчёты стартуют безбайндными
        Assert.Equal("Slow queries", first.Title);  // пользовательский текст, не ключ локализации
        Assert.Null(registry.Resolve("Report.Run:tpl-1"));

        Assert.NotNull(second);
        Assert.Equal("Мой отчёт", second.Title);
    }

    [Fact]
    public void ReportSource_CollectionChange_RaisesRegistryChanged()
    {
        var builtIn = new ObservableCollection<ReportTemplate>();
        var registry = new ShortcutRegistry(ShortcutPlatform.Windows);
        registry.AddSource(new ReportShortcutSource(builtIn, new ObservableCollection<ReportTemplate>()));

        var raised = false;
        registry.Changed += (_, _) => raised = true;

        builtIn.Add(new ReportTemplate { Id = "tpl-1", Name = "X" });

        Assert.True(raised);
        Assert.NotNull(registry.FindAction("Report.Run:tpl-1"));
    }

    [Fact]
    public void ReportSource_RemovedTemplate_OrphanOverrideKeptAndReappliedOnReturn()
    {
        var builtIn = new ObservableCollection<ReportTemplate>
        {
            new() { Id = "tpl-1", Name = "Slow queries" },
        };
        var registry = new ShortcutRegistry(ShortcutPlatform.Windows);
        registry.AddSource(new ReportShortcutSource(builtIn, new ObservableCollection<ReportTemplate>()));

        Assert.True(registry.SetOverride("Report.Run:tpl-1", "Ctrl+F5"));

        // Пользователь удалил отчёт: оверрайд сохраняется (файл его не теряет)...
        builtIn.Clear();
        Assert.Null(registry.Resolve("Report.Run:tpl-1"));
        Assert.True(registry.Overrides.ContainsKey("Report.Run:tpl-1"));

        // ...и отчёт реимпортировали с тем же GUID — биндинг возвращается.
        builtIn.Add(new ReportTemplate { Id = "tpl-1", Name = "Slow queries (v2)" });
        Assert.Equal(Key.F5, registry.Resolve("Report.Run:tpl-1")!.Key);
    }
}
