using System.Reflection;
using FirebirdTraceAnalyzer.Interfaces.Plugins;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Plugins;

/// <summary>Сервис поиска и инициализации плагинов</summary>
public class PluginManagerService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly string _pluginsDirectory;
    private readonly List<IAnalyzerPlugin> _loadedPlugins = new();

    public PluginManagerService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _pluginsDirectory = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "Plugins");
        
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
        }
    }

    /// <summary>Сканирует поддиректории и динамически загружает все валидные плагины</summary>
    public IReadOnlyList<IAnalyzerPlugin> LoadAllPlugins()
    {
        _loadedPlugins.Clear();

        if (!Directory.Exists(_pluginsDirectory)) return _loadedPlugins;

        // Обходим все поддиректории в папке Plugins
        foreach (var pluginDir in Directory.GetDirectories(_pluginsDirectory))
        {
            // Ищем все файлы с расширением .dll внутри текущей папки
            var dllFiles = Directory.GetFiles(pluginDir, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (var pluginDllPath in dllFiles)
            {
                var fileName = Path.GetFileName(pluginDllPath);

                // Защита: пропускаем сборку самого SDK, если пользователь случайно скопировал её в эту папку
                if (fileName.Equals("FirebirdTraceParser.dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var context = new PluginLoadContext(pluginDllPath);
                    var assembly = context.LoadFromAssemblyPath(pluginDllPath);
                    
                    // Безопасно извлекаем типы, реализующие IAnalyzerPlugin
                    // Используем GetTypes(), но обрабатываем возможные исключения загрузки типов
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IAnalyzerPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in pluginTypes)
                    {
                        if (Activator.CreateInstance(type) is IAnalyzerPlugin pluginInstance)
                            _loadedPlugins.Add(pluginInstance);
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Возникает, если это библиотека зависимостей, и мы не можем считать её типы напрямую
                    Logger.Warn(ex, $"Предупреждение: Не удалось загрузить типы из сборки {fileName}. Возможно, это библиотека зависимостей.");
                }
                catch (Exception ex)
                {
                    // Логируем критические ошибки загрузки конкретной DLL, но не прерываем цикл для остальных
                    Logger.Warn(ex, $"Ошибка при обработке файла {fileName}");
                }
            }
        }

        return _loadedPlugins;
    }

    public IEnumerable<ISortPlugin> GetSortPlugins() => _loadedPlugins.OfType<ISortPlugin>();
    public IEnumerable<IFilterPlugin> GetFilterPlugins() => _loadedPlugins.OfType<IFilterPlugin>();
}