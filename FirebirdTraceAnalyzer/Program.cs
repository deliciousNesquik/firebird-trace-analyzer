using System.Text.RegularExpressions;
using Avalonia;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.EventLinking;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceAnalyzer.Interfaces.Plugins;
using FirebirdTraceAnalyzer.Interfaces.Filtering;
using FirebirdTraceAnalyzer.Interfaces.Remote;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports.Exporters;
using FirebirdTraceAnalyzer.Interfaces.Searching;
using FirebirdTraceAnalyzer.Interfaces.Sorting;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services;
using FirebirdTraceAnalyzer.Services.Diagnostics;
using FirebirdTraceAnalyzer.Services.Dialogs;
using FirebirdTraceAnalyzer.Services.EventLinking;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceAnalyzer.Services.Persistence;
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
        // Пути к конфигам строим от каталога приложения (AppContext.BaseDirectory), а не от
        // текущего рабочего каталога: при запуске .app из Finder/launchd CWD = "/", и
        // относительные пути ("Configuration/rules.json" и т.п.) не находятся → приложение падает.
        var baseDir = AppContext.BaseDirectory;

        // Прописываем пути логов в NLog (GDC) ДО первой записи, читая их прямо из settings.json,
        // чтобы стартовые логи (включая логи парсера) сразу шли в настроенную папку.
        LogConfiguration.ApplyFromSettingsFile();

        var logger = LogManager.Setup()
            .LoadConfigurationFromFile(Path.Combine(baseDir, "Configuration", "nlog.config"))
            .GetCurrentClassLogger();

        // Глобальные страховочные обработчики: фоновые падения (пул потоков, незамеченные Task)
        // не доходят до try вокруг UI-цикла и иначе ушли бы без единой записи в лог.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.Fatal(e.ExceptionObject as Exception,
                "Unhandled exception (IsTerminating={Terminating})", e.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

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

        // База — каталог приложения (AppContext.BaseDirectory), а не CWD: при запуске .app из
        // Finder/launchd текущий каталог = "/", и относительные пути не находятся → краш.
        var baseDir = AppContext.BaseDirectory;

        // конфигурация приложения, настройки, расположение и прочее
        var configuration = new ConfigurationBuilder()
            .SetBasePath(baseDir)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        // DI контейнер для подключения сервисов
        var services = new ServiceCollection();
        
        // настройки приложения сопоставляются с моделями данных для использования объекта как конфигурации
        services.Configure<AppSettings>(config:configuration.GetSection("Settings"));
        services.Configure<UiSectionSettings>(config:configuration.GetSection("UI:Sections"));

        services.AddSingleton<IConfiguration>(configuration);

        // Сервис пользовательских настроек: значения по умолчанию из appsettings.json,
        // сохранение изменений — в %AppData%/FirebirdTraceAnalyzer/settings.json
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // Реестр видимых фоновых задач (мини-панель «идёт фоновая работа»).
        services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();

        // Сбор таймингов конвейера (скачивание/парсинг/запись/UI) за сессию — для окна «Статистика парсера».
        services.AddSingleton<IParseTelemetry, ParseTelemetryService>();

        // Хранилище распарсенных событий (SQLite). Создаётся лениво при первом обращении;
        // пока никто не резолвит — файл БД не создаётся. Путь — из настроек.
        services.AddSingleton<IEventStore>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            var dir = settings.GetEventStoreDirectory();
            return new EventStoreService(Path.Combine(dir, "events.db"));
        });

        // Диспетчер сериализует доступ к единственному соединению стора и выносит запись с критического
        // пути. Синглтон на тот же IEventStore — один диспетчер на одно соединение.
        services.AddSingleton<EventStoreDispatcher>(sp =>
            new EventStoreDispatcher(sp.GetRequiredService<IEventStore>()));

        // Ленивая обёртка: позволяет внедрять диспетчер в конструктор (вместо App.Services), НЕ создавая
        // соединение/БД, пока к нему реально не обратятся (в режиме StorageMode.Off — не обращаемся).
        services.AddSingleton(sp => new Lazy<EventStoreDispatcher>(sp.GetRequiredService<EventStoreDispatcher>));

        // Координатор хранилища: инкапсулирует режим/диспетчер/запись/чтение/обслуживание (вынос из MainWindowViewModel).
        services.AddSingleton<IEventStoreCoordinator, EventStoreCoordinator>();

        // используем встроенный в парсере метод для подключения парсера как сервис
        services.AddFirebirdTraceParser(
            rulesPath: RulesConfiguration.EnsureRulesFile(),
            nlogConfigPath: Path.Combine(baseDir, "Configuration", "nlog.config")
        );

        // добавляем сервисы для ui приложения
        services.AddSingleton<IEventPropertyAccessor, EventPropertyAccessor>();
        services.AddSingleton<IEventChainService, EventChainService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IWindowProvider, WindowProvider>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISortingService, SortingService>();
        services.AddSingleton<IFilteringService, FilteringService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IPluginManagerService, PluginManagerService>();
        
        services.AddSingleton<IFieldDiscoveryService, FieldDiscoveryService>();
        
        // SSH сервисы
        services.AddSingleton<IHostKeyStore, KnownHostsStore>();
        services.AddSingleton<ISshProfileStore, SshProfileStore>();
        services.AddSingleton<ISshConnectionService, SshConnectionService>();
        services.AddSingleton<IRemoteFileService, RemoteFileService>();
        services.AddSingleton<ICredentialStorageService, CredentialStorageService>();
        
        // сервисы отчетов
        services.AddSingleton<IReportHistoryStore, ReportHistoryStore>();
        services.AddSingleton<IReportTemplateService, ReportTemplateService>();
        services.AddSingleton<IReportProjectionService, ReportProjectionService>();
        services.AddSingleton<IReportGenerationService, ReportGenerationService>();
        
        services.AddTransient<ReportDesignerViewModel>();
        services.AddTransient<ReportPreviewViewModel>();
        services.AddTransient<ReportHistoryViewModel>();
        services.AddTransient<ManageTemplatesViewModel>();

        
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
        services.AddTransient<SettingsWindowViewModel>();
        
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
            // Сохраняем первопричину (тип + стек) как InnerException, а не только текст сообщения.
            throw new InvalidOperationException("Failed to load parser rules.", ex);
        }
    }
}