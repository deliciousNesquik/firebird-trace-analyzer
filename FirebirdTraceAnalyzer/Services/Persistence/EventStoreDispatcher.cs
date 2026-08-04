using NLog;

namespace FirebirdTraceAnalyzer.Services.Persistence;

/// <summary>
/// Сериализует доступ к единственному (однопоточному) SQLite-соединению хранилища и выносит запись
/// с критического пути парсинга/отображения.
///
/// Все операции выполняются строго по очереди (FIFO) через цепочку задач на пуле потоков, поэтому
/// соединением в каждый момент пользуется не более одного потока. Запись ставится в очередь методом
/// <see cref="Post"/> и немедленно возвращает управление (парсинг и добавление карточки не ждут диска);
/// чтения/операции с результатом ждут завершения через <see cref="RunAsync{T}"/>. FIFO-порядок гарантирует,
/// что запись файла выполнится раньше его последующего удаления (важно для «зеркала сессии»).
///
/// Регистрируется синглтоном на тот же <see cref="IEventStore"/>, что и хранилище: единый диспетчер на
/// одно соединение — иначе параллельные обращения к соединению из разных мест приведут к гонке.
/// </summary>
public sealed class EventStoreDispatcher
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IEventStore _store;
    private readonly object _lock = new();
    private Task _tail = Task.CompletedTask;

    public EventStoreDispatcher(IEventStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Ставит операцию-мутацию в очередь и сразу возвращается (fire-and-forget). Исключения логируются,
    /// не пробрасываются — сбой записи не должен ронять UI.
    /// </summary>
    public void Post(Action<IEventStore> job)
    {
        lock (_lock)
        {
            _tail = _tail.ContinueWith(
                _ =>
                {
                    try { job(_store); }
                    catch (Exception ex) { Logger.Error(ex, "EventStore background job failed"); }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    /// <summary>Ставит операцию в очередь и возвращает задачу её завершения (без результата).</summary>
    public Task RunAsync(Action<IEventStore> job) => Enqueue(() =>
    {
        job(_store);
        return true;
    });

    /// <summary>Ставит операцию в очередь и возвращает её результат по завершении.</summary>
    public Task<T> RunAsync<T>(Func<IEventStore, T> job) => Enqueue(() => job(_store));

    /// <summary>
    /// Выполняет операцию ВНЕ очереди (параллельно с ней). Допустимо ТОЛЬКО для операций, которые НЕ
    /// трогают основное соединение стора — например <see cref="IEventStore.ExecuteQuery"/>, открывающий
    /// собственное соединение. Иначе будет гонка за единственным соединением. Нужно, чтобы тяжёлая
    /// аналитика не вставала в очередь за записью и не блокировала её (в WAL читатель не мешает писателю).
    /// </summary>
    public Task<T> RunUnqueuedAsync<T>(Func<IEventStore, T> job) => Task.Run(() => job(_store));

    private Task<T> Enqueue<T>(Func<T> job)
    {
        lock (_lock)
        {
            // ContinueWith выполняется независимо от исхода предыдущей операции (в т.ч. если та упала),
            // поэтому одна ошибка не рвёт очередь. Ошибку самой этой операции получит её awaiter.
            var next = _tail.ContinueWith(
                _ => job(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            _tail = next;
            return next;
        }
    }
}
