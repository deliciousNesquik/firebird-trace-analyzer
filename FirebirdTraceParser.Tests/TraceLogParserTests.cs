using System.Text;
using FirebirdTraceParser.Models.Events;
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
