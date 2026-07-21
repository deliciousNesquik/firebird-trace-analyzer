using FirebirdTraceAnalyzer.Services.Plugins;

namespace FirebirdTraceAnalyzer.Interfaces.Plugins;

/// <summary>
/// Поиск, загрузка и учёт плагинов (сортировки/фильтры), разрешение коллизий версий,
/// включение/выключение и установка/удаление пакетов.
/// </summary>
public interface IPluginManagerService
{
    /// <summary>Каталог плагинов (для «открыть папку»).</summary>
    string PluginsDirectory { get; }

    /// <summary>Полный список обнаруженных плагинов (для окна управления).</summary>
    IReadOnlyList<PluginInfo> GetPlugins();

    /// <summary>Сканирует подпапки, загружает плагины, разрешает версии и вычисляет статусы.</summary>
    IReadOnlyList<PluginInfo> LoadAllPlugins();

    /// <summary>Активные (включённые и не затенённые) плагины сортировки.</summary>
    IEnumerable<ISortPlugin> GetSortPlugins();

    /// <summary>Активные (включённые и не затенённые) плагины фильтрации.</summary>
    IEnumerable<IFilterPlugin> GetFilterPlugins();

    /// <summary>Включает/выключает конкретный экземпляр плагина (файл + Id).</summary>
    void SetEnabled(string filePath, string id, bool enabled);

    /// <summary>Включён ли конкретный экземпляр плагина.</summary>
    bool IsEnabled(string filePath, string id);

    /// <summary>Группы плагинов с одинаковым Id (коллизии версий).</summary>
    IReadOnlyList<IReadOnlyList<PluginInfo>> GetCollisionGroups();

    /// <summary>Есть ли неразрешённые коллизии (несколько активных экземпляров одного Id).</summary>
    bool HasUnresolvedCollisions();

    /// <summary>Устанавливает пакет плагина из архива/папки. Возвращает успех.</summary>
    bool InstallPlugin(string sourcePath);

    /// <summary>Удаляет пакет плагина. Возвращает (удалён сейчас, отложено до перезапуска).</summary>
    (bool DeletedNow, bool Pending) DeletePackage(string folderPath);
}
