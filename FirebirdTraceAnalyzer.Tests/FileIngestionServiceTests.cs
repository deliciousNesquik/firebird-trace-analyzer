using System.Security.Cryptography;
using FirebirdTraceAnalyzer.Services;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.Results;
using FirebirdTraceParser.Models.ValueObjects;
using FirebirdTraceParser.Parsing.Engine;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// A1: сервис приёма файлов. Хеш детерминирован (SHA-256), разбор возвращает события в порядке и
/// границы времени по первому/последнему событию. Парсер подменён фейком.
/// </summary>
public sealed class FileIngestionServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "fta_ingest_" + Guid.NewGuid().ToString("N") + ".log");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static EventBase At(DateTime ts) =>
        new TraceInitEvent
        {
            Timestamp = ts, TraceId = 1, HexTraceId = "0x01", EventType = EventType.TraceInit,
            Session = new TraceSessionInfo { SessionId = 1 }
        };

    [Fact]
    public async Task ComputeHashAsync_MatchesSha256()
    {
        var bytes = "some trace content\nline2\n"u8.ToArray();
        await File.WriteAllBytesAsync(_path, bytes);

        var svc = new FileIngestionService(new FakeParser([]));
        var expected = Convert.ToHexString(SHA256.HashData(bytes));

        Assert.Equal(expected, await svc.ComputeHashAsync(_path));
    }

    [Fact]
    public async Task ParseAsync_ReturnsEventsAndTimeBounds()
    {
        await File.WriteAllTextAsync(_path, "ignored by fake parser");
        var t1 = new DateTime(2026, 7, 21, 10, 0, 0);
        var t2 = new DateTime(2026, 7, 21, 10, 5, 0);
        var t3 = new DateTime(2026, 7, 21, 10, 9, 0);

        var svc = new FileIngestionService(new FakeParser([At(t1), At(t2), At(t3)]));
        var parsed = await svc.ParseAsync(_path);

        Assert.Equal(3, parsed.Events.Count);
        Assert.Equal(t1, parsed.StartTrace);
        Assert.Equal(t3, parsed.EndTrace);
    }

    [Fact]
    public async Task ParseAsync_EmptyFile_HasMinDateBounds()
    {
        await File.WriteAllTextAsync(_path, "");
        var svc = new FileIngestionService(new FakeParser([]));

        var parsed = await svc.ParseAsync(_path);

        Assert.Empty(parsed.Events);
        Assert.Equal(DateTime.MinValue, parsed.StartTrace);
        Assert.Equal(DateTime.MinValue, parsed.EndTrace);
    }

    private sealed class FakeParser(IReadOnlyList<EventBase> events) : ITraceLogParser
    {
        public async IAsyncEnumerable<EventBase> ParseStreamAsync(
            Stream stream, IProgress<double>? progress = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default,
            ParseOptions? options = null)
        {
            foreach (var e in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return e;
            }

            await Task.CompletedTask;
        }

        public ParsingResult<EventBase> ParseFile(string filePath, ParseOptions? options = null) => throw new NotImplementedException();
        public ParsingResult<EventBase> Parse(TextReader reader, ParseOptions? options = null) => throw new NotImplementedException();
        public Task<ParsingResult<EventBase>> ParseFileAsync(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default, ParseOptions? options = null) => throw new NotImplementedException();
    }
}
