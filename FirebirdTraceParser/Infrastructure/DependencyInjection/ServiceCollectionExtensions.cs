using System.Text.RegularExpressions;
using FirebirdTraceParser.Parsing.Engine;
using FirebirdTraceParser.Parsing.Handlers;
using FirebirdTraceParser.Parsing.Rules;
using FirebirdTraceParser.Parsing.Utils;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace FirebirdTraceParser.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет все службы парсера Firebird Trace Log.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <param name="rulesPath">Путь к файлу правил для парсера.</param>
    /// <param name="nlogConfigPath">Путь к конфигурации NLog (опционально).</param>
    public static IServiceCollection AddFirebirdTraceParser(
        this IServiceCollection services,
        string rulesPath,
        string? nlogConfigPath = null)
    {
        // настраиваем логгер, если путь не передан ищем в текущей директории
        var configPath = nlogConfigPath ?? "nlog.config";
        
        if (File.Exists(configPath))
            LogManager.Setup().LoadConfigurationFromFile(configPath);

        // регистрируем логгер для дальнейшего логирования процессов
        services.AddSingleton<ILogger>(provider =>
            LogManager.GetLogger("FirebirdTraceParser"));

        // регистрируем стандартное кеширование
        services.AddMemoryCache();

        // Опции по умолчанию. Регистрируем сам ParseOptions (а не IOptions<>), потому что
        // DefaultEventHandler/JsonRuleLoader принимают ParseOptions напрямую через конструктор.
        // Overload с настройкой перекрывает эту регистрацию (последняя AddSingleton выигрывает).
        services.AddSingleton(ParseOptions.Default);

        // сервис загрузки правил
        services.AddSingleton<IRuleLoader, JsonRuleLoader>();

        // лениво загружаем правила
        services.AddSingleton<IReadOnlyDictionary<string, Regex>>(provider =>
        {
            var loader = provider.GetRequiredService<IRuleLoader>();
            return loader.LoadRules(rulesPath);
        });

        // парсер таблиц производительности (инъектируется в обработчик)
        services.AddSingleton<IPerformanceTableParser, PerformanceTableParser>();

        // регистрируем стандартный обработчик событий
        services.AddSingleton<IEventHandler, DefaultEventHandler>();

        // регистрируем парсер (Transient - для параллельного использования)
        services.AddTransient<ITraceLogParser, TraceLogParser>();

        return services;
    }

    /// <summary>
    /// Добавляет парсер с кастомными опциями. ParseOptions — неизменяемый record (init-only
    /// свойства), поэтому настройка идёт через with-трансформ, а не Action: например
    /// <c>o =&gt; o with { ParsePerformanceTables = false }</c>.
    /// </summary>
    public static IServiceCollection AddFirebirdTraceParser(
        this IServiceCollection services,
        string rulesPath,
        Func<ParseOptions, ParseOptions> configureOptions,
        string? nlogConfigPath = null)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddFirebirdTraceParser(rulesPath, nlogConfigPath);

        // Перекрываем зарегистрированный по умолчанию ParseOptions настроенным экземпляром —
        // теперь он реально дойдёт до DefaultEventHandler(ILogger, ParseOptions) и JsonRuleLoader.
        var options = configureOptions(ParseOptions.Default)
                      ?? throw new ArgumentException("configureOptions вернул null", nameof(configureOptions));
        services.AddSingleton(options);

        return services;
    }
}