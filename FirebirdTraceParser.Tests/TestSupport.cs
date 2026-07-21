using System.Text.RegularExpressions;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Parsing.Handlers;
using FirebirdTraceParser.Parsing.Rules;
using FirebirdTraceParser.Parsing.Utils;
using Microsoft.Extensions.Caching.Memory;
using NLog;

namespace FirebirdTraceParser.Tests;

/// <summary>
/// Общая инфраструктура тестов: реальные правила из поставляемого rules.json, логгер-заглушка,
/// хендлер и фабрики блоков/тел событий, отформатированных строго по паттернам rules.json.
/// </summary>
internal static class TestSupport
{
    public static readonly ILogger Logger = LogManager.GetLogger("tests");

    /// <summary>rules.json, скопированный в выходной каталог теста (линк на файл приложения).</summary>
    public static string RulesPath => Path.Combine(AppContext.BaseDirectory, "rules.json");

    private static readonly Lazy<IReadOnlyDictionary<string, Regex>> LazyRules = new(() =>
    {
        var loader = new JsonRuleLoader(new MemoryCache(new MemoryCacheOptions()), Logger);
        return loader.LoadRules(RulesPath);
    });

    public static IReadOnlyDictionary<string, Regex> Rules => LazyRules.Value;

    public static DefaultEventHandler NewHandler() => new(Logger, new PerformanceTableParser(Logger));

    public static ParsingContext NewContext() => new();

    /// <summary>Собирает Match заголовка блока по строке-заголовку.</summary>
    public static Match Header(string headerLine) => Rules["block_header"].Match(headerLine);

    // ---- строки-заголовки ----
    public const string Ts = "2026-06-01T11:19:04.1720";
    public const string TracePrefix = Ts + " (607408:0x7f2cbe321dc0) ";

    public static string HeaderLine(string eventType) => TracePrefix + eventType;

    // ---- фрагменты тела (строго по sample из rules.json) ----
    public const string AttachmentLine =
        "\t/interbas/reid_2022.gdb (ATT_11335646, REPL:NONE, WIN1251, TCPv4:192.168.3.218/52931)";

    public const string ProcessLine = "\tC:\\Python310-32\\python.exe:2540";

    public const string TransactionLine =
        "(TRA_48170828, INIT_48170682, READ_COMMITTED | READ_CONSISTENCY | NOWAIT | READ_WRITE)";

    public const string SessionLine = "  SESSION_994";
    public const string SqlDashes = "-------------------------------------------------------------------------------";
    public const string PerformanceLine = "377 ms, 6 read(s), 469 write(s), 1446 fetch(es), 1440 mark(s)";
    public const string FetchedLine = "6 records fetched";

    /// <summary>Тело события statement (start/finish) с атачем, транзакцией, SQL и параметром.</summary>
    public static List<string> StatementBody(bool withPerformance) =>
        withPerformance
            ?
            [
                AttachmentLine, ProcessLine, TransactionLine,
                "Statement 556761380:", SqlDashes, "SELECT * FROM USERS WHERE ID = ?",
                "param0 = bigint, \"195\"", FetchedLine, PerformanceLine
            ]
            :
            [
                AttachmentLine, ProcessLine, TransactionLine,
                "Statement 556761380:", SqlDashes, "SELECT * FROM USERS WHERE ID = ?",
                "param0 = bigint, \"195\""
            ];

    public static List<string> ProcedureBody() =>
    [
        AttachmentLine, ProcessLine, TransactionLine, "Procedure SP_GET_USER:",
        "param0 = bigint, \"195\"", PerformanceLine
    ];

    public static List<string> TriggerBody() =>
    [
        AttachmentLine, ProcessLine, TransactionLine,
        "Trigger USERS_BI FOR USERS (BEFORE INSERT):", PerformanceLine
    ];

    public static List<string> ErrorBody() =>
    [
        AttachmentLine, ProcessLine, "335544364 : request synchronization error"
    ];

    public static List<string> AttachBody() => [AttachmentLine, ProcessLine];
    public static List<string> TraceInitBody() => [SessionLine];
}
