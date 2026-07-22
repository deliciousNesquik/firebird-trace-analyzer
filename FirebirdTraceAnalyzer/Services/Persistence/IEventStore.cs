using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Models.Storage;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Services.Persistence;

/// <summary>Запись событий в хранилище.</summary>
public interface IEventStoreWriter
{
    /// <summary>
    /// Записывает события одного файла (по его хэшу) в одной транзакции. Повторный вызов с тем же
    /// хэшем заменяет ранее записанные события этого файла. SQL/подключения дедуплицируются между
    /// файлами. Коммит по завершении файла — этого достаточно для аварийного восстановления.
    /// </summary>
    void WriteFile(TraceFileInfoModel file, IEnumerable<EventBase> events);
}

/// <summary>Чтение событий и манифеста файлов.</summary>
public interface IEventStoreReader
{
    /// <summary>Есть ли в хранилище файл с таким хэшем.</summary>
    bool ContainsFile(string fileHash);

    /// <summary>Восстанавливает события одного файла (в порядке записи).</summary>
    IReadOnlyList<EventBase> ReadFile(string fileHash);

    /// <summary>Список файлов в хранилище (манифест).</summary>
    IReadOnlyList<TraceFileInfoModel> ListFiles();

    /// <summary>Стрим событий по диапазону времени (без загрузки всего в память).</summary>
    IEnumerable<EventBase> Query(DateTime? from = null, DateTime? to = null);
}

/// <summary>Обслуживание хранилища: удаление файлов, очистка, сжатие.</summary>
public interface IEventStoreMaintenance
{
    /// <summary>Удаляет один файл и его события (осиротевшие словари не трогаем — чистит Vacuum/обслуживание).</summary>
    void DeleteFile(string fileHash);

    /// <summary>Полностью очищает хранилище (для сессионного режима — в начале нового парсинга).
    /// Возвращает место на диск и пересобирает структуру/индексы (VACUUM).</summary>
    void Clear();

    /// <summary>
    /// Обслуживание: удаляет осиротевшие дедуп-словари (sql_text/attachment без ссылок), затем
    /// VACUUM — пересборка БД и индексов, возврат места на диск. Тяжёлая операция — вызывать
    /// отложенно/фоново, не на каждый DeleteFile.
    /// </summary>
    void Compact();
}

/// <summary>Перенос данных между файлами-хранилищами (импорт/экспорт).</summary>
public interface IEventStoreTransfer
{
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
}

/// <summary>Сводная статистика и разбивка размера хранилища.</summary>
public interface IEventStoreStatistics
{
    /// <summary>Сводная статистика хранилища.</summary>
    EventStoreStatistics GetStatistics();

    /// <summary>Разбивка размера: строки по таблицам + байты текстовых нагрузок (тяжёлый полный скан — по запросу).</summary>
    EventStoreSizeBreakdown GetSizeBreakdown();
}

/// <summary>Интроспекция схемы и произвольные read-only запросы (окно «Анализ хранилища»).</summary>
public interface IEventStoreSchema
{
    /// <summary>Схема БД по интроспекции (sqlite_master + PRAGMA table_info): таблицы и их колонки
    /// в порядке объявления. Служебные таблицы SQLite (sqlite_%) исключены. Используется
    /// автодополнением окна «Анализ хранилища» — всегда актуальна реальной DDL.</summary>
    IReadOnlyList<SchemaTable> GetSchema();

    /// <summary>
    /// Выполняет произвольный SELECT/WITH (только чтение) и возвращает результат динамическими
    /// колонками/строками. Записи отклоняются (PRAGMA query_only + валидация). Строк не больше
    /// <paramref name="maxRows"/> (с признаком усечения). Вызывать только через диспетчер.
    /// </summary>
    StorageQueryResult ExecuteQuery(string sql, int maxRows, CancellationToken cancellationToken = default);
}

/// <summary>
/// Персистентное хранилище распарсенных трейс-событий на SQLite с дедупом SQL-текстов и подключений.
/// Обслуживает и «сессионный» кэш переоткрытия, и накопительный архив (режим выбирается вызывающим).
/// Один файл БД переносим между пользователями.
///
/// Разделено по ролям (ISP): запись (<see cref="IEventStoreWriter"/>), чтение
/// (<see cref="IEventStoreReader"/>), обслуживание (<see cref="IEventStoreMaintenance"/>), перенос
/// (<see cref="IEventStoreTransfer"/>), статистика (<see cref="IEventStoreStatistics"/>) и схема/запросы
/// (<see cref="IEventStoreSchema"/>). Потребители могут зависеть от нужной узкой роли; составной
/// <see cref="IEventStore"/> оставлен для диспетчера и DI-регистрации единой реализации.
/// </summary>
public interface IEventStore :
    IDisposable,
    IEventStoreWriter,
    IEventStoreReader,
    IEventStoreMaintenance,
    IEventStoreTransfer,
    IEventStoreStatistics,
    IEventStoreSchema
{
}
