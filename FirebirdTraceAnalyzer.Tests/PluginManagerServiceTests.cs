using System.Text.Json;
using FirebirdTraceAnalyzer.Services.Plugins;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// L14 (security): отложенные удаления берутся из недоверенного plugins.state.json. Удалять можно
/// ТОЛЬКО пути внутри каталога плагинов — иначе подменённый state.json стёр бы произвольные данные.
/// </summary>
public sealed class PluginManagerServiceTests
{
    [Fact]
    public void ProcessPendingDeletions_RefusesPathOutsidePluginsDir()
    {
        var pluginsDir = Path.Combine(Path.GetTempPath(), "fta_plugins_" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "fta_outside_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pluginsDir);
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "important.txt"), "keep me");
        try
        {
            var stateFile = Path.Combine(pluginsDir, "plugins.state.json");
            // Подменённый state.json просит удалить каталог ВНЕ папки плагинов.
            File.WriteAllText(stateFile,
                $"{{\"PendingDelete\":[{JsonSerializer.Serialize(outside)}],\"Disabled\":[]}}");

            new PluginManagerService(pluginsDir).LoadAllPlugins();

            // Путь вне каталога плагинов НЕ должен быть удалён.
            Assert.True(Directory.Exists(outside));
            Assert.True(File.Exists(Path.Combine(outside, "important.txt")));
        }
        finally
        {
            try { Directory.Delete(pluginsDir, true); } catch { /* ignore */ }
            try { Directory.Delete(outside, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void FullyDisabledDll_IsSkipped_NotLoaded_NoCodeExecuted()
    {
        // Все типы DLL известны и выключены → сборка не грузится (ctor не исполняется). Заглушка Disabled,
        // а НЕ LoadError: если бы её пытались загрузить, битая «сборка» дала бы LoadError.
        var plugins = LoadWithBogusDll(disabled: true);

        var p = Assert.Single(plugins);
        Assert.Equal("X", p.Id);
        Assert.Equal(PluginStatus.Disabled, p.Status);
        Assert.Null(p.Instance);
        // Заглушка показывает НАСТОЯЩИЕ метаданные из снимка, а не голый Id (регрессия F6).
        Assert.Equal("Cool Sorter", p.Name);
        Assert.Equal("1.2.0", p.Version);
    }

    [Fact]
    public void EnabledDll_IsAttemptedToLoad_AndBogusOneFailsWithLoadError()
    {
        // Контраст: не выключено → DLL грузится → битая «сборка» даёт LoadError (значит скип выше — реальный).
        var plugins = LoadWithBogusDll(disabled: false);

        var p = Assert.Single(plugins);
        Assert.Equal(PluginStatus.LoadError, p.Status);
    }

    private static IReadOnlyList<PluginInfo> LoadWithBogusDll(bool disabled)
    {
        var pluginsDir = Path.Combine(Path.GetTempPath(), "fta_plugins_" + Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(pluginsDir, "myplugin");
        Directory.CreateDirectory(sub);
        var dll = Path.Combine(sub, "myplugin.dll");
        File.WriteAllBytes(dll, [0x00, 0x01, 0x02]); // не валидная .NET-сборка

        var stateFile = Path.Combine(pluginsDir, "plugins.state.json");
        var knownTypes = new[]
        {
            new { File = dll, Types = new[] { new { Id = "X", Name = "Cool Sorter", Author = "Acme", Version = "1.2.0", Kind = 0 } } }
        };
        object state = disabled
            ? new
            {
                Disabled = new[] { new { File = dll, Id = "X" } },
                PendingDelete = Array.Empty<string>(),
                KnownTypes = knownTypes
            }
            : new
            {
                Disabled = Array.Empty<object>(),
                PendingDelete = Array.Empty<string>(),
                KnownTypes = knownTypes
            };
        File.WriteAllText(stateFile, JsonSerializer.Serialize(state));

        try
        {
            return new PluginManagerService(pluginsDir).LoadAllPlugins();
        }
        finally
        {
            try { Directory.Delete(pluginsDir, true); } catch { /* ignore */ }
        }
    }
}
