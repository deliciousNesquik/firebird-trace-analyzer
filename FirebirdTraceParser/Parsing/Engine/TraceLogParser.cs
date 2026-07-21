using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.Results;
using FirebirdTraceParser.Parsing.Handlers;
using FirebirdTraceParser.Parsing.Rules;
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

    // block_header проверяется на КАЖДОЙ строке файла — держим Regex в поле, чтобы не делать
    // словарный lookup в горячем цикле. Отсутствие правила — ошибка конфигурации (fail-fast).
    private readonly Regex _blockHeaderRx = rules is not null && rules.TryGetValue(RuleKeys.BlockHeader, out var bh)
        ? bh
        : throw new ArgumentException("rules должны содержать правило 'block_header'", nameof(rules));

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
        // IsMatch не аллоцирует объект Match — а строк тела блока на порядки больше заголовков.
        // Матч выполняется на недоверенной строке, поэтому таймаут ReDoS не должен ронять весь
        // разбор: ловим RegexMatchTimeoutException, фиксируем предупреждение и пропускаем строку.
        bool isHeader;
        try
        {
            isHeader = _blockHeaderRx.IsMatch(line);
        }
        catch (RegexMatchTimeoutException ex)
        {
            warnings.Add(new ParsingWarning
            {
                Severity = options.ValidationMode == ValidationMode.Strict
                    ? WarningSeverity.Error
                    : WarningSeverity.Warning,
                Message = $"Regex timeout matching block header: {ex.Message}",
                LineNumber = lineNumber
            });
            _logger.Warn(ex, "Regex timeout matching block_header at line {LineNumber}", lineNumber);
            return;
        }

        if (isHeader)
        {
            // Новый блок - обработать предыдущий
            if (currentBlock.HasData) FlushBlock(currentBlock, events, warnings, options, context);

            currentBlock.Reset();
            currentBlock.Header = _blockHeaderRx.Match(line); // Match только на редких строках-заголовках
            currentBlock.StartLine = lineNumber;
        }
        else if (currentBlock.HasData && !string.IsNullOrWhiteSpace(line))
        {
            if (currentBlock.BodyLines.Count >= options.MaxBlockLines)
            {
                // Тело блока превысило лимит — защита от OOM на файле без границ блоков.
                // Усекаем: сбрасываем то, что накопили, и игнорируем хвост до следующего заголовка.
                warnings.Add(new ParsingWarning
                {
                    Severity = WarningSeverity.Warning,
                    Message = $"Block body exceeded MaxBlockLines ({options.MaxBlockLines}); block truncated",
                    LineNumber = currentBlock.StartLine,
                    BlockContent = string.Join("\n", currentBlock.BodyLines.Take(3))
                });
                FlushBlock(currentBlock, events, warnings, options, context);
                currentBlock.Reset();
                return;
            }

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
            {
                events.Add(evt);
            }
            else
            {
                // Блок не дал события (неизвестный тип или не хватило обязательных данных) — он
                // пропущен. Фиксируем как Warning, чтобы это попало в SkippedBlocks и не терялось тихо.
                warnings.Add(new ParsingWarning
                {
                    Severity = WarningSeverity.Warning,
                    Message = "Block skipped: no event produced (unknown type or missing data)",
                    LineNumber = block.StartLine,
                    BlockContent = string.Join("\n", block.BodyLines.Take(3))
                });
            }
        }
        catch (Exception ex)
        {
            // Строгий режим трактует сбой разбора как ошибку (HasErrors), мягкий — как предупреждение.
            var severity = options.ValidationMode == ValidationMode.Strict
                ? WarningSeverity.Error
                : WarningSeverity.Warning;

            warnings.Add(new ParsingWarning
            {
                Severity = severity,
                Message = $"Failed to parse block: {ex.Message}",
                LineNumber = block.StartLine,
                BlockContent = string.Join("\n", block.BodyLines.Take(3))
            });

            // Уровень лога соответствует severity результата (в Strict сбой блока — это Error).
            if (severity == WarningSeverity.Error)
                _logger.Error(ex, "Failed to parse block at line {LineNumber}", block.StartLine);
            else
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