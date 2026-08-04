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
}
