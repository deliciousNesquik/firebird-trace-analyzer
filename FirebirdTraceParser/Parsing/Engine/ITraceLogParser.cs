using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.Results;

namespace FirebirdTraceParser.Parsing.Engine;

public interface ITraceLogParser
{
    ParsingResult<EventBase> ParseFile(string filePath, ParseOptions? options = null);

    /// <summary>Синхронный разбор из произвольного TextReader (без файла) с полным ParsingResult.</summary>
    ParsingResult<EventBase> Parse(TextReader reader, ParseOptions? options = null);

    Task<ParsingResult<EventBase>> ParseFileAsync(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        ParseOptions? options = null);

    IAsyncEnumerable<EventBase> ParseStreamAsync(
        Stream stream,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        ParseOptions? options = null);
}