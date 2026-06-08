using System.Text.RegularExpressions;
using Avalonia;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceAnalyzer.Services.Plugins;
using FirebirdTraceAnalyzer.Services.Reports;
using FirebirdTraceAnalyzer.Services.Reports.Exporters;
using FirebirdTraceAnalyzer.Services.Searching;
using FirebirdTraceAnalyzer.Services.Sorting;
using FirebirdTraceAnalyzer.ViewModels;
using FirebirdTraceParser.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace FirebirdTraceAnalyzer;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var logger = LogManager.Setup()
            .LoadConfigurationFromFile("nlog.config")
            .GetCurrentClassLogger();

        try
        {
            logger.Info("Initializing the application");

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "Fatal error while starting application");
            throw;
        }
        finally
        {
            logger.Info("Shutting down the application");
            LogManager.Shutdown();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static IServiceProvider ConfigureServices()
    {
        var logger = LogManager.GetCurrentClassLogger();
        
        // конфигурация приложения, настройки, расположение и прочее
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        // DI контейнер для подключения сервисов
        var services = new ServiceCollection();
        
        // настройки приложения сопоставляются с моделями данных для использования объекта как конфигурации
        services.Configure<AppSettings>(config:configuration.GetSection("Settings"));
        services.Configure<UiSectionSettings>(config:configuration.GetSection("UI:Sections"));
        
        services.AddSingleton<IConfiguration>(configuration);

        // используем встроенный в парсере метод для подключения парсера как сервис
        services.AddFirebirdTraceParser(
            rulesPath: "Configuration/rules.json",
            nlogConfigPath: "Configuration/nlog.config"
        );

        // добавляем сервисы для ui приложения
        services.AddSingleton<IEventPropertyAccessor, EventPropertyAccessor>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IWindowProvider, WindowProvider>();
        services.AddSingleton<ISortingService, SortingService>();
        services.AddSingleton<IFilteringService, FilteringService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<PluginManagerService>();
        
        services.AddSingleton<IFieldDiscoveryService, FieldDiscoveryService>();
        
        // SSH сервисы
        services.AddSingleton<ISshConnectionService, SshConnectionService>();
        services.AddSingleton<IRemoteFileService, RemoteFileService>();
        services.AddSingleton<ICredentialStorageService, CredentialStorageService>();
        
        // сервисы отчетов
        services.AddSingleton<IReportTemplateService, ReportTemplateService>();
        services.AddSingleton<IReportGenerationService, ReportGenerationService>();
        
        services.AddTransient<ReportDesignerViewModel>();
        services.AddTransient<ReportPreviewViewModel>();
        services.AddTransient<ReportHistoryViewModel>();

        
        services.AddSingleton<PdfReportExporter>();
        services.AddSingleton<IReportExporter>(provider => provider.GetRequiredService<PdfReportExporter>());

        services.AddSingleton<CsvReportExporter>();
        services.AddSingleton<IReportExporter>(provider => provider.GetRequiredService<CsvReportExporter>());

        services.AddSingleton<DocxReportExporter>();
        services.AddSingleton<IReportExporter>(provider => provider.GetRequiredService<DocxReportExporter>());

        services.AddSingleton<XlsxReportExporter>();
        services.AddSingleton<IReportExporter>(provider => provider.GetRequiredService<XlsxReportExporter>());

        // добавляем ViewModels главного окна
        services.AddTransient<MainWindowViewModel>();
        
        // view model для ssh удаленного скачивания файлов
        services.AddTransient<RemoteConnectionDialogViewModel>();
        services.AddTransient<RemoteFileSelectionViewModel>();
        services.AddTransient<DownloadProgressViewModel>();

        // собираем все в провайдера
        var serviceProvider = services.BuildServiceProvider();

        // валидируем парсера, потому что без него ничего не сможем обработать!
        ValidateParserConfiguration(serviceProvider, logger);
        
        return serviceProvider;
    }

    private static void ValidateParserConfiguration(IServiceProvider provider, ILogger logger)
    {
        try
        {
            // получаем правила, которые загрузились в парсер
            var rules = provider.GetRequiredService<IReadOnlyDictionary<string, Regex>>();
            
            logger.Info("{RuleCount} rule(s) was loaded", rules.Count);

            // в случае если правил парсинга нет, выбрасываем ошибку
            if (rules.Count == 0)
            {
                logger.Fatal("No rules were loaded");
                throw new Exception("No rules were loaded");
            }
            
            // перебираем все правила и отображаем превью до 50 символов
            foreach (var rule in rules)
            {
                var preview = rule.Value.ToString().Length > 50 ? $"{rule.Value.ToString()[..47]}..." : rule.Value.ToString();
                logger.Debug($"Rule loaded: {rule.Key, -25} -> {preview}");
            }
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "Failed to load parser rules. The application will now close.");
            throw new Exception($"Failed to load parser rules. {ex.Message}");
        }
    }
}