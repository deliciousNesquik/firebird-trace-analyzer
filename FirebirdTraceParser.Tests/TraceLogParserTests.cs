using System.Text;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.Results;
using FirebirdTraceParser.Parsing.Engine;
using static FirebirdTraceParser.Tests.TestSupport;

namespace FirebirdTraceParser.Tests;

public sealed class TraceLogParserTests
{
    private static TraceLogParser NewParser() => new(Rules, NewHandler(), Logger);

    private static string CleanTrace()
    {
        var sb = new StringBuilder();
        sb.AppendLine(HeaderLine("TRACE_INIT"));
        sb.AppendLine(SessionLine);
        sb.AppendLine(HeaderLine("EXECUTE_STATEMENT_START"));
        foreach (var l in StatementBody(false)) sb.AppendLine(l);
        sb.AppendLine(HeaderLine("EXECUTE_STATEMENT_FINISH"));
        foreach (var l in StatementBody(true)) sb.AppendLine(l);
        return sb.ToString();
    }

    private static string TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ftp_trace_{Guid.NewGuid():N}.log");
        File.WriteAllText(path, content);
        return path;
    }

    // ------------------------------------------------- per-call options (issue #32)

    private static string StatementFinishTraceWithPerfTable()
    {
        var sb = new StringBuilder();
        sb.AppendLine(HeaderLine("EXECUTE_STATEMENT_FINISH"));
        foreach (var l in StatementFinishBodyWithPerfTable()) sb.AppendLine(l);
        return sb.ToString();
    }

    [Fact]
    public void Trailing_junk_after_perf_table_does_not_create_phantom_rows()
    {
        // Парсер фильтрует пустые строки до хендлера, поэтому терминатор таблицы "пустая
        // строка" не срабатывает - и мусор после таблицы попадал в неё фантомной записью
        // с нулевыми счётчиками (issue #39). Таблица обязана заканчиваться на первой
        // строке, не являющейся строкой таблицы.
        var trace = StatementFinishTraceWithPerfTable() + "some trailing text line\n";
        var result = NewParser().Parse(new StringReader(trace));
        var f = Assert.IsType<StatementFinishEvent>(Assert.Single(result.Events));
        Assert.NotNull(f.PerformanceTable);
        Assert.Single(f.PerformanceTable!.Items); // только RDB$INDICES
        Assert.DoesNotContain(f.PerformanceTable.Items, i => i.TableName.Contains("trailing"));
    }

    [Fact]
    public void Per_call_option_disables_performance_table_parsing()
    {
        // Per-call ParseOptions должен доходить до обработчика так же, как DI-опции (issue #32).
        var parser = new TraceLogParser(Rules, NewHandler(), Logger);
        var result = parser.Parse(new StringReader(StatementFinishTraceWithPerfTable()),
            new ParseOptions { ParsePerformanceTables = false });
        var f = Assert.IsType<StatementFinishEvent>(Assert.Single(result.Events));
        Assert.Null(f.PerformanceTable); // RED: опция до хендлера не доходит, таблица парсится
        Assert.NotNull(f.Performance);   // сами тайминги при этом парсятся
    }

    // ------------------------------------------------- timestamp warning (issue #37)

    [Fact]
    public void Malformed_timestamp_emits_warning_but_event_still_returned()
    {
        // Битый ts: событие возвращается с MinValue (закреплено MalformedTimestamp_FallsBackToDefault),
        // но потеря метки времени обязана быть видна как ParsingWarning (issue #37).
        var trace = "2026-13-45T99:99:99.9999 (607408:0x7f2cbe321dc0) EXECUTE_STATEMENT_START\n"
                    + string.Join("\n", StatementBody(false));
        var result = NewParser().Parse(new StringReader(trace));
        Assert.False(result.HasErrors); // деградация данных, не ошибка разбора
        Assert.Contains(result.Warnings, w => w.Message.Contains("timestamp", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------- options validation (issue #40)

    [Fact]
    public void Degenerate_options_throw_at_entry_not_mid_parse()
    {
        // fail-fast на конфигурации (issue #40): мусорные опции ловятся на входе,
        // а не превращаются в ноль событий со спамом предупреждений.
        var parser = NewParser();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            parser.Parse(new StringReader(CleanTrace()), new ParseOptions { MaxBlockLines = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            parser.Parse(new StringReader(CleanTrace()), new ParseOptions { BatchSize = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            parser.Parse(new StringReader(CleanTrace()), new ParseOptions { RegexTimeout = TimeSpan.Zero }));
    }

    // ------------------------------------------------- stream ownership (issue #36)

    [Fact]
    public async Task ParseStreamAsync_leaves_caller_stream_open_after_completion()
    {
        // Контракт владения (issue #36): поток принадлежит вызывающему - парсер его не закрывает.
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(CleanTrace()));
        await foreach (var _ in NewParser().ParseStreamAsync(stream)) { }
        Assert.True(stream.CanRead); // RED: сегодня StreamReader закрывает чужой поток
    }

    [Fact]
    public async Task ParseStreamAsync_leaves_caller_stream_open_on_early_break()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(CleanTrace()));
        await foreach (var _ in NewParser().ParseStreamAsync(stream))
        {
            break; // ранний выход из await foreach - самое частое использование стриминга
        }
        Assert.True(stream.CanRead);
    }

    // ------------------------------------------------- progress (issue #35)

    private sealed class SyncProgress(Action<double> action) : IProgress<double>
    {
        public void Report(double value) => action(value);
    }

    [Fact]
    public async Task ParseStreamAsync_reports_progress_ending_at_one()
    {
        var reports = new List<double>();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(CleanTrace()));
        await foreach (var _ in NewParser().ParseStreamAsync(stream, new SyncProgress(reports.Add))) { }

        // RED (issue #35): параметр progress сегодня игнорируется - ни одного отчёта.
        Assert.NotEmpty(reports);
        Assert.Equal(1.0, reports[^1]);
        Assert.Equal(reports, reports.OrderBy(v => v).ToList()); // монотонно неубывающая
    }

    // ------------------------------------------------- warnings observer (issue #34)

    private sealed class TestObserver : IObserver<ParsingWarning>
    {
        public List<ParsingWarning> Warnings { get; } = new();
        public bool Completed { get; private set; }
        public void OnNext(ParsingWarning value) => Warnings.Add(value);
        public void OnError(Exception error) => throw new InvalidOperationException(
            "OnError не ожидается: ошибки данных деградируют в ParsingWarning");
        public void OnCompleted() => Completed = true;
    }

    [Fact]
    public async Task ParseStreamAsync_notifies_observer_with_same_warnings_as_file_parse()
    {
        // Два блока без события (RESTART без обработчика; statement без аттача) -> два skipped-warning.
        var trace = string.Join("\n",
            HeaderLine("TRACE_INIT"), SessionLine,
            HeaderLine("EXECUTE_PROCEDURE_RESTART"), AttachmentLine, ProcessLine,
            HeaderLine("EXECUTE_STATEMENT_START"), "Statement body line");

        var observer = new TestObserver();
        var options = new ParseOptions { WarningObserver = observer };
        await foreach (var _ in NewParser().ParseStreamAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(trace)), null, default, options)) { }

        // RED (issue #34): стриминговый потребитель не получает диагностику.
        var fileResult = NewParser().Parse(new StringReader(trace));
        Assert.NotEmpty(fileResult.Warnings);
        Assert.Equal(fileResult.Warnings.Count, observer.Warnings.Count);
        Assert.All(observer.Warnings, w =>
            Assert.Contains(fileResult.Warnings, fw => fw.LineNumber == w.LineNumber && fw.Message == w.Message));
        Assert.True(observer.Completed);
    }

    [Fact]
    public void ParseFile_CleanTrace_ThreeEventsNoErrors()
    {
        var path = TempFile(CleanTrace());
        try
        {
            var result = NewParser().ParseFile(path);
            Assert.Equal(3, result.Events.Count);
            Assert.False(result.HasErrors);
            Assert.IsType<TraceInitEvent>(result.Events[0]);
            Assert.IsType<StatementStartEvent>(result.Events[1]);
            Assert.IsType<StatementFinishEvent>(result.Events[2]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ParseStreamAsync_YieldsSameEventCount()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CleanTrace()));
        var events = new List<EventBase>();
        await foreach (var e in NewParser().ParseStreamAsync(stream))
            events.Add(e);
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public void SkippedBlock_RecordedAsWarning()
    {
        // Заголовок есть, но тело без атача → событие не построено → блок пропущен как Warning.
        var trace = HeaderLine("EXECUTE_STATEMENT_START") + "\n" + TransactionLine + "\n";
        var path = TempFile(trace);
        try
        {
            var result = NewParser().ParseFile(path);
            Assert.Empty(result.Events);
            Assert.True(result.SkippedBlocks >= 1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_TextReader_CleanTrace_NoFileNeeded()
    {
        var result = NewParser().Parse(new StringReader(CleanTrace()));
        Assert.Equal(3, result.Events.Count);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Parse_TextReader_SkippedBlock_Warns()
    {
        var result = NewParser().Parse(new StringReader(HeaderLine("EXECUTE_STATEMENT_START") + "\n" + TransactionLine + "\n"));
        Assert.Empty(result.Events);
        Assert.True(result.SkippedBlocks >= 1);
    }

    [Fact]
    public void Parse_NullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NewParser().Parse(null!));
    }

    // ---------------------------------------------------------------- edge / absurd

    [Fact]
    public void EmptyFile_NoEventsNoThrow()
    {
        var path = TempFile("");
        try
        {
            var result = NewParser().ParseFile(path);
            Assert.Empty(result.Events);
            Assert.Empty(result.Warnings);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OnlyWhitespaceLines_NoEvents()
    {
        var path = TempFile("   \n\t\n \n\r\n");
        try
        {
            Assert.Empty(NewParser().ParseFile(path).Events);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GarbageWithoutHeader_NoEventsNoThrow()
    {
        var path = TempFile("this is not a trace\nneither is this\n42\n");
        try
        {
            var result = NewParser().ParseFile(path);
            Assert.Empty(result.Events);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TruncatedLastBlock_StillFlushed()
    {
        // Файл обрывается на середине последнего блока — он всё равно должен быть обработан.
        var sb = new StringBuilder();
        sb.AppendLine(HeaderLine("ATTACH_DATABASE"));
        sb.AppendLine(AttachmentLine); // без завершающего процесса/newline-хвоста
        var path = TempFile(sb.ToString());
        try
        {
            var result = NewParser().ParseFile(path);
            Assert.Single(result.Events);
            Assert.IsType<AttachDatabaseEvent>(result.Events[0]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Cancellation_Honored()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CleanTrace()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in NewParser().ParseStreamAsync(stream, cancellationToken: cts.Token)) { }
        });
    }

    [Fact]
    public void CrlfLineEndings_ParsedLikeLf()
    {
        var path = TempFile(CleanTrace().Replace("\n", "\r\n"));
        try
        {
            Assert.Equal(3, NewParser().ParseFile(path).Events.Count);
        }
        finally { File.Delete(path); }
    }
}
