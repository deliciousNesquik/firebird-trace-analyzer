namespace FirebirdTraceAnalyzer.Services.Persistence;

/// <summary>
/// Разбивка размера хранилища: число строк по таблицам и байты текстовых полезных нагрузок
/// (SQL-тексты, значения параметров, сообщения ошибок). Постраничная разбивка таблица/индекс
/// недоступна — в бандле e_sqlite3 нет виртуальной таблицы <c>dbstat</c>, — поэтому «остальное»
/// (числовые колонки event + индексы + служебные страницы) считается как размер БД минус текст.
/// </summary>
public sealed record EventStoreSizeBreakdown
{
    public required long DbSizeBytes { get; init; }

    // Число строк по таблицам
    public required long EventRows { get; init; }
    public required long SqlTextRows { get; init; }
    public required long AttachmentRows { get; init; }
    public required long ParameterRows { get; init; }
    public required long ErrorLineRows { get; init; }
    public required long PerfItemRows { get; init; }

    // Байты текстовых нагрузок (несжатые, по LENGTH(CAST(... AS BLOB)))
    public required long SqlTextBytes { get; init; }
    public required long ParameterBytes { get; init; }
    public required long ErrorMessageBytes { get; init; }

    /// <summary>Суммарные байты текстовых полезных нагрузок.</summary>
    public long TextPayloadBytes => SqlTextBytes + ParameterBytes + ErrorMessageBytes;

    /// <summary>Оценка «остального»: индексы + числовые колонки + служебные страницы (размер БД − текст).</summary>
    public long OtherBytes => Math.Max(0, DbSizeBytes - TextPayloadBytes);
}
