using System.Diagnostics;
using System.Security.Cryptography;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Parsing.Engine;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Приём трейс-файлов: SHA-256 и потоковый разбор. Разбор — CPU-работа, уводится в Task.Run
/// (ConfigureAwait(false)), чтобы не грузить UI-поток на файлах в миллионы событий.
/// </summary>
public sealed class FileIngestionService : IFileIngestionService
{
    /// <summary>Буфер FileStream (1 МБ) — крупные последовательные чтения.</summary>
    private const int FileStreamBufferBytes = 1024 * 1024;

    /// <summary>Начальная ёмкость списка событий одного файла — реже перевыделяем при росте.</summary>
    private const int InitialEventCapacity = 8192;

    private readonly ITraceLogParser _parser;

    public FileIngestionService(ITraceLogParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
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

        var (events, start, end) = await Task.Run(async () =>
        {
            var list = new List<EventBase>(InitialEventCapacity);
            var startTrace = DateTime.MinValue;
            var endTrace = DateTime.MinValue;

            await foreach (var evt in _parser.ParseStreamAsync(stream, cancellationToken: cancellationToken)
                               .ConfigureAwait(false))
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

        return new ParsedFile(events, start, end, parseSw.ElapsedMilliseconds);
    }
}
