using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Координатор хранилища событий: инкапсулирует режим хранения (Off/Session/Accumulate), доступ к
/// диспетчеру (единое SQLite-соединение, FIFO-очередь), фоновую запись/чтение и обслуживание.
/// Выносит эту ответственность из MainWindowViewModel; применение прочитанных событий к UI-коллекциям
/// остаётся у вызывающего (тонкая оркестрация).
/// </summary>
public interface IEventStoreCoordinator
{
    /// <summary>Хранилище активно (режим не Off и диспетчер доступен). В режиме Off БД не создаётся.</summary>
    bool IsEnabled { get; }

    /// <summary>Есть ли файл с таким хэшем в хранилище (через очередь диспетчера). При сбое — false.</summary>
    Task<bool> ContainsAsync(string fileHash);

    /// <summary>
    /// Ставит запись событий файла в фоновую очередь и сразу возвращается (диск не на критическом пути).
    /// Работает по снимку списка — исключает гонку с очисткой рабочего набора на UI-потоке.
    /// </summary>
    void Persist(TraceFileInfoModel file, IReadOnlyList<EventBase> events);

    /// <summary>Читает события файла из хранилища (порядок = порядок записи). Пустой список, если хранилище выключено.</summary>
    Task<IReadOnlyList<EventBase>> ReadFileAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>Манифест файлов в хранилище (для восстановления сессии). Пустой список при сбое/выключенном хранилище.</summary>
    Task<IReadOnlyList<TraceFileInfoModel>> ListFilesAsync();

    /// <summary>
    /// Режим Session — «зеркало сессии»: удаляет файлы из хранилища при их закрытии/удалении. В
    /// Accumulate — no-op. Ставит удаление в ту же FIFO-очередь и помечает отложенное обслуживание.
    /// </summary>
    void RemoveIfSession(IReadOnlyCollection<string> fileHashes);

    /// <summary>Режим Session: полностью очищает хранилище (закрытие всех файлов = пустая сессия).</summary>
    void ClearIfSession();

    /// <summary>
    /// Отложенное обслуживание на старте: если помечено, фоново выполняет чистку осиротевших словарей
    /// + VACUUM и снимает флаг. Не блокирует.
    /// </summary>
    Task RunPendingMaintenanceAsync();
}
