using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.Results;
using FirebirdTraceParser.Parsing.Engine;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Приём трейс-файлов: SHA-256 и потоковый разбор. Разбор — CPU-работа, уводится в Task.Run
/// (ConfigureAwait(false)), чтобы не грузить UI-поток на файлах в миллионы событий.
/// </summary>
public sealed class FileIngestionService : IFileIngestionService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>Буфер FileStream (1 МБ) — крупные последовательные чтения.</summary>
    private const int FileStreamBufferBytes = 1024 * 1024;

    /// <summary>Начальная ёмкость списка событий одного файла — реже перевыделяем при росте.</summary>
    private const int InitialEventCapacity = 8192;

    private readonly ITraceLogParser _parser;

    public FileIngestionService(ITraceLogParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Собирает ParsingWarning из потока разбора: без этого предупреждения парсера живут
    /// только в parser.log, а приложение считает загрузку полностью успешной, даже когда
    /// часть блоков файла пропущена.
    /// </summary>
    private sealed class WarningCollector(Action<ParsingWarning> onNext) : IObserver<ParsingWarning>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(ParsingWarning value) => onNext(value);
    }

    public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes, useAsync: true);

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes);
    }

    public async Task<ParsedFile> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var parseSw = Stopwatch.StartNew();

        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes, useAsync: true);

        var warnings = new List<ParsingWarning>();
        var options = new ParseOptions
        {
            WarningObserver = new WarningCollector(warnings.Add),
        };

        var (events, start, end) = await Task.Run(async () =>
        {
            var list = new List<EventBase>(InitialEventCapacity);
            var startTrace = DateTime.MinValue;
            var endTrace = DateTime.MinValue;

            await foreach (var evt in _parser.ParseStreamAsync(stream, options: options,
                               cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (startTrace == DateTime.MinValue)
                    startTrace = evt.Timestamp;

                endTrace = evt.Timestamp;
                list.Add(evt);
            }

            return (list, startTrace, endTrace);
        }, cancellationToken);

        parseSw.Stop();

        // Предупреждения парсера дублируются в app-лог суммарно: детали построчно остались
        // в parser.log, но файл с пропущенными блоками в app-логе выглядит как чистая загрузка.
        if (warnings.Count > 0)
        {
            var errorCount = warnings.Count(w => w.Severity == WarningSeverity.Error);
            Logger.Warn(
                "Parsed {File}: {Events} event(s), {Warnings} parse warning(s) ({Errors} with Error severity) — события могут быть неполными",
                Path.GetFileName(filePath), events.Count, warnings.Count, errorCount);
        }

        return new ParsedFile(events, start, end, parseSw.ElapsedMilliseconds, warnings);
    }
}
