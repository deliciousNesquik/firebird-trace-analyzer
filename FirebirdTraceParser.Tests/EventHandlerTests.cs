using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using static FirebirdTraceParser.Tests.TestSupport;

namespace FirebirdTraceParser.Tests;

public sealed class EventHandlerTests
{
    // ---------------------------------------------------------------- happy path

    [Fact]
    public void StatementStart_Parsed()
    {
        var evt = NewHandler().Handle(Header(HeaderLine("EXECUTE_STATEMENT_START")), StatementBody(false), Rules, NewContext());
        var s = Assert.IsType<StatementStartEvent>(evt);
        Assert.Equal(EventType.ExecuteStatementStart, s.EventType);
        Assert.Equal("SELECT * FROM USERS WHERE ID = ?", s.Sql);
        Assert.Equal(556761380L, s.StatementId);
        Assert.Equal(11335646L, s.Attachment.AttachmentId);
        Assert.Single(s.Parameters);
        Assert.Equal("195", s.Parameters[0].Value);
        Assert.Equal(2026, s.Timestamp.Year);
    }

    [Fact]
    public void StatementFinish_ParsesPerformance()
    {
        var evt = NewHandler().Handle(Header(HeaderLine("EXECUTE_STATEMENT_FINISH")), StatementBody(true), Rules, NewContext());
        var f = Assert.IsType<StatementFinishEvent>(evt);
        Assert.Equal(377, f.Performance.ExecuteMs);
        Assert.Equal(6, f.Performance.ReadCount);
        Assert.Equal(469, f.Performance.WriteCount);
        Assert.Equal(1440, f.Performance.MarkCount);
        Assert.Equal(6, f.Performance.FetchCount);
    }

    [Fact]
    public void FailedStatementFinish_Parsed()
    {
        var evt = NewHandler().Handle(Header(HeaderLine("FAILED EXECUTE_STATEMENT_FINISH")), StatementBody(true), Rules, NewContext());
        Assert.IsType<FailedStatementFinishEvent>(evt);
    }

    [Fact]
    public void TraceInit_Parsed()
    {
        var evt = NewHandler().Handle(Header(HeaderLine("TRACE_INIT")), TraceInitBody(), Rules, NewContext());
        var t = Assert.IsType<TraceInitEvent>(evt);
        Assert.Equal(994, t.Session.SessionId);
    }

    [Fact]
    public void AttachDatabase_Parsed()
    {
        var evt = NewHandler().Handle(Header(HeaderLine("ATTACH_DATABASE")), AttachBody(), Rules, NewContext());
        var a = Assert.IsType<AttachDatabaseEvent>(evt);
        Assert.Equal("/interbas/reid_2022.gdb", a.Attachment.DatabasePath);
        Assert.Equal("REPL", a.Attachment.User);
        Assert.Equal("WIN1251", a.Attachment.Charset);
        Assert.Equal(52931, a.Attachment.Port);
    }

    [Fact]
    public void ProcedureStart_Parsed()
    {
        var evt = NewHandler().Handle(Header(HeaderLine("EXECUTE_PROCEDURE_START")), ProcedureBody(), Rules, NewContext());
        var p = Assert.IsType<ProcedureStartEvent>(evt);
        Assert.Equal("SP_GET_USER", p.ProcedureName);
    }

    [Fact]
    public void TriggerStart_DmlParsed()
    {
        var evt = NewHandler().Handle(Header(HeaderLine("EXECUTE_TRIGGER_START")), TriggerBody(), Rules, NewContext());
        var tr = Assert.IsType<TriggerStartEvent>(evt);
        Assert.Equal("USERS_BI", tr.TriggerName);
        Assert.Equal("USERS", tr.Table);
        Assert.Equal("BEFORE", tr.Timing);
        Assert.Equal("INSERT", tr.Event);
    }

    [Fact]
    public void Error_ParsesComponentAndChain()
    {
        var evt = NewHandler().Handle(Header(HeaderLine("ERROR AT JResultSet::fetchNext")), ErrorBody(), Rules, NewContext());
        var e = Assert.IsType<ErrorEvent>(evt);
        Assert.Equal("JResultSet::fetchNext", e.Component);
        Assert.Single(e.Errors);
        Assert.Equal(335544364, e.Errors[0].ErrorCode);
        Assert.Equal("request synchronization error", e.Errors[0].Message);
    }

    [Fact]
    public void Error_PositionalGroups_BackwardCompatFallback()
    {
        // Уже развёрнутый rules.json с безымянными группами (?<code>/?<message> отсутствуют) —
        // хендлер обязан откатиться на позиционные 1/2 и корректно разобрать код/сообщение.
        var rules = new Dictionary<string, System.Text.RegularExpressions.Regex>(Rules)
        {
            ["error_line"] = new(@"^(\d+)\s*:\s*(.*)$",
                System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1))
        };
        var evt = NewHandler().Handle(Header(HeaderLine("ERROR AT X::y")), ErrorBody(), rules, NewContext());
        var e = Assert.IsType<ErrorEvent>(evt);
        Assert.Equal(335544364, e.Errors[0].ErrorCode);
        Assert.Equal("request synchronization error", e.Errors[0].Message);
    }

    [Fact]
    public void Transaction_IsolationParsed()
    {
        var evt = NewHandler().Handle(Header(HeaderLine("EXECUTE_STATEMENT_START")), StatementBody(false), Rules, NewContext());
        var s = Assert.IsType<StatementStartEvent>(evt);
        Assert.NotNull(s.Transaction);
        Assert.Equal("READ_COMMITTED", s.Transaction!.IsolationLevel);
        Assert.Equal("NOWAIT", s.Transaction.LockMode);
        Assert.Equal("READ_WRITE", s.Transaction.AccessMode);
        Assert.Equal("READ_CONSISTENCY", s.Transaction.ConsistencyMode);
    }

    [Fact]
    public void Transaction_SnapshotWaitReadOnly_Parsed()
    {
        List<string> body =
        [
            AttachmentLine, ProcessLine, "(TRA_100, SNAPSHOT | WAIT | READ_ONLY)",
            "Statement 1:", SqlDashes, "SELECT 1"
        ];
        var s = Assert.IsType<StatementStartEvent>(
            NewHandler().Handle(Header(HeaderLine("EXECUTE_STATEMENT_START")), body, Rules, NewContext()));
        Assert.Equal("SNAPSHOT", s.Transaction!.IsolationLevel);
        Assert.Equal("WAIT", s.Transaction.LockMode);
        Assert.Equal("READ_ONLY", s.Transaction.AccessMode);
        Assert.Equal("NONE", s.Transaction.ConsistencyMode); // не задан → дефолт
    }

    // ---------------------------------------------------------------- edge / absurd

    [Fact]
    public void UnknownButHeaderMatched_EventType_ReturnsNull()
    {
        // EXECUTE_PROCEDURE_RESTART распознаётся block_header'ом, но не имеет обработчика → null (не падение).
        Assert.Null(NewHandler().Handle(Header(HeaderLine("EXECUTE_PROCEDURE_RESTART")), AttachBody(), Rules, NewContext()));
    }

    [Fact]
    public void StatementWithoutAttachment_ReturnsNull()
    {
        Assert.Null(NewHandler().Handle(Header(HeaderLine("EXECUTE_STATEMENT_START")),
            [TransactionLine, "Statement 1:"], Rules, NewContext()));
    }

    [Fact]
    public void EmptyBody_ReturnsNull()
    {
        Assert.Null(NewHandler().Handle(Header(HeaderLine("EXECUTE_STATEMENT_START")), [], Rules, NewContext()));
    }

    [Fact]
    public void MalformedTimestamp_FallsBackToDefault()
    {
        // block_header принимает синтаксически-похожий, но невалидный ts; TryParse → default(DateTime).
        var badHeader = Rules["block_header"].Match(
            "2026-13-45T99:99:99.9999 (607408:0x7f2cbe321dc0) EXECUTE_STATEMENT_START");
        Assert.True(badHeader.Success);
        var evt = NewHandler().Handle(badHeader, StatementBody(false), Rules, NewContext());
        Assert.NotNull(evt);
        Assert.Equal(default, evt!.Timestamp);
    }

    [Fact]
    public void OverflowingStatementId_FallsBackToZero()
    {
        List<string> body =
        [
            AttachmentLine, ProcessLine, TransactionLine,
            "Statement 99999999999999999999999999:", SqlDashes, "SELECT 1"
        ];
        var evt = NewHandler().Handle(Header(HeaderLine("EXECUTE_STATEMENT_START")), body, Rules, NewContext());
        var s = Assert.IsType<StatementStartEvent>(evt);
        Assert.Equal(0L, s.StatementId); // GetGroupLong не смог распарсить → default, но не исключение
    }

    [Fact]
    public void GiantSqlBlock_DoesNotThrow()
    {
        var body = new List<string> { AttachmentLine, ProcessLine, TransactionLine, "Statement 1:", SqlDashes };
        for (var i = 0; i < 50_000; i++) body.Add($"-- comment line {i}");
        var evt = NewHandler().Handle(Header(HeaderLine("EXECUTE_STATEMENT_START")), body, Rules, NewContext());
        var s = Assert.IsType<StatementStartEvent>(evt);
        Assert.Contains("comment line 49999", s.Sql);
    }

    [Fact]
    public void UnicodeAndControlChars_InParamValue_Preserved()
    {
        List<string> body =
        [
            AttachmentLine, ProcessLine, TransactionLine, "Statement 1:", SqlDashes, "SELECT 1",
            "param0 = varchar, \"Ünïcödé — Ⅷ 中文 �\""
        ];
        var evt = NewHandler().Handle(Header(HeaderLine("EXECUTE_STATEMENT_START")), body, Rules, NewContext());
        var s = Assert.IsType<StatementStartEvent>(evt);
        Assert.Equal("Ünïcödé — Ⅷ 中文 �", s.Parameters[0].Value);
    }
}
