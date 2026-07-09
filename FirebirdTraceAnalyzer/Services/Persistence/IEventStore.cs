using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Services.Persistence;

/// <summary>
/// Персистентное хранилище распарсенных трейс-событий на SQLite с дедупом SQL-текстов и подключений.
/// Обслуживает и «сессионный» кэш переоткрытия, и накопительный архив (режим выбирается вызывающим).
/// Один файл БД переносим между пользователями. Этап 1: изолированное ядро (без привязки к конвейеру).
/// </summary>
public interface IEventStore : IDisposable
{
    /// <summary>
    /// Записывает события одного файла (по его хэшу) в одной транзакции. Повторный вызов с тем же
    /// хэшем заменяет ранее записанные события этого файла. SQL/подключения дедуплицируются между
    /// файлами. Коммит по завершении файла — этого достаточно для аварийного восстановления.
    /// </summary>
    void WriteFile(TraceFileInfoModel file, IEnumerable<EventBase> events);

    /// <summary>Есть ли в хранилище файл с таким хэшем.</summary>
    bool ContainsFile(string fileHash);

    /// <summary>Восстанавливает события одного файла (в порядке записи).</summary>
    IReadOnlyList<EventBase> ReadFile(string fileHash);

    /// <summary>Список файлов в хранилище (манифест).</summary>
    IReadOnlyList<TraceFileInfoModel> ListFiles();

    /// <summary>Стрим событий по диапазону времени (без загрузки всего в память).</summary>
    IEnumerable<EventBase> Query(DateTime? from = null, DateTime? to = null);

    /// <summary>Удаляет один файл и его события (осиротевшие словари не трогаем — чистит Vacuum/обслуживание).</summary>
    void DeleteFile(string fileHash);

    /// <summary>Полностью очищает хранилище (для сессионного режима — в начале нового парсинга).</summary>
    void Clear();

    /// <summary>
    /// Импортирует файлы из другого файла-хранилища (перенос между пользователями). Файлы с уже
    /// существующим хэшем пропускаются; SQL/подключения дедуплицируются. Возвращает число импортированных файлов.
    /// </summary>
    int ImportFrom(string sourceDbPath);

    /// <summary>
    /// Экспортирует указанные файлы в новый самодостаточный файл-хранилище (для передачи другому
    /// пользователю). Существующий целевой файл перезаписывается.
    /// </summary>
    void ExportTo(string targetDbPath, IEnumerable<TraceFileInfoModel> files);

    /// <summary>Сводная статистика хранилища.</summary>
    EventStoreStatistics GetStatistics();

    /// <summary>Разбивка размера: строки по таблицам + байты текстовых нагрузок (тяжёлый полный скан — по запросу).</summary>
    EventStoreSizeBreakdown GetSizeBreakdown();
}
