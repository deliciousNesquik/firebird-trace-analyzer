using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.Results;
using FirebirdTraceParser.Parsing.Handlers;
using NLog;

namespace FirebirdTraceParser.Parsing.Engine;

public sealed class TraceLogParser(
    IReadOnlyDictionary<string, Regex> rules,
    IEventHandler handler,
    ILogger logger)
    : ITraceLogParser
{
    private readonly IReadOnlyDictionary<string, Regex> _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    private readonly IEventHandler _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public ParsingResult<EventBase> ParseFile(string filePath, ParseOptions? options = null)
    {
        options ??= ParseOptions.Default;

        _logger.Info("Starting parsing file: {FilePath}", filePath);

        var events = new List<EventBase>();
        var warnings = new List<ParsingWarning>();

        using var reader = new StreamReader(filePath, options.Encoding);

        string? line;
        var lineNumber = 0;
        var currentBlock = new BlockBuffer();
        var context = new ParsingContext();

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            ProcessLine(line, lineNumber, currentBlock, events, warnings, options, context);
        }

        // Последний блок
        if (currentBlock.HasData)
            FlushBlock(currentBlock, events, warnings, options, context);

        _logger.Info("Parsing completed: {EventCount} events, {WarningCount} warnings",
            events.Count, warnings.Count);

        return new ParsingResult<EventBase>
        {
            Events = events,
            Warnings = warnings
        };
    }

    public async Task<ParsingResult<EventBase>> ParseFileAsync(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        ParseOptions? options = null)
    {
        options ??= ParseOptions.Default;

        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;

        var events = new List<EventBase>();
        var warnings = new List<ParsingWarning>();

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, true);
        
        using var reader = new StreamReader(stream, options.Encoding);
        
        var lineNumber = 0;
        var currentBlock = new BlockBuffer();
        var context = new ParsingContext();

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;

            cancellationToken.ThrowIfCancellationRequested();

            ProcessLine(line, lineNumber, currentBlock, events, warnings, options, context);

            // Прогресс по позиции потока — дёшево и без повторного кодирования строки
            if (progress != null && lineNumber % 1000 == 0 && fileSize > 0)
                progress.Report((double)stream.Position / fileSize);
        }

        if (currentBlock.HasData)
            FlushBlock(currentBlock, events, warnings, options, context);

        progress?.Report(1.0);

        return new ParsingResult<EventBase>
        {
            Events = events,
            Warnings = warnings
        };
    }

    public async IAsyncEnumerable<EventBase> ParseStreamAsync(
        Stream stream,
        IProgress<double>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        ParseOptions? options = null)
    {
        options ??= ParseOptions.Default;

        using var reader = new StreamReader(stream, options.Encoding);

        var buffer = new List<EventBase>(options.BatchSize);
        var currentBlock = new BlockBuffer();
        var warnings = new List<ParsingWarning>();
        var context = new ParsingContext();
        
        var lineNumber = 0;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            cancellationToken.ThrowIfCancellationRequested();

            ProcessLine(line, lineNumber, currentBlock, buffer, warnings, options, context);

            // Yield батчами
            if (buffer.Count < options.BatchSize) continue;
            
            foreach (var evt in buffer)
                yield return evt;
            
            buffer.Clear();
        }

        // Последний блок
        if (currentBlock.HasData)
            FlushBlock(currentBlock, buffer, warnings, options, context);

        // Логируем накопленные предупреждения через ILogger (это библиотека — Console здесь недопустим)
        foreach (var warning in warnings)
        {
            switch (warning.Severity)
            {
                case WarningSeverity.Error:
                    _logger.Error("Parse warning at line {LineNumber}: {Message}", warning.LineNumber, warning.Message);
                    break;
                case WarningSeverity.Warning:
                    _logger.Warn("Parse warning at line {LineNumber}: {Message}", warning.LineNumber, warning.Message);
                    break;
                default:
                    _logger.Info("Parse info at line {LineNumber}: {Message}", warning.LineNumber, warning.Message);
                    break;
            }
        }

        // Остаток батча
        foreach (var evt in buffer)
            yield return evt;
    }

    private void ProcessLine(
        string line,
        int lineNumber,
        BlockBuffer currentBlock,
        List<EventBase> events,
        List<ParsingWarning> warnings,
        ParseOptions options,
        ParsingContext context)
    {
        var blockMatch = _rules["block_header"].Match(line);

        if (blockMatch.Success)
        {
            // Новый блок - обработать предыдущий
            if (currentBlock.HasData) FlushBlock(currentBlock, events, warnings, options, context);

            currentBlock.Reset();
            currentBlock.Header = blockMatch;
            currentBlock.StartLine = lineNumber;
        }
        else if (currentBlock.HasData && !string.IsNullOrWhiteSpace(line))
        {
            currentBlock.BodyLines.Add(line);
        }
    }

    private void FlushBlock(
        BlockBuffer block,
        List<EventBase> events,
        List<ParsingWarning> warnings,
        ParseOptions options,
        ParsingContext context)
    {
        try
        {
            var evt = _handler.Handle(block.Header!, block.BodyLines, _rules, context);

            if (evt != null)
                events.Add(evt);
            else
                warnings.Add(new ParsingWarning
                {
                    Severity = WarningSeverity.Info,
                    Message = "Block handler returned null",
                    LineNumber = block.StartLine
                });
        }
        catch (Exception ex)
        {
            var severity = options.ValidationMode == ValidationMode.Strict
                ? WarningSeverity.Warning
                : WarningSeverity.Error;

            warnings.Add(new ParsingWarning
            {
                Severity = severity,
                Message = $"Failed to parse block: {ex.Message}",
                LineNumber = block.StartLine,
                BlockContent = string.Join("\n", block.BodyLines.Take(3))
            });

            _logger.Warn(ex, "Failed to parse block at line {LineNumber}", block.StartLine);
        }
    }

    private sealed class BlockBuffer
    {
        public Match? Header { get; set; }
        public List<string> BodyLines { get; } = new();
        public int StartLine { get; set; }
        public bool HasData => Header != null;

        public void Reset()
        {
            Header = null;
            BodyLines.Clear();
            StartLine = 0;
        }
    }
}