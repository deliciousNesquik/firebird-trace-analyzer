using System.Diagnostics;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services.Diagnostics;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Persistence;

/// <summary>
/// Реализация координатора хранилища. Владеет ленивым диспетчером (в режиме Off БД не создаётся),
/// режимом хранения из настроек, телеметрией записи и индикаторами фоновых задач.
/// </summary>
public sealed class EventStoreCoordinator : IEventStoreCoordinator
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Lazy<EventStoreDispatcher> _dispatcher;
    private readonly ISettingsService _settings;
    private readonly IParseTelemetry? _telemetry;
    private readonly IBackgroundTaskService? _backgroundTasks;

    public EventStoreCoordinator(
        Lazy<EventStoreDispatcher> dispatcher,
        ISettingsService settings,
        IParseTelemetry? telemetry = null,
        IBackgroundTaskService? backgroundTasks = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _telemetry = telemetry;
        _backgroundTasks = backgroundTasks;
    }

    private AppSettings App => _settings.App;

    /// <summary>Диспетчер только если режим не Off (иначе не трогаем DI/БД).</summary>
    private EventStoreDispatcher? Dispatcher => App.StorageMode == StorageMode.Off ? null : _dispatcher.Value;

    public bool IsEnabled => Dispatcher is not null;

    public async Task<bool> ContainsAsync(string fileHash)
    {
        var dispatcher = Dispatcher;
        if (dispatcher is null)
            return false;

        try
        {
            return await dispatcher.RunAsync(store => store.ContainsFile(fileHash));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "EventStore: ContainsFile failed for {Hash}", fileHash);
            return false; // при сбое проверки просто парсим как обычно
        }
    }

    public void Persist(TraceFileInfoModel file, IReadOnlyList<EventBase> events)
    {
        var dispatcher = Dispatcher;
        if (dispatcher is null)
            return;

        // Снимок ссылок: рабочий список на UI-потоке может быть очищен при быстром закрытии файла,
        // пока фоновый писатель его перечисляет.
        var snapshot = new List<EventBase>(events);
        var name = file.FileName;
        var bg = _backgroundTasks?.Begin("store-write", Loc.Tr("Background.StoreWrite"));

        dispatcher.Post(store =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                store.WriteFile(file, snapshot);
            }
            finally
            {
                sw.Stop();
                _telemetry?.AddStoreWrite(name, sw.ElapsedMilliseconds);
                bg?.Dispose();
            }
        });
    }

    public async Task<IReadOnlyList<EventBase>> ReadFileAsync(string fileHash)
    {
        var dispatcher = Dispatcher;
        if (dispatcher is null)
            return [];

        return await dispatcher.RunAsync(store => store.ReadFile(fileHash));
    }

    public async Task<IReadOnlyList<TraceFileInfoModel>> ListFilesAsync()
    {
        var dispatcher = Dispatcher;
        if (dispatcher is null)
            return [];

        try
        {
            return await dispatcher.RunAsync(store => store.ListFiles());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "EventStore: ListFiles failed");
            return [];
        }
    }

    public void RemoveIfSession(IReadOnlyCollection<string> fileHashes)
    {
        if (App.StorageMode != StorageMode.Session)
            return;

        var dispatcher = Dispatcher;
        if (dispatcher is null || fileHashes.Count == 0)
            return;

        var hashes = fileHashes.ToList();
        dispatcher.Post(store =>
        {
            foreach (var hash in hashes)
                store.DeleteFile(hash);
        });

        // Частичное удаление место на диске не освобождает — помечаем обслуживание на след. запуск
        // (VACUUM на каждое закрытие файла делать нельзя — дорого).
        App.StorageMaintenancePending = true;
        _settings.Save();
    }

    public void ClearIfSession()
    {
        if (App.StorageMode != StorageMode.Session)
            return;

        // Clear() сам делает VACUUM — обслуживание больше не требуется.
        Dispatcher?.Post(store => store.Clear());
        App.StorageMaintenancePending = false;
        _settings.Save();
    }

    public Task RunPendingMaintenanceAsync()
    {
        if (App.StorageMode == StorageMode.Off || !App.StorageMaintenancePending)
            return Task.CompletedTask;

        var dispatcher = Dispatcher;
        if (dispatcher is null)
            return Task.CompletedTask;

        var bg = _backgroundTasks?.Begin("store-maintenance", Loc.Tr("Background.StoreMaintenance"));
        dispatcher.Post(store =>
        {
            try
            {
                store.Compact();
            }
            finally
            {
                bg?.Dispose();
            }
        });

        // Флаг снимаем сразу — обслуживание уже в очереди диспетчера.
        App.StorageMaintenancePending = false;
        _settings.Save();

        return Task.CompletedTask;
    }
}
