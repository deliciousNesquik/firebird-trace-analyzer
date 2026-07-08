namespace FirebirdTraceAnalyzer.Services.Persistence;

/// <summary>Сводная статистика хранилища событий (для окна управления/статистики).</summary>
public sealed record EventStoreStatistics
{
    /// <summary>Файлов в хранилище.</summary>
    public required int FileCount { get; init; }

    /// <summary>Всего событий.</summary>
    public required long EventCount { get; init; }

    /// <summary>Уникальных SQL-текстов (после дедупа).</summary>
    public required long UniqueSqlCount { get; init; }

    /// <summary>Уникальных подключений (после дедупа).</summary>
    public required long UniqueAttachmentCount { get; init; }

    /// <summary>Начало временного диапазона событий (null — если пусто).</summary>
    public required DateTime? RangeStart { get; init; }

    /// <summary>Конец временного диапазона событий (null — если пусто).</summary>
    public required DateTime? RangeEnd { get; init; }

    /// <summary>Размер файла БД на диске, байт.</summary>
    public required long DbSizeBytes { get; init; }

    /// <summary>Суммарный размер исходных трейс-файлов, байт (для оценки сжатия).</summary>
    public required long RawSizeBytes { get; init; }

    /// <summary>Коэффициент сжатия raw/db (0, если БД пуста).</summary>
    public double CompressionRatio => DbSizeBytes > 0 ? (double)RawSizeBytes / DbSizeBytes : 0;
}
