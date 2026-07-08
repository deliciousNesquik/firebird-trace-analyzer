namespace FirebirdTraceAnalyzer.Services.Diagnostics;

/// <summary>
/// Потокобезопасный сборщик таймингов в памяти. Один <see cref="FileParseMetric"/> на файл (ключ — имя);
/// фазы дописываются под общим замком. Регистрируется синглтоном; данные живут одну сессию.
/// </summary>
public sealed class ParseTelemetryService : IParseTelemetry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, FileParseMetric> _byName = new(StringComparer.Ordinal);
    private long _finalizeMs;

    public long FinalizeMs
    {
        get { lock (_sync) return _finalizeMs; }
    }

    private FileParseMetric GetOrAdd(string name)
    {
        if (!_byName.TryGetValue(name, out var m))
        {
            m = new FileParseMetric { Name = name };
            _byName[name] = m;
        }
        return m;
    }

    public void RecordDownload(string name, long ms, long bytes)
    {
        lock (_sync)
        {
            var m = GetOrAdd(name);
            m.DownloadMs = ms;
            if (bytes > 0) m.SizeBytes = bytes;
        }
    }

    public void RecordProduce(string name, long ms, long eventCount, long sizeBytes, ParseSource source, bool fromCache)
    {
        lock (_sync)
        {
            var m = GetOrAdd(name);
            m.ProduceMs = ms;
            m.EventCount = eventCount;
            if (sizeBytes > 0) m.SizeBytes = sizeBytes;
            m.Source = source;
            m.FromCache = fromCache;
        }
    }

    public void AddStoreWrite(string name, long ms)
    {
        lock (_sync)
            GetOrAdd(name).StoreWriteMs += ms;
    }

    public void AddUi(string name, long ms)
    {
        lock (_sync)
            GetOrAdd(name).UiMs += ms;
    }

    public void AddFinalize(long ms)
    {
        lock (_sync)
            _finalizeMs += ms;
    }

    public IReadOnlyList<FileParseMetric> Snapshot()
    {
        lock (_sync)
            // Копируем и метрики, и список — UI не должен видеть изменяющиеся под замком объекты.
            return _byName.Values.Select(Copy).ToList();
    }

    public void Clear()
    {
        lock (_sync)
        {
            _byName.Clear();
            _finalizeMs = 0;
        }
    }

    private static FileParseMetric Copy(FileParseMetric m) => new()
    {
        Name = m.Name,
        Source = m.Source,
        SizeBytes = m.SizeBytes,
        EventCount = m.EventCount,
        DownloadMs = m.DownloadMs,
        ProduceMs = m.ProduceMs,
        StoreWriteMs = m.StoreWriteMs,
        UiMs = m.UiMs,
        FromCache = m.FromCache
    };
}
