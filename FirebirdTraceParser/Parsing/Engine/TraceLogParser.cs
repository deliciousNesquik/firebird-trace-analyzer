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
        ValidateOptions(options);
        _logger.Info("Starting parsing file: {FilePath}", filePath);

        using var reader = new StreamReader(filePath, options.Encoding);
        return Parse(reader, options);
    }

    /// <summary>
    /// Синхронный разбор из произвольного <see cref="TextReader"/> — ядро <see cref="ParseFile"/>.
    /// Не зависит от файловой системы, возвращает полный <see cref="ParsingResult{T}"/> с Warnings,
    /// поэтому поведение SkippedBlocks/Strict проверяется тестами через <c>new StringReader(trace)</c>.
    /// </summary>
    public ParsingResult<EventBase> Parse(
        TextReader reader,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        options ??= ParseOptions.Default;
        ValidateOptions(options);

        var events = new List<EventBase>();
        var warnings = new List<ParsingWarning>();

        string? line;
        var lineNumber = 0;
        var currentBlock = new BlockBuffer();
        var context = new ParsingContext();

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            cancellationToken.ThrowIfCancellationRequested();
            ProcessLine(line, lineNumber, currentBlock, events, warnings, options, context);
        }

        // Последний блок
        if (currentBlock.HasData)
            FlushBlock(currentBlock, events, warnings, options, context);

        LogWarnings(warnings);
        options.WarningObserver?.OnCompleted();
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
        ValidateOptions(options);

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

        LogWarnings(warnings);
        options.WarningObserver?.OnCompleted();
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

        // Контракт владения (issue #36): поток принадлежит вызывающему - парсер не закрывает его
        ValidateOptions(options);

        // (leaveOpen), в том числе при раннем выходе из await foreach.
        using var reader = new StreamReader(stream, options.Encoding,
            detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);

        var buffer = new List<EventBase>(options.BatchSize);
        var currentBlock = new BlockBuffer();
        var warnings = new List<ParsingWarning>();
        var context = new ParsingContext();

        var lineNumber = 0;
        // Прогресс по позиции потока (issue #35): доступен только для seekable-потоков;
        // для non-seekable остаётся финальный отчёт 1.0.
        var canReportPosition = progress != null && stream.CanSeek;
        var totalBytes = canReportPosition ? stream.Length : 0;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            cancellationToken.ThrowIfCancellationRequested();

            ProcessLine(line, lineNumber, currentBlock, buffer, warnings, options, context);

            if (canReportPosition && lineNumber % 1000 == 0)
                progress!.Report(Math.Min(1.0, (double)stream.Position / totalBytes));

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
        LogWarnings(warnings);
        options.WarningObserver?.OnCompleted();

        // Остаток батча
        foreach (var evt in buffer)
            yield return evt;

        progress?.Report(1.0);
    }

    // Единое логирование предупреждений для всех трёх API — иначе файловые пути «слепы» по логам.
    private void LogWarnings(IReadOnlyList<ParsingWarning> warnings)
    {
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
    }

    // Единая точка эмиссии warning'ов (issue #34): список результата + OnNext наблюдателю.
    private static void AddWarning(List<ParsingWarning> warnings, ParseOptions options, ParsingWarning warning)
    {
        warnings.Add(warning);
        options.WarningObserver?.OnNext(warning);
    }

    // Fail-fast на конфигурации (issue #40): мусорные опции ловятся на входе, а не дают
    // ноль событий со спамом предупреждений посреди разбора.
    private static void ValidateOptions(ParseOptions options)
    {
        if (options.BatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.BatchSize,
                "BatchSize must be positive.");
        if (options.MaxBlockLines <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxBlockLines,
                "MaxBlockLines must be positive.");
        if (options.RegexTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), options.RegexTimeout,
                "RegexTimeout must be positive.");
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
        // Guard охватывает и IsMatch на каждой строке, и второй Match на строке-заголовке
        // (issue #45): таймаут на любом из матчей деградирует в предупреждение, а не роняет
        // весь разбор. IsMatch оставлен быстрым путём для строк тел (Match аллоцирует объект).
        try
        {
            if (_blockHeaderRx.IsMatch(line))
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
                    AddWarning(warnings, options, new ParsingWarning
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
        catch (RegexMatchTimeoutException ex)
        {
            AddWarning(warnings, options, new ParsingWarning
            {
                Severity = options.ValidationMode == ValidationMode.Strict
                    ? WarningSeverity.Error
                    : WarningSeverity.Warning,
                Message = $"Regex timeout matching block header: {ex.Message}",
                LineNumber = lineNumber
            });
            _logger.Warn(ex, "Regex timeout matching block_header at line {LineNumber}", lineNumber);
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
            var evt = _handler.Handle(block.Header!, block.BodyLines, _rules, context, options);

            if (evt != null)
            {
                events.Add(evt);

                // MinValue сюда попадает только из ParseEventMetadata: битый ts деградировал
                // до дефолта - терять этот факт молча нельзя (issue #37).
                if (evt.Timestamp == default)
                {
                    AddWarning(warnings, options, new ParsingWarning
                    {
                        Severity = WarningSeverity.Warning,
                        Message = "Event has unparseable timestamp; defaulted to DateTime.MinValue",
                        LineNumber = block.StartLine
                    });
                }
            }
            else
            {
                // Блок не дал события (неизвестный тип или не хватило обязательных данных) — он
                // пропущен. Фиксируем как Warning, чтобы это попало в SkippedBlocks и не терялось тихо.
                AddWarning(warnings, options, new ParsingWarning
                {
                    Severity = WarningSeverity.Warning,
                    Message = "Block skipped: no event produced (unknown type or missing data)",
                    LineNumber = block.StartLine,
                    BlockContent = string.Join("\n", block.BodyLines.Take(3))
                });
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Строгий режим трактует сбой разбора как ошибку (HasErrors), мягкий — как предупреждение.
            // Фатальные исключения (OOM и т.п.) не глотаются - фильтр пропускает их наверх (issue #44).
            var severity = options.ValidationMode == ValidationMode.Strict
                ? WarningSeverity.Error
                : WarningSeverity.Warning;

            AddWarning(warnings, options, new ParsingWarning
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