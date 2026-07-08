namespace FirebirdTraceAnalyzer.Services.Diagnostics;

/// <summary>Как события файла попали в приложение (для колонки «источник» в статистике).</summary>
public enum ParseSource
{
    LocalParse,
    RemoteParse,
    StoreCache
}

/// <summary>
/// Тайминги обработки одного файла по фазам конвейера (мс). Заполняется по мере прохождения фаз;
/// производные показатели (события/с, МБ/с, мкс/событие) считаются на лету для отображения.
/// </summary>
public sealed class FileParseMetric
{
    public required string Name { get; init; }
    public ParseSource Source { get; set; }
    public long SizeBytes { get; set; }
    public long EventCount { get; set; }

    /// <summary>Скачивание с удалённого сервера (0 для локальных файлов).</summary>
    public long DownloadMs { get; set; }

    /// <summary>Получение событий: парсинг ИЛИ чтение из хранилища (см. <see cref="FromCache"/>).</summary>
    public long ProduceMs { get; set; }

    /// <summary>Запись в хранилище (0, если хранилище выключено или файл пришёл из кэша).</summary>
    public long StoreWriteMs { get; set; }

    /// <summary>Добавление в рабочий набор/UI (AllEvents + карточка).</summary>
    public long UiMs { get; set; }

    /// <summary>События взяты из хранилища (кэш переоткрытия), а не распарсены.</summary>
    public bool FromCache { get; set; }

    /// <summary>Короткая метка источника для таблицы (dev-инструмент, не локализуется).</summary>
    public string SourceShort => Source switch
    {
        ParseSource.LocalParse => "local",
        ParseSource.RemoteParse => "remote",
        ParseSource.StoreCache => "cache",
        _ => "?"
    };

    public long TotalMs => DownloadMs + ProduceMs + StoreWriteMs + UiMs;
    public double EventsPerSec => ProduceMs > 0 ? EventCount * 1000.0 / ProduceMs : 0;
    public double MbPerSec => ProduceMs > 0 ? SizeBytes / 1048576.0 * 1000.0 / ProduceMs : 0;
    public double MicrosPerEvent => EventCount > 0 ? ProduceMs * 1000.0 / EventCount : 0;
}
