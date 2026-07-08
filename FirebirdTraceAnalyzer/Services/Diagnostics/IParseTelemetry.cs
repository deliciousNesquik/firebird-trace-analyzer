namespace FirebirdTraceAnalyzer.Services.Diagnostics;

/// <summary>
/// Сбор таймингов обработки файлов по фазам конвейера (скачивание/парсинг/запись/UI) за текущую сессию.
/// Метрики держатся только в памяти и сбрасываются при перезапуске. Потокобезопасно: фазы одного файла
/// могут писаться из разных потоков (парсинг, фоновая запись в стор, UI-поток).
/// </summary>
public interface IParseTelemetry
{
    /// <summary>Тайминг скачивания файла с удалённого сервера.</summary>
    void RecordDownload(string name, long ms, long bytes);

    /// <summary>Тайминг получения событий: парсинг (<paramref name="fromCache"/> = false) или чтение из стора.</summary>
    void RecordProduce(string name, long ms, long eventCount, long sizeBytes, ParseSource source, bool fromCache);

    /// <summary>Добавляет тайминг записи в хранилище (может вызываться из фоновой очереди).</summary>
    void AddStoreWrite(string name, long ms);

    /// <summary>Добавляет тайминг добавления в рабочий набор/UI (накапливается по файлу).</summary>
    void AddUi(string name, long ms);

    /// <summary>Накапливает время «финализации» сессии — пересчёт фильтров/сортировок/статистики (пакетно).</summary>
    void AddFinalize(long ms);

    /// <summary>Суммарное время финализации за сессию, мс.</summary>
    long FinalizeMs { get; }

    /// <summary>Снимок метрик по файлам (копия, безопасно для UI).</summary>
    IReadOnlyList<FileParseMetric> Snapshot();

    /// <summary>Сбрасывает все метрики сессии.</summary>
    void Clear();
}
