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
public class PluginManagerService : IPluginManagerService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly string _pluginsDirectory;
    private readonly string _stateFile;

    private readonly List<PluginInfo> _plugins = new();

    // Выключение хранится по экземпляру плагина = (путь к DLL, Id), а НЕ по файлу и НЕ по Id:
    //  • по Id нельзя — при коллизии два плагина делят один Id;
    //  • по файлу нельзя — одна DLL может содержать несколько плагинов (напр. и сортировку, и фильтр),
    //    и выключение файла выключило бы их все сразу.
    // Ключ (файл + Id) различает конкретный класс в конкретной DLL. Выбор «какой из коллизии
    // оставить» = выключить остальные экземпляры этой же группы Id.
    private HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _pendingDelete = new();

    // Плагины (Id + снимок метаданных), которые каждая DLL экспортировала при последней успешной
    // загрузке. Учатся, когда плагин ещё включён и грузится штатно; позволяют на следующем старте
    // ПРОПУСТИТЬ загрузку сборки (и её конструкторов), если ВСЕ её плагины выключены — иначе код
    // «выключенного» плагина исполнялся бы при Activator.CreateInstance. Метаданные нужны, чтобы заглушка
    // выключенного плагина показывала настоящие Name/Author/Version/Kind, а не голый Id.
    private Dictionary<string, List<KnownPluginMeta>> _knownTypesByPath = new(StringComparer.OrdinalIgnoreCase);

    // Разделитель для ключа экземпляра; NUL не встречается в путях/Id.
    private const char InstanceKeySep = '\0';
    private static string InstanceKey(string filePath, string id) => filePath + InstanceKeySep + id;
    private static string InstanceKey(PluginInfo p) => InstanceKey(p.FilePath, p.Id);

    /// <param name="pluginsDirectory">
    /// Каталог плагинов. Если не задан — %AppData%/FirebirdTraceAnalyzer/Plugins (шов для тестов;
    /// DI подставляет значение по умолчанию).
    /// </param>
    public PluginManagerService(string? pluginsDirectory = null)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "Plugins");
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

                // Если ВСЕ типы этой DLL (по памяти прошлой загрузки) выключены — НЕ загружаем сборку,
                // чтобы конструктор выключенного плагина не исполнялся. Показываем их как Disabled из памяти.
                if (AllKnownTypesDisabled(pluginDllPath))
                {
                    AddDisabledPlaceholders(pluginDllPath);
                    continue;
                }

                LoadFromDll(pluginDllPath, fileName);
            }
        }

        ResolveVersionsAndStatuses();

        // Персистим выученные типы по путям (для скипа выключенных DLL на следующем старте).
        SaveState();
        return _plugins;
    }

    // МОДЕЛЬ ДОВЕРИЯ (безопасность): плагины — это .NET-сборки, которые загружаются и исполняются
    // с ПОЛНЫМИ правами процесса приложения (изоляции/песочницы/проверки подписи нет — .NET не даёт
    // надёжной внутрипроцессной песочницы). То есть установка плагина = запуск произвольного кода на
    // машине пользователя. Это осознанное проектное решение (плагины ставит сам пользователь из
    // доверенных источников), эквивалентное запуску обычной программы. НЕ загружайте сюда сборки из
    // недоверенных источников. Ужесточение (подпись/manifest allowlist/отдельный процесс) — при
    // необходимости отдельной задачей.
    private void LoadFromDll(string pluginDllPath, string fileName)
    {
        try
        {
            var context = new PluginLoadContext(pluginDllPath);
            var assembly = context.LoadFromAssemblyPath(pluginDllPath);

            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IAnalyzerPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            var loadedMeta = new List<KnownPluginMeta>();

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
                loadedMeta.Add(new KnownPluginMeta
                {
                    Id = instance.Id, Name = instance.Name, Author = instance.Author,
                    Version = instance.Version, Kind = (int)kind
                });
            }

            // Запоминаем состав типов этой DLL (с метаданными) — чтобы на следующем старте пропустить её
            // загрузку, если все они выключены, и показать заглушки с настоящими метаданными.
            _knownTypesByPath[pluginDllPath] = loadedMeta;
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
    /// Можно ли пропустить загрузку DLL: известен её состав типов с прошлой загрузки И все они выключены.
    /// Тогда сборка не грузится и конструкторы не исполняются.
    /// </summary>
    private bool AllKnownTypesDisabled(string dllPath) =>
        _knownTypesByPath.TryGetValue(dllPath, out var types)
        && types.Count > 0
        && types.All(t => _disabled.Contains(InstanceKey(dllPath, t.Id)));

    /// <summary>
    /// Добавляет записи-заглушки (Status=Disabled, Instance=null) для плагинов пропущенной DLL — чтобы
    /// они были видны в UI с настоящими метаданными и их можно было включить обратно (тогда DLL
    /// загрузится на следующем старте).
    /// </summary>
    private void AddDisabledPlaceholders(string dllPath)
    {
        Logger.Info("Skipping load of fully-disabled plugin DLL (no code executed): {Path}", dllPath);
        foreach (var meta in _knownTypesByPath[dllPath])
            _plugins.Add(new PluginInfo
            {
                Id = meta.Id,
                Name = string.IsNullOrEmpty(meta.Name) ? meta.Id : meta.Name,
                Author = string.IsNullOrEmpty(meta.Author) ? "—" : meta.Author,
                Version = string.IsNullOrEmpty(meta.Version) ? "—" : meta.Version,
                FilePath = dllPath,
                Kind = (PluginKind)meta.Kind,
                ParsedVersion = ParseVersion(meta.Version),
                Instance = null,
                Status = PluginStatus.Disabled
            });
    }

    /// <summary>
    /// Проставляет статусы: выключенные пользователем экземпляры (по паре путь+Id) → Disabled; среди
    /// оставшихся с одинаковым plugin Id активна старшая версия, прочие — Shadowed. Чтобы при коллизии
    /// оставить конкретный экземпляр — выключите остальные (см. <see cref="SetEnabled"/>).
    /// </summary>
    private void ResolveVersionsAndStatuses()
    {
        foreach (var group in _plugins
                     .Where(p => p.Status != PluginStatus.LoadError)
                     .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
        {
            // Кандидаты на «активность» — только не выключенные пользователем экземпляры (путь+Id).
            var enabled = group.Where(p => !_disabled.Contains(InstanceKey(p))).ToList();

            foreach (var p in group)
                p.Status = _disabled.Contains(InstanceKey(p)) ? PluginStatus.Disabled : PluginStatus.Shadowed;

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
    /// Включает/выключает конкретный экземпляр плагина (по паре путь DLL + Id) и сохраняет состояние
    /// на диск. Вступает в силу после перезапуска приложения (регистрация происходит на старте).
    /// </summary>
    public void SetEnabled(string filePath, string id, bool enabled)
    {
        var key = InstanceKey(filePath, id);
        if (enabled)
            _disabled.Remove(key);
        else
            _disabled.Add(key);

        SaveState();
    }

    public bool IsEnabled(string filePath, string id) => !_disabled.Contains(InstanceKey(filePath, id));

    /// <summary>
    /// Группы коллизий: плагины (не сбойные) с одинаковым Id, где таких плагинов больше одного.
    /// Каждая группа — набор конкурирующих за один Id DLL.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<PluginInfo>> GetCollisionGroups() =>
        _plugins
            .Where(p => p.Status != PluginStatus.LoadError)
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => (IReadOnlyList<PluginInfo>)g.ToList())
            .ToList();

    /// <summary>
    /// Есть ли неразрешённые коллизии: группа с одним Id, где включено больше одного экземпляра
    /// (пользователь ещё не выбрал, какой оставить). Используется, чтобы на старте предложить выбор.
    /// </summary>
    public bool HasUnresolvedCollisions() =>
        GetCollisionGroups().Any(g => g.Count(p => IsEnabled(p.FilePath, p.Id)) > 1);

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
        // Безопасность: удаляем только СТРОГО внутри каталога плагинов. Сверяем префикс с
        // разделителем в конце, иначе соседняя папка вроде "PluginsEvil" тоже прошла бы проверку
        // (StartsWith("…/Plugins") без разделителя), и её можно было бы удалить.
        var full = Path.GetFullPath(folderPath);
        var root = Path.GetFullPath(_pluginsDirectory);
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warn("Refused to delete outside plugins dir: {Path}", folderPath);
            return (false, false);
        }

        try
        {
            if (Directory.Exists(full))
                Directory.Delete(full, recursive: true);

            _pendingDelete.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
            ForgetPluginsUnder(full);
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
            ForgetPluginsUnder(full);
            SaveState();
            return (false, true);
        }
    }

    /// <summary>Убирает из снимка (<see cref="_plugins"/>) записи, чьи DLL лежат в удаляемой папке,
    /// чтобы окно (список и коллизии) сразу отражало удаление.</summary>
    private void ForgetPluginsUnder(string folderFullPath)
    {
        var prefix = folderFullPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        _plugins.RemoveAll(p =>
            Path.GetFullPath(p.FilePath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
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

        var root = Path.GetFullPath(_pluginsDirectory);
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        // Регистрочувствительность — по ФС (как и остальные операции с путями плагинов): Windows/macOS
        // регистронезависимы, Linux — регистрозависим. Ordinal на Windows ложно счёл бы вложенный путь
        // с иным регистром «вне каталога».
        var pathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (var folder in _pendingDelete.ToList())
        {
            try
            {
                // Удаляем ТОЛЬКО пути внутри каталога плагинов: state.json недоверенный (мог быть
                // подменён/повреждён), а Directory.Delete(recursive) по произвольному пути — это удаление
                // чужих данных.
                var full = Path.GetFullPath(folder);
                if (!full.StartsWith(rootWithSep, pathComparison))
                {
                    Logger.Warn("Refusing to delete pending path outside plugins dir: {Path}", folder);
                    _pendingDelete.Remove(folder);
                    continue;
                }

                if (Directory.Exists(full))
                    Directory.Delete(full, recursive: true);
                _pendingDelete.Remove(folder);
                Logger.Info("Deleted pending plugin package: {Path}", full);
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
        _disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _pendingDelete = new List<string>();
        _knownTypesByPath = new Dictionary<string, List<KnownPluginMeta>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (File.Exists(_stateFile))
            {
                var json = File.ReadAllText(_stateFile);
                var state = JsonSerializer.Deserialize<PluginsState>(json);
                if (state?.Disabled is { Count: > 0 })
                    foreach (var d in state.Disabled)
                        if (!string.IsNullOrEmpty(d.File) && !string.IsNullOrEmpty(d.Id))
                            _disabled.Add(InstanceKey(d.File, d.Id));
                if (state?.PendingDelete is { Count: > 0 })
                    _pendingDelete = state.PendingDelete;
                if (state?.KnownTypes is { Count: > 0 })
                    foreach (var kt in state.KnownTypes)
                    {
                        if (string.IsNullOrEmpty(kt.File))
                            continue;
                        if (kt.Types is { Count: > 0 })
                            _knownTypesByPath[kt.File] = kt.Types;
                        else if (kt.Ids is { Count: > 0 })
                            // Старый формат {File, Ids:[...]} — мигрируем, чтобы skip-гвард пережил апгрейд
                            // (иначе конструктор выключенного плагина исполнился бы). Метаданные заполнятся
                            // при следующей штатной загрузке.
                            _knownTypesByPath[kt.File] = kt.Ids.Select(id => new KnownPluginMeta { Id = id }).ToList();
                    }
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
                new PluginsState
                {
                    Disabled = _disabled.Select(SplitKey).ToList(),
                    PendingDelete = _pendingDelete,
                    KnownTypes = _knownTypesByPath
                        .Select(kv => new KnownTypesEntry { File = kv.Key, Types = kv.Value })
                        .ToList()
                },
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

    /// <summary>Разбивает внутренний ключ экземпляра обратно на путь и Id для сохранения.</summary>
    private static DisabledEntry SplitKey(string key)
    {
        var i = key.IndexOf(InstanceKeySep);
        return i < 0
            ? new DisabledEntry { File = key, Id = "" }
            : new DisabledEntry { File = key[..i], Id = key[(i + 1)..] };
    }

    private sealed class DisabledEntry
    {
        public string File { get; set; } = "";
        public string Id { get; set; } = "";
    }

    private sealed class KnownPluginMeta
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Version { get; set; } = "";
        public int Kind { get; set; } // (int)PluginKind — флаги
    }

    private sealed class KnownTypesEntry
    {
        public string File { get; set; } = "";
        public List<KnownPluginMeta> Types { get; set; } = new();

        // Легаси-поле прежнего формата (только Id, без метаданных). Читается ради миграции старого
        // plugins.state.json; при следующем SaveState файл перезаписывается в формате Types.
        public List<string>? Ids { get; set; }
    }

    private sealed class PluginsState
    {
        public List<DisabledEntry> Disabled { get; set; } = new();
        public List<string> PendingDelete { get; set; } = new();
        public List<KnownTypesEntry> KnownTypes { get; set; } = new();
    }
}
