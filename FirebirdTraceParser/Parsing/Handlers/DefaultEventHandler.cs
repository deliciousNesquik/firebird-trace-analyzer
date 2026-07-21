using System.Globalization;
using System.Text.RegularExpressions;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;
using FirebirdTraceParser.Parsing.Engine;
using FirebirdTraceParser.Parsing.Rules;
using FirebirdTraceParser.Parsing.Utils;
using NLog;

namespace FirebirdTraceParser.Parsing.Handlers;

/// <summary> Обработчик событий по умолчанию. </summary>
public sealed class DefaultEventHandler(
    ILogger logger,
    IPerformanceTableParser performanceTableParser,
    ParseOptions? options = null) : IEventHandler
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IPerformanceTableParser _performanceTableParser =
        performanceTableParser ?? throw new ArgumentNullException(nameof(performanceTableParser));
    private readonly ParseOptions _options = options ?? ParseOptions.Default;

    private static readonly Dictionary<string, EventType> EventTypeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TRACE_INIT"] = EventType.TraceInit,
        ["TRACE_FINI"] = EventType.TraceFinish,
        ["ATTACH_DATABASE"] = EventType.AttachDatabase,
        ["DETACH_DATABASE"] = EventType.DetachDatabase,
        ["EXECUTE_STATEMENT_START"] = EventType.ExecuteStatementStart,
        ["EXECUTE_STATEMENT_RESTART"] = EventType.ExecuteStatementRestart,
        ["EXECUTE_STATEMENT_FINISH"] = EventType.ExecuteStatementFinish,
        ["EXECUTE_PROCEDURE_START"] = EventType.ExecuteProcedureStart,
        ["EXECUTE_PROCEDURE_FINISH"] = EventType.ExecuteProcedureFinish,
        ["EXECUTE_TRIGGER_START"] = EventType.ExecuteTriggerStart,
        ["EXECUTE_TRIGGER_FINISH"] = EventType.ExecuteTriggerFinish,
        ["FAILED EXECUTE_STATEMENT_FINISH"] = EventType.FailedExecuteStatementFinish,
        ["FAILED EXECUTE_PROCEDURE_FINISH"] = EventType.FailedExecuteProcedureFinish,
        ["FAILED EXECUTE_TRIGGER_FINISH"] = EventType.FailedExecuteTriggerFinish
    };

    /// <summary>Обработчик тела блока конкретного типа события.</summary>
    private delegate EventBase? EventFactory(Match header, IReadOnlyList<string> body,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context);

    // Диспетчеризация таблицей вместо switch: новый тип = одна запись, ветвление кода не трогаем.
    // Ленивая инициализация (ссылки на методы экземпляра нельзя в инициализаторе поля); идемпотентна.
    private Dictionary<EventType, EventFactory>? _dispatch;

    private Dictionary<EventType, EventFactory> BuildDispatch() => new()
    {
        [EventType.TraceInit] = HandleTraceInit,
        [EventType.TraceFinish] = HandleTraceFinish,
        [EventType.AttachDatabase] = HandleAttach,
        [EventType.DetachDatabase] = HandleDetach,
        [EventType.ExecuteStatementStart] = HandleStatementStart,
        [EventType.ExecuteStatementRestart] = HandleStatementRestart,
        [EventType.ExecuteStatementFinish] = HandleStatementFinish,
        [EventType.ExecuteProcedureStart] = HandleProcedureStart,
        [EventType.ExecuteProcedureFinish] = HandleProcedureFinish,
        [EventType.ExecuteTriggerStart] = HandleTriggerStart,
        [EventType.ExecuteTriggerFinish] = HandleTriggerFinish,
        [EventType.FailedExecuteStatementFinish] = HandleFailedStatementFinish,
        [EventType.FailedExecuteProcedureFinish] = HandleFailedProcedureFinish,
        [EventType.FailedExecuteTriggerFinish] = HandleFailedTriggerFinish
    };

    public EventBase? Handle(Match blockHeader, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var eventTypeStr = blockHeader.GetGroupValue("event_type");

        if (eventTypeStr.StartsWith("ERROR AT ", StringComparison.OrdinalIgnoreCase))
            return HandleError(blockHeader, bodyLines, rules, context);

        if (!EventTypeMapping.TryGetValue(eventTypeStr, out var eventType))
        {
            _logger.Warn("Unknown event type for parser: '{EventType}'", eventTypeStr);
            return null;
        }

        _dispatch ??= BuildDispatch();
        var result = _dispatch.TryGetValue(eventType, out var factory)
            ? factory(blockHeader, bodyLines, rules, context)
            : null;

        if (result == null)
            _logger.Warn("Handler returned null for {EventType}", eventType);

        return result;
    }

    // ==================== Common Event Metadata Parsing ====================

    /// <summary>
    ///     Извлекает общие метаданные заголовка события (timestamp, trace_id, hex_trace_id).
    /// </summary>
    private static (DateTime Timestamp, int TraceId, string HexTraceId) ParseEventMetadata(Match header,
        ParsingContext context)
    {
        // ValueSpan — без аллокации строки на каждое событие (пустой span у несовпавшей группы → default).
        var timestamp = DateTime.TryParse(header.Groups["ts"].ValueSpan, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var ts) ? ts : default;

        return (
            Timestamp: timestamp,
            TraceId: header.GetGroupInt("trace_id"),
            HexTraceId: context.Intern(header.GetGroupValue("hex_trace_id"))
        );
    }

    /// <summary>
    ///     Создает PerformanceInfo по умолчанию (все нули).
    /// </summary>
    private static PerformanceInfo CreateDefaultPerformance()
    {
        return new PerformanceInfo
        {
            ExecuteMs = 0,
            FetchCount = 0,
            ReadCount = 0,
            WriteCount = 0,
            MarkCount = 0
        };
    }

    // ==================== Handlers ====================

    private TraceInitEvent? HandleTraceInit(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var session = ParseSessionInfo(bodyLines, rules, context);
        if (session is null) return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new TraceInitEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.TraceInit,
            Session = session
        };
    }

    private TraceFinishEvent? HandleTraceFinish(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var session = ParseSessionInfo(bodyLines, rules, context);
        if (session is null) return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new TraceFinishEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.TraceFinish,
            Session = session
        };
    }

    private AttachDatabaseEvent? HandleAttach(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var attachment = ParseAttachmentInfo(bodyLines, rules, context);
        if (attachment is null) return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new AttachDatabaseEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.AttachDatabase,
            Attachment = attachment
        };
    }

    private DetachDatabaseEvent? HandleDetach(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var attachment = ParseAttachmentInfo(bodyLines, rules, context);
        if (attachment is null) return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new DetachDatabaseEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.DetachDatabase,
            Attachment = attachment
        };
    }

    private StatementStartEvent? HandleStatementStart(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseStatementData(bodyLines, rules, false, false, context);
        if (data.Attachment is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new StatementStartEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.ExecuteStatementStart,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            StatementId = data.StatementId,
            Sql = data.Sql ?? string.Empty,
            Parameters = data.Params
        };
    }

    private StatementRestartEvent? HandleStatementRestart(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseStatementData(bodyLines, rules, false, true, context);
        if (data.Attachment is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new StatementRestartEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.ExecuteStatementRestart,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            StatementId = data.StatementId,
            RestartCount = data.RestartCount,
            Sql = data.Sql ?? string.Empty,
            Parameters = data.Params
        };
    }

    private StatementFinishEvent? HandleStatementFinish(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseStatementData(bodyLines, rules, true, true, context);
        if (data.Attachment is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new StatementFinishEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.ExecuteStatementFinish,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            StatementId = data.StatementId,
            Sql = data.Sql ?? string.Empty,
            Parameters = data.Params,
            Performance = data.Performance ?? CreateDefaultPerformance(),
            PerformanceTable = _options.ParsePerformanceTables ? data.PerformanceTable : null
        };
    }

    private FailedStatementFinishEvent? HandleFailedStatementFinish(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseStatementData(bodyLines, rules, true, true, context);
        if (data.Attachment is null || data.Transaction is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new FailedStatementFinishEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.FailedExecuteStatementFinish,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            StatementId = data.StatementId,
            Sql = data.Sql ?? string.Empty,
            Parameters = data.Params,
            Performance = data.Performance ?? CreateDefaultPerformance(),
            PerformanceTable = _options.ParsePerformanceTables ? data.PerformanceTable : null
        };
    }

    private ProcedureStartEvent? HandleProcedureStart(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseProcedureData(bodyLines, rules, false, context);
        if (data.Attachment is null || data.Transaction is null || data.ProcedureName is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new ProcedureStartEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.ExecuteProcedureStart,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            ProcedureName = data.ProcedureName,
            Parameters = data.Params
        };
    }

    private ProcedureFinishEvent? HandleProcedureFinish(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseProcedureData(bodyLines, rules, true, context);
        if (data.Attachment is null || data.Transaction is null || data.ProcedureName is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new ProcedureFinishEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.ExecuteProcedureFinish,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            ProcedureName = data.ProcedureName,
            Parameters = data.Params,
            Performance = data.Performance ?? CreateDefaultPerformance(),
            PerformanceTable = _options.ParsePerformanceTables ? data.PerformanceTable : null
        };
    }

    private FailedProcedureFinishEvent? HandleFailedProcedureFinish(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseProcedureData(bodyLines, rules, true, context);
        if (data.Attachment is null || data.Transaction is null || data.ProcedureName is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new FailedProcedureFinishEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.FailedExecuteProcedureFinish,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            ProcedureName = data.ProcedureName,
            Parameters = data.Params,
            Performance = data.Performance ?? CreateDefaultPerformance(),
            PerformanceTable = _options.ParsePerformanceTables ? data.PerformanceTable : null
        };
    }

    private TriggerStartEvent? HandleTriggerStart(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseTriggerData(bodyLines, rules, false, context);

        if (data.Attachment is null || data.Transaction is null || data.TriggerName is null || data.Event is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new TriggerStartEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.ExecuteTriggerStart,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            TriggerName = data.TriggerName,
            Table = data.Table,
            Timing = data.Timing,
            Event = data.Event
        };
    }

    private TriggerFinishEvent? HandleTriggerFinish(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseTriggerData(bodyLines, rules, true, context);

        if (data.Attachment is null || data.Transaction is null || data.TriggerName is null || data.Event is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new TriggerFinishEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.ExecuteTriggerFinish,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            TriggerName = data.TriggerName,
            Table = data.Table,
            Timing = data.Timing,
            Event = data.Event,
            Performance = data.Performance ?? CreateDefaultPerformance(),
            PerformanceTable = _options.ParsePerformanceTables ? data.PerformanceTable : null
        };
    }

    private FailedTriggerFinishEvent? HandleFailedTriggerFinish(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var data = ParseTriggerData(bodyLines, rules, true, context);

        if (data.Attachment is null || data.Transaction is null || data.TriggerName is null || data.Event is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new FailedTriggerFinishEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.FailedExecuteTriggerFinish,
            Attachment = data.Attachment,
            Transaction = data.Transaction,
            TriggerName = data.TriggerName,
            Table = data.Table,
            Timing = data.Timing,
            Event = data.Event,
            Performance = data.Performance ?? CreateDefaultPerformance(),
            PerformanceTable = _options.ParsePerformanceTables ? data.PerformanceTable : null
        };
    }

    private ErrorEvent? HandleError(Match header, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var attachment = ParseAttachmentInfo(bodyLines, rules, context);
        if (attachment is null) return null;

        // Извлекаем компонент из "ERROR AT <компонент>"
        var eventTypeStr = header.GetGroupValue("event_type");
        var component = eventTypeStr["ERROR AT ".Length..].Trim();

        // Парсим цепочку ошибок
        var errors = ParseErrorChain(bodyLines, rules, context);

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header, context);

        return new ErrorEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.Error,
            Attachment = attachment,
            Component = context.Intern(component),
            Errors = errors
        };
    }

    // ==================== Parsing helpers ====================

    private static TraceSessionInfo? ParseSessionInfo(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var sessionRx = rules[RuleKeys.Session];

        foreach (var line in lines)
        {
            var m = sessionRx.Match(line);
            if (m.Success)
            {
                var sessionId = m.GetGroupInt("session_id");
                return context.InternSession(sessionId);
            }
        }

        return null;
    }

    private static AttachmentInfo? ParseAttachmentInfo(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var attachmentRx = rules[RuleKeys.Attachment];
        var processRx = rules[RuleKeys.Process];
        Match? am = null;
        Match? pm = null;

        foreach (var line in lines)
        {
            if (am is null)
            {
                var match = attachmentRx.Match(line);
                if (match.Success) am = match;
            }

            if (pm is null)
            {
                var match = processRx.Match(line);
                if (match.Success) pm = match;
            }

            if (am is not null && pm is not null) break;
        }

        if (am is null) return null;

        var attachmentId = am.GetGroupLong("attachment_id");

        if (context.TryGetAttachment(attachmentId, out var cachedInfo))
            return cachedInfo;

        var newInfo = new AttachmentInfo
        {
            AttachmentId = attachmentId,
            DatabasePath = context.Intern(am.GetGroupValue("database_path")),
            User = context.Intern(am.GetGroupValue("user")),
            Role = context.Intern(am.GetGroupValue("role")),
            Charset = context.Intern(am.GetGroupValue("charset")),
            Protocol = context.Intern(am.GetGroupValue("protocol").Trim()),
            Address = context.Intern(am.GetGroupValue("address", "<internal>")),
            Port = am.GetGroupInt("port"),
            ProcessPath = pm is not null ? context.Intern(pm.GetGroupValue("process_path")) : null,
            ProcessId = pm is not null ? pm.GetGroupInt("process_id") : null
        };

        return context.AddAttachment(newInfo);
    }

    private static TransactionInfo? ParseTransactionInfo(string line,
        IReadOnlyDictionary<string, Regex> rules)
    {
        if (!line.Contains("(TRA_")) return null;

        var m = rules[RuleKeys.Transaction].Match(line);
        if (!m.Success) return null;

        var tid = m.GetGroupLong("transaction_id");

        // Получаем ValueSpan вместо Value (нет аллокации строки для группы)
        var paramsSpan = m.Groups["params"].ValueSpan;

        // Дефолтные значения (строковые литералы уже интернированы самой CLR)
        var isolationLevel = "NONE";
        var consistencyMode = "NONE";
        var lockMode = "NONE";
        var accessMode = "NONE";

        // Классификация токенов таблицей вместо длинной if/else-цепочки. Alternate-lookup по
        // ReadOnlySpan<char> — без аллокации строки на токен. NONE/(NONE) просто нет в таблице.
        var lookup = TxTokens.GetAlternateLookup<ReadOnlySpan<char>>();
        var start = 0;
        while (start < paramsSpan.Length)
        {
            var currentSlice = paramsSpan[start..];
            var nextPipe = currentSlice.IndexOf('|');

            var token = (nextPipe == -1 ? currentSlice : currentSlice[..nextPipe]).Trim();

            if (!token.IsEmpty && lookup.TryGetValue(token, out var map))
            {
                switch (map.Field)
                {
                    case TxField.Isolation: isolationLevel = map.Value; break;
                    case TxField.Consistency: consistencyMode = map.Value; break;
                    case TxField.Lock: lockMode = map.Value; break;
                    case TxField.Access: accessMode = map.Value; break;
                }
            }

            if (nextPipe == -1) break;
            start += nextPipe + 1;
        }

        return new TransactionInfo
        {
            TransactionId = tid,
            IsolationLevel = isolationLevel,
            ConsistencyMode = consistencyMode,
            LockMode = lockMode,
            AccessMode = accessMode
        };
    }

    private enum TxField { Isolation, Consistency, Lock, Access }

    /// <summary>Токен параметров транзакции → (поле, каноническое значение). Расширение — одна строка.</summary>
    private static readonly Dictionary<string, (TxField Field, string Value)> TxTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["READ_COMMITTED"] = (TxField.Isolation, "READ_COMMITTED"),
            ["CONCURRENCY"] = (TxField.Isolation, "CONCURRENCY"),
            ["SNAPSHOT"] = (TxField.Isolation, "SNAPSHOT"),
            ["SNAPSHOT_TABLE_STABILITY"] = (TxField.Isolation, "SNAPSHOT_TABLE_STABILITY"),
            ["READ_CONSISTENCY"] = (TxField.Consistency, "READ_CONSISTENCY"),
            ["NO_RECORD_VERSION"] = (TxField.Consistency, "NO_RECORD_VERSION"),
            ["RECORD_VERSION"] = (TxField.Consistency, "RECORD_VERSION"),
            ["NOWAIT"] = (TxField.Lock, "NOWAIT"),
            ["WAIT"] = (TxField.Lock, "WAIT"),
            ["LOCK_TIMEOUT"] = (TxField.Lock, "LOCK_TIMEOUT"),
            ["READ_WRITE"] = (TxField.Access, "READ_WRITE"),
            ["READ_ONLY"] = (TxField.Access, "READ_ONLY")
        };

    private static IReadOnlyList<ErrorLines> ParseErrorChain(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        List<ErrorLines>? errors = null;
        var errorLineRx = rules[RuleKeys.ErrorLine];

        foreach (var line in lines)
        {
            var match = errorLineRx.Match(line.Trim());
            if (!match.Success) continue;

            // Именованные группы (актуальный rules.json) с откатом на позиционные 1/2 —
            // чтобы уже развёрнутые у пользователей конфиги с безымянными группами не сломались.
            var code = match.Groups["code"].Success ? match.GetGroupInt("code") : match.GetGroupInt("1");
            var message = match.Groups["message"].Success
                ? match.GetGroupValue("message")
                : match.GetGroupValue("2");

            (errors ??= new()).Add(new ErrorLines
            {
                ErrorCode = code,
                Message = context.Intern(message.Trim())
            });
        }

        return errors ?? (IReadOnlyList<ErrorLines>)Array.Empty<ErrorLines>();
    }

    // ==================== Вспомогательные методы для классификации ====================

    /// <summary>
    ///     Парсит одну строку SQL-параметра и возвращает объект SqlParameters или null.
    /// </summary>
    private static SqlParameters? ParseSqlParameter(string line, IReadOnlyDictionary<string, Regex> rules,
        ParsingContext context)
    {
        var paramM = rules[RuleKeys.Parameters].Match(line);
        if (!paramM.Success)
            return null;

        var value = paramM.Groups["value"].Success
            ? paramM.GetGroupValue("value")
            : paramM.GetGroupValue("value_null");

        return new SqlParameters
        {
            Name = context.Intern(paramM.GetGroupValue("name")),
            Dtype = context.Intern(paramM.GetGroupValue("dtype")),
            Value = context.Intern(value)
        };
    }

    // ==================== Data Records ====================

    private sealed record StatementData(
        AttachmentInfo? Attachment,
        TransactionInfo? Transaction,
        long? StatementId,
        string? Sql,
        IReadOnlyList<SqlParameters> Params,
        PerformanceInfo? Performance,
        PerformanceTable? PerformanceTable,
        int? RestartCount);

    private sealed record ProcedureData(
        AttachmentInfo? Attachment,
        TransactionInfo? Transaction,
        string? ProcedureName,
        IReadOnlyList<SqlParameters> Params,
        PerformanceInfo? Performance,
        PerformanceTable? PerformanceTable);

    private sealed record TriggerData(
        AttachmentInfo? Attachment,
        TransactionInfo? Transaction,
        string? TriggerName,
        string? Table,
        string? Timing,
        string? Event,
        PerformanceInfo? Performance,
        PerformanceTable? PerformanceTable);

    // ==================== Complex Parsers ====================

    private StatementData ParseStatementData(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules, bool includePerformance, bool isRestarted, ParsingContext context)
    {
        var attachment = ParseAttachmentInfo(lines, rules, context);
        TransactionInfo? transaction = null;
        long? statementId = null;
        string? sql = null;
        List<SqlParameters>? paramsList = null;
        PerformanceInfo? performance = null;
        PerformanceTable? performanceTable = null;
        var performanceTableSearched = false;
        int? restartCount = null;
        var fetchCount = 0;

        // Кэшируем Regex в локали — убираем словарные лукапы из горячего цикла
        var statementRx = rules[RuleKeys.Statement];
        var restartedRx = rules[RuleKeys.Restarted];
        var parametersRx = rules[RuleKeys.Parameters];
        var fetchedRx = rules[RuleKeys.Fetched];
        var performanceRx = rules[RuleKeys.Performance];

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (transaction is null)
            {
                transaction = ParseTransactionInfo(line, rules);
                if (transaction is not null) continue;
            }

            var sm = statementRx.Match(line);
            if (sm.Success)
            {
                statementId = sm.GetGroupLong("statement_id");
                continue;
            }

            if (isRestarted && restartCount is null)
            {
                var rm = restartedRx.Match(line);
                if (rm.Success)
                {
                    restartCount = rm.GetGroupInt("count");
                    continue;
                }
            }

            // SQL block start
            if (line.Trim().StartsWith("-----"))
            {
                var sqlLines = new List<string>();
                i++;
                while (i < lines.Count)
                {
                    var l = lines[i];

                    if (isRestarted && restartedRx.IsMatch(l))
                    {
                        // Не добавляем эту строку в SQL, она будет обработана на следующей итерации основного цикла
                        i--; // Откатываем индекс, чтобы основной цикл обработал эту строку
                        break;
                    }

                    // break if param or performance/fetched
                    if (parametersRx.IsMatch(l) ||
                        (includePerformance && (fetchedRx.IsMatch(l) || performanceRx.IsMatch(l))))
                    {
                        i--; // вернуть строку основному циклу, иначе первый параметр/perf после SQL теряется
                        break;
                    }

                    sqlLines.Add(l);
                    i++;
                }

                sql = context.Intern(string.Join("\n", sqlLines).Trim());
                continue;
            }

            var param = ParseSqlParameter(line, rules, context);
            if (param is not null)
            {
                (paramsList ??= new()).Add(param);
                continue;
            }

            if (includePerformance)
            {
                // "N records fetched" идёт отдельной строкой перед таймингами — запоминаем.
                var fm = fetchedRx.Match(line);
                if (fm.Success)
                {
                    fetchCount = fm.GetGroupInt("fetch_count");
                    continue;
                }

                if (performance is null)
                {
                    performance = ParsePerformance(line, rules, fetchCount);
                    if (performance is not null) continue;
                }
            }

            if (includePerformance && !performanceTableSearched)
            {
                // Сканируем хвост на таблицу производительности один раз, без копии массива
                performanceTableSearched = true;
                performanceTable = _performanceTableParser.ParsePerformanceTable(lines, i, rules, context);
            }
        }

        return new StatementData(attachment, transaction, statementId, sql,
            paramsList ?? (IReadOnlyList<SqlParameters>)Array.Empty<SqlParameters>(),
            performance, performanceTable, restartCount);
    }

    private ProcedureData ParseProcedureData(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules, bool includePerformance, ParsingContext context)
    {
        var attachment = ParseAttachmentInfo(lines, rules, context);
        TransactionInfo? transaction = null;
        string? procedureName = null;
        var procedureRx = rules[RuleKeys.Procedure];
        var fetchedRx = rules[RuleKeys.Fetched];
        List<SqlParameters>? paramsList = null;
        PerformanceInfo? performance = null;
        PerformanceTable? performanceTable = null;
        var fetchCount = 0;

        foreach (var line in lines)
        {
            if (transaction is null)
            {
                transaction = ParseTransactionInfo(line, rules);
                if (transaction is not null) continue;
            }

            var pmProc = procedureRx.Match(line);
            if (pmProc.Success)
            {
                procedureName = context.Intern(pmProc.GetGroupValue("procedure_name"));
                continue;
            }

            var param = ParseSqlParameter(line, rules, context);
            if (param is not null)
            {
                (paramsList ??= new()).Add(param);
                continue;
            }

            if (includePerformance)
            {
                var fm = fetchedRx.Match(line);
                if (fm.Success)
                {
                    fetchCount = fm.GetGroupInt("fetch_count");
                    continue;
                }

                if (performance is null)
                    performance = ParsePerformance(line, rules, fetchCount);
            }
        }

        if (includePerformance)
            performanceTable = _performanceTableParser.ParsePerformanceTable(lines, 0, rules, context);

        return new ProcedureData(attachment, transaction, procedureName,
            paramsList ?? (IReadOnlyList<SqlParameters>)Array.Empty<SqlParameters>(),
            performance, performanceTable);
    }

    private TriggerData ParseTriggerData(IReadOnlyList<string> lines, IReadOnlyDictionary<string, Regex> rules,
        bool includePerformance, ParsingContext context)
    {
        var attachment = ParseAttachmentInfo(lines, rules, context);
        TransactionInfo? transaction = null;
        string? triggerName = null;
        var triggerRx = rules[RuleKeys.Trigger];
        var fetchedRx = rules[RuleKeys.Fetched];
        string? table = null;
        string? timing = null;
        string? eventType = null;
        PerformanceInfo? performance = null;
        PerformanceTable? performanceTable = null;
        var fetchCount = 0;

        foreach (var line in lines)
        {
            if (transaction is null)
            {
                transaction = ParseTransactionInfo(line, rules);
                if (transaction is not null) continue;
            }

            var tm = triggerRx.Match(line);
            if (tm.Success)
            {
                triggerName = context.Intern(tm.GetGroupValue("trigger_name"));

                // проверяем тип триггера (DML или DDL)
                if (tm.Groups["table"].Success) // DML триггер (FOR table)
                {
                    table = context.Intern(tm.GetGroupValue("table"));
                    timing = context.Intern(tm.GetGroupValue("timing"));
                    eventType = context.Intern(tm.GetGroupValue("event"));
                }
                else if (tm.Groups["ddl_event"].Success) // DDL триггер (ON ...)
                {
                    table = null;
                    timing = null;
                    eventType = context.Intern(tm.GetGroupValue("ddl_event")); // "ON CONNECT", "ON DISCONNECT", etc.
                }

                continue;
            }

            if (includePerformance)
            {
                var fm = fetchedRx.Match(line);
                if (fm.Success)
                {
                    fetchCount = fm.GetGroupInt("fetch_count");
                    continue;
                }

                if (performance is null)
                    performance = ParsePerformance(line, rules, fetchCount);
            }
        }

        if (includePerformance)
            performanceTable = _performanceTableParser.ParsePerformanceTable(lines, 0, rules, context);

        return new TriggerData(attachment, transaction, triggerName, table, timing, eventType, performance,
            performanceTable);
    }

    /// <summary>
    /// Разбирает строку метрик выполнения. Количество выданных записей приходит отдельной строкой
    /// ("N records fetched") ДО строки таймингов, поэтому оно накапливается вызывающим и передаётся
    /// сюда параметром <paramref name="fetchCount"/> — иначе оно теряется (было всегда 0).
    /// </summary>
    private static PerformanceInfo? ParsePerformance(string line,
        IReadOnlyDictionary<string, Regex> rules, int fetchCount)
    {
        var mPerf = rules[RuleKeys.Performance].Match(line);
        if (mPerf.Success)
            return new PerformanceInfo
            {
                ExecuteMs = mPerf.GetGroupInt("execute_ms"),
                FetchCount = fetchCount,
                ReadCount = mPerf.GetGroupInt("read"),
                WriteCount = mPerf.GetGroupInt("write"),
                MarkCount = mPerf.GetGroupInt("mark")
            };

        return null;
    }
}
