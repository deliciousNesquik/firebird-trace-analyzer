using System.Reflection;
using System.Text.Json;
using FirebirdTraceAnalyzer.Interfaces.Plugins;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Plugins;

/// <summary>
/// Поиск, загрузка и учёт плагинов. Ведёт полный список обнаруженных плагинов (<see cref="PluginInfo"/>)
/// с метаданными, состоянием и ошибками загрузки; разрешает коллизии версий (при совпадении plugin Id
/// побеждает старшая версия, остальные — затеняются); хранит список выключенных плагинов на диске.
/// Регистрируются только «эффективные» плагины (активные: включённые и не затенённые).
/// </summary>
public class PluginManagerService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly string _pluginsDirectory;
    private readonly string _stateFile;

    private readonly List<PluginInfo> _plugins = new();
    private HashSet<string> _disabledIds = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _pendingDelete = new();

    public PluginManagerService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _pluginsDirectory = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "Plugins");
        _stateFile = Path.Combine(_pluginsDirectory, "plugins.state.json");

        if (!Directory.Exists(_pluginsDirectory))
            Directory.CreateDirectory(_pluginsDirectory);
    }

    /// <summary>Каталог плагинов (для «открыть папку»).</summary>
    public string PluginsDirectory => _pluginsDirectory;

    /// <summary>Полный список обнаруженных плагинов (для окна управления).</summary>
    public IReadOnlyList<PluginInfo> GetPlugins() => _plugins;

    /// <summary>Сканирует подпапки, загружает плагины, разрешает версии и вычисляет статусы.</summary>
    public IReadOnlyList<PluginInfo> LoadAllPlugins()
    {
        _plugins.Clear();
        LoadState();

        // Отложенные удаления (пакеты, которые были залочены в прошлой сессии) — до сканирования.
        ProcessPendingDeletions();

        if (!Directory.Exists(_pluginsDirectory))
            return _plugins;

        foreach (var pluginDir in Directory.GetDirectories(_pluginsDirectory))
        {
            var dllFiles = Directory.GetFiles(pluginDir, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (var pluginDllPath in dllFiles)
            {
                var fileName = Path.GetFileName(pluginDllPath);

                // Сборку самого SDK пропускаем, если её случайно скопировали в папку плагина.
                if (fileName.Equals("FirebirdTraceParser.dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                LoadFromDll(pluginDllPath, fileName);
            }
        }

        ResolveVersionsAndStatuses();
        return _plugins;
    }

    private void LoadFromDll(string pluginDllPath, string fileName)
    {
        try
        {
            var context = new PluginLoadContext(pluginDllPath);
            var assembly = context.LoadFromAssemblyPath(pluginDllPath);

            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IAnalyzerPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in pluginTypes)
            {
                if (Activator.CreateInstance(type) is not IAnalyzerPlugin instance)
                    continue;

                var kind = PluginKind.None;
                if (instance is ISortPlugin) kind |= PluginKind.Sort;
                if (instance is IFilterPlugin) kind |= PluginKind.Filter;

                _plugins.Add(new PluginInfo
                {
                    Id = instance.Id,
                    Name = instance.Name,
                    Author = instance.Author,
                    Version = instance.Version,
                    FilePath = pluginDllPath,
                    Kind = kind,
                    ParsedVersion = ParseVersion(instance.Version),
                    Instance = instance,
                    Status = PluginStatus.Active // уточняется в ResolveVersionsAndStatuses
                });
            }
        }
        catch (Exception ex)
        {
            // Не роняем загрузку остальных: фиксируем ошибку записью, чтобы её было видно в окне.
            Logger.Warn(ex, "Failed to load plugin assembly {File}", fileName);

            _plugins.Add(new PluginInfo
            {
                Id = fileName,
                Name = fileName,
                Author = "—",
                Version = "—",
                FilePath = pluginDllPath,
                Kind = PluginKind.None,
                Status = PluginStatus.LoadError,
                LoadError = ex.Message,
                Instance = null
            });
        }
    }

    /// <summary>
    /// Проставляет статусы: выключенные (по сохранённому списку) → Disabled; среди оставшихся с
    /// одинаковым plugin Id старшая версия → Active, прочие → Shadowed.
    /// </summary>
    private void ResolveVersionsAndStatuses()
    {
        foreach (var group in _plugins
                     .Where(p => p.Status != PluginStatus.LoadError)
                     .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
        {
            // Кандидаты на «активность» — только не выключенные пользователем.
            var enabled = group.Where(p => !_disabledIds.Contains(p.Id)).ToList();

            foreach (var p in group)
                p.Status = _disabledIds.Contains(p.Id) ? PluginStatus.Disabled : PluginStatus.Shadowed;

            // Победитель среди включённых — с наибольшей версией (нераспарсенные считаем 0.0).
            var winner = enabled
                .OrderByDescending(p => p.ParsedVersion ?? new Version(0, 0))
                .FirstOrDefault();

            if (winner is not null)
                winner.Status = PluginStatus.Active;

            if (enabled.Count > 1)
                Logger.Warn(
                    "Multiple plugins with Id '{Id}' — keeping v{Version}, shadowing {Count} older.",
                    winner!.Id, winner.Version, enabled.Count - 1);
        }
    }

    /// <summary>Эффективные плагины для регистрации: активные (включённые и не затенённые).</summary>
    private IEnumerable<IAnalyzerPlugin> EffectivePlugins() =>
        _plugins.Where(p => p.Status == PluginStatus.Active && p.Instance is not null)
                .Select(p => p.Instance!);

    public IEnumerable<ISortPlugin> GetSortPlugins() => EffectivePlugins().OfType<ISortPlugin>();
    public IEnumerable<IFilterPlugin> GetFilterPlugins() => EffectivePlugins().OfType<IFilterPlugin>();

    /// <summary>
    /// Включает/выключает плагин по Id и сохраняет состояние на диск. Вступает в силу после
    /// перезапуска приложения (регистрация происходит на старте).
    /// </summary>
    public void SetEnabled(string pluginId, bool enabled)
    {
        if (enabled)
            _disabledIds.Remove(pluginId);
        else
            _disabledIds.Add(pluginId);

        SaveState();
    }

    public bool IsEnabled(string pluginId) => !_disabledIds.Contains(pluginId);

    /// <summary>
    /// Устанавливает плагин из одиночной DLL или ZIP-архива. Для каждой установки создаётся отдельная
    /// подпапка в каталоге плагинов (по имени файла/архива): DLL копируется туда, ZIP — распаковывается.
    /// Другие форматы отклоняются. Вступает в силу после перезапуска. Возвращает true при успехе.
    /// </summary>
    public bool InstallPlugin(string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return false;

            var ext = Path.GetExtension(sourcePath);
            var isDll = ext.Equals(".dll", StringComparison.OrdinalIgnoreCase);
            var isZip = ext.Equals(".zip", StringComparison.OrdinalIgnoreCase);

            if (!isDll && !isZip)
            {
                Logger.Warn("Rejected plugin install — unsupported file type: {Src}", sourcePath);
                return false;
            }

            Directory.CreateDirectory(_pluginsDirectory);

            // Подпапка по имени файла/архива, с защитой от коллизии имён.
            var baseName = Path.GetFileNameWithoutExtension(sourcePath);
            var dir = Path.Combine(_pluginsDirectory, baseName);
            var i = 1;
            while (Directory.Exists(dir))
                dir = Path.Combine(_pluginsDirectory, $"{baseName}_{i++}");

            Directory.CreateDirectory(dir);

            if (isDll)
            {
                File.Copy(sourcePath, Path.Combine(dir, Path.GetFileName(sourcePath)));
            }
            else
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(sourcePath, dir);

                // В архиве обязана быть хотя бы одна DLL, иначе это не пакет плагина.
                var hasDll = Directory.EnumerateFiles(dir, "*.dll", SearchOption.AllDirectories).Any();
                if (!hasDll)
                {
                    Directory.Delete(dir, recursive: true);
                    Logger.Warn("Rejected plugin install — no DLL in archive: {Src}", sourcePath);
                    return false;
                }

                FlattenSingleRootFolder(dir);
            }

            Logger.Info("Installed plugin from {Src} -> {Dir}", sourcePath, dir);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to install plugin from {Src}", sourcePath);
            return false;
        }
    }

    /// <summary>
    /// Удаляет пакет плагина (всю подпапку целиком — DLL и зависимости). Если файлы залочены
    /// текущей сессией, помечает папку на удаление при следующем старте. Возвращает
    /// (deletedNow, pending).
    /// </summary>
    public (bool DeletedNow, bool Pending) DeletePackage(string folderPath)
    {
        // Безопасность: удаляем только внутри каталога плагинов.
        var full = Path.GetFullPath(folderPath);
        var root = Path.GetFullPath(_pluginsDirectory);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warn("Refused to delete outside plugins dir: {Path}", folderPath);
            return (false, false);
        }

        try
        {
            if (Directory.Exists(full))
                Directory.Delete(full, recursive: true);

            _pendingDelete.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
            SaveState();
            Logger.Info("Deleted plugin package: {Path}", full);
            return (true, false);
        }
        catch (Exception ex)
        {
            // Скорее всего файл залочен загруженной сборкой — удалим при следующем запуске.
            Logger.Warn(ex, "Package delete deferred (locked?): {Path}", full);
            if (!_pendingDelete.Contains(full, StringComparer.OrdinalIgnoreCase))
                _pendingDelete.Add(full);
            SaveState();
            return (false, true);
        }
    }

    /// <summary>
    /// Если после распаковки на верхнем уровне нет DLL, но есть единственная подпапка с содержимым
    /// (типичный случай «заархивировали папку целиком») — поднимает её содержимое на уровень выше,
    /// чтобы сканер, читающий только верхний уровень подпапки плагина, нашёл DLL.
    /// </summary>
    private static void FlattenSingleRootFolder(string dir)
    {
        try
        {
            var topDlls = Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly);
            if (topDlls.Length > 0)
                return;

            var entries = Directory.GetFileSystemEntries(dir);
            if (entries.Length != 1 || !Directory.Exists(entries[0]))
                return;

            var inner = entries[0];
            foreach (var file in Directory.GetFiles(inner))
                File.Move(file, Path.Combine(dir, Path.GetFileName(file)));
            foreach (var sub in Directory.GetDirectories(inner))
                Directory.Move(sub, Path.Combine(dir, Path.GetFileName(sub)));

            Directory.Delete(inner, recursive: true);
        }
        catch (Exception ex)
        {
            // Не критично: если не удалось «поднять» — оставляем как есть.
            Logger.Warn(ex, "Could not flatten extracted plugin folder {Dir}", dir);
        }
    }

    private void ProcessPendingDeletions()
    {
        if (_pendingDelete.Count == 0)
            return;

        foreach (var folder in _pendingDelete.ToList())
        {
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
                _pendingDelete.Remove(folder);
                Logger.Info("Deleted pending plugin package: {Path}", folder);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Still cannot delete pending package: {Path}", folder);
            }
        }

        SaveState();
    }

    private void LoadState()
    {
        _disabledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _pendingDelete = new List<string>();

        try
        {
            if (File.Exists(_stateFile))
            {
                var json = File.ReadAllText(_stateFile);
                var state = JsonSerializer.Deserialize<PluginsState>(json);
                if (state?.Disabled is { Count: > 0 })
                    _disabledIds = new HashSet<string>(state.Disabled, StringComparer.OrdinalIgnoreCase);
                if (state?.PendingDelete is { Count: > 0 })
                    _pendingDelete = state.PendingDelete;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to read plugins state");
        }
    }

    private void SaveState()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new PluginsState { Disabled = _disabledIds.ToList(), PendingDelete = _pendingDelete },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_stateFile, json);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to save plugins state");
        }
    }

    private static Version? ParseVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Отсекаем возможный SemVer-суффикс ("1.2.3-beta" → "1.2.3").
        var core = raw.Split('-', '+')[0];
        return Version.TryParse(core, out var v) ? v : null;
    }

    private sealed class PluginsState
    {
        public List<string> Disabled { get; set; } = new();
        public List<string> PendingDelete { get; set; } = new();
    }
}
