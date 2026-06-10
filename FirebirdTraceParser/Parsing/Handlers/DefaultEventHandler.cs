using System.Globalization;
using System.Text.RegularExpressions;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.Enums;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;
using FirebirdTraceParser.Parsing.Engine;
using FirebirdTraceParser.Parsing.Utils;
using NLog;

namespace FirebirdTraceParser.Parsing.Handlers;

/// <summary> Обработчик событий по умолчанию. </summary>
public sealed class DefaultEventHandler(ILogger logger, ParseOptions? options = null) : IEventHandler
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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


    public EventBase? Handle(Match blockHeader, IReadOnlyList<string> bodyLines,
        IReadOnlyDictionary<string, Regex> rules)
    {
        var eventTypeStr = blockHeader.Groups["event_type"].Value;

        if (eventTypeStr.StartsWith("ERROR AT ", StringComparison.OrdinalIgnoreCase))
            return HandleError(blockHeader, bodyLines, rules);

        if (!EventTypeMapping.TryGetValue(eventTypeStr, out var eventType))
        {
            _logger.Warn("Unknown event type for parser: '{EventType}'", eventTypeStr);
            return null;
        }
        
        EventBase? result = eventType switch
        {
            EventType.TraceInit => HandleTraceInit(blockHeader, bodyLines, rules),
            EventType.TraceFinish => HandleTraceFinish(blockHeader, bodyLines, rules),
            EventType.AttachDatabase => HandleAttach(blockHeader, bodyLines, rules),
            EventType.DetachDatabase => HandleDetach(blockHeader, bodyLines, rules),
            EventType.ExecuteStatementStart => HandleStatementStart(blockHeader, bodyLines, rules),
            EventType.ExecuteStatementRestart => HandleStatementRestart(blockHeader, bodyLines, rules),
            EventType.ExecuteStatementFinish => HandleStatementFinish(blockHeader, bodyLines, rules),
            EventType.ExecuteProcedureStart => HandleProcedureStart(blockHeader, bodyLines, rules),
            EventType.ExecuteProcedureFinish => HandleProcedureFinish(blockHeader, bodyLines, rules),
            EventType.ExecuteTriggerStart => HandleTriggerStart(blockHeader, bodyLines, rules),
            EventType.ExecuteTriggerFinish => HandleTriggerFinish(blockHeader, bodyLines, rules),
            EventType.FailedExecuteStatementFinish => HandleFailedStatementFinish(blockHeader, bodyLines, rules),
            EventType.FailedExecuteProcedureFinish => HandleFailedProcedureFinish(blockHeader, bodyLines, rules),
            EventType.FailedExecuteTriggerFinish => HandleFailedTriggerFinish(blockHeader, bodyLines, rules),
            _ => null
        };

        if (result == null)
            _logger.Warn("Handler returned null for {EventType}", eventType);

        return result;
    }

    // ==================== Common Event Metadata Parsing ====================

    /// <summary>
    ///     Извлекает общие метаданные заголовка события (timestamp, trace_id, hex_trace_id).
    /// </summary>
    private static (DateTime Timestamp, int TraceId, string HexTraceId) ParseEventMetadata(Match header)
    {
        var timestamp = DateTime.TryParse(header.Groups["ts"].Value, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var ts) ? ts : default;

        return (
            Timestamp: timestamp,
            TraceId: ParseIntOrDefault(header.Groups["trace_id"].ValueSpan),
            HexTraceId: StringPool.Intern(header.Groups["hex_trace_id"].Value)
        );
    }

    // Безопасный разбор числовых групп: один битый/переполненный field не теряет всё событие
    private static int ParseIntOrDefault(ReadOnlySpan<char> span, int defaultValue = 0)
        => int.TryParse(span, out var value) ? value : defaultValue;

    private static long ParseLongOrDefault(ReadOnlySpan<char> span, long defaultValue = 0)
        => long.TryParse(span, out var value) ? value : defaultValue;

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var session = ParseSessionInfo(bodyLines, rules);
        if (session is null) return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var session = ParseSessionInfo(bodyLines, rules);
        if (session is null) return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var attachment = ParseAttachmentInfo(bodyLines, rules);
        if (attachment is null) return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var attachment = ParseAttachmentInfo(bodyLines, rules);
        if (attachment is null) return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseStatementData(bodyLines, rules, false, false);
        if (data.Attachment is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseStatementData(bodyLines, rules, false, true);
        if (data.Attachment is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseStatementData(bodyLines, rules, true, true);
        if (data.Attachment is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseStatementData(bodyLines, rules, true, true);
        if (data.Attachment is null || data.Transaction is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseProcedureData(bodyLines, rules, false);
        if (data.Attachment is null || data.Transaction is null || data.ProcedureName is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseProcedureData(bodyLines, rules, true);
        if (data.Attachment is null || data.Transaction is null || data.ProcedureName is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseProcedureData(bodyLines, rules, true);
        if (data.Attachment is null || data.Transaction is null || data.ProcedureName is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseTriggerData(bodyLines, rules, false);

        if (data.Attachment is null || data.Transaction is null || data.TriggerName is null || data.Event is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseTriggerData(bodyLines, rules, true);

        if (data.Attachment is null || data.Transaction is null || data.TriggerName is null || data.Event is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var data = ParseTriggerData(bodyLines, rules, true);

        if (data.Attachment is null || data.Transaction is null || data.TriggerName is null || data.Event is null)
            return null;

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

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
        IReadOnlyDictionary<string, Regex> rules)
    {
        var attachment = ParseAttachmentInfo(bodyLines, rules);
        if (attachment is null) return null;

        // Извлекаем компонент из "ERROR AT <компонент>"
        var eventTypeStr = header.Groups["event_type"].Value;
        var component = eventTypeStr["ERROR AT ".Length..].Trim();

        // Парсим цепочку ошибок
        var errors = ParseErrorChain(bodyLines, rules);

        var (timestamp, traceId, hexTraceId) = ParseEventMetadata(header);

        return new ErrorEvent
        {
            Timestamp = timestamp,
            TraceId = traceId,
            HexTraceId = hexTraceId,
            EventType = EventType.Error,
            Attachment = attachment,
            Component = StringPool.Intern(component),
            Errors = errors
        };
    }

    // ==================== Parsing helpers ====================

    private static TraceSessionInfo? ParseSessionInfo(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules)
    {
        foreach (var line in lines)
        {
            var m = rules["session"].Match(line);
            if (m.Success)
            {
                var sessionId = ParseIntOrDefault(m.Groups["session_id"].ValueSpan);
                return TraceSessionPool.Intern(sessionId);
            }
        }

        return null;
    }

    private static AttachmentInfo? ParseAttachmentInfo(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules)
    {
        Match? am = null;
        Match? pm = null;

        foreach (var line in lines)
        {
            if (am is null)
            {
                var match = rules["attachment"].Match(line);
                if (match.Success) am = match;
            }

            if (pm is null)
            {
                var match = rules["process"].Match(line);
                if (match.Success) pm = match;
            }

            if (am is not null && pm is not null) break;
        }

        if (am is null) return null;

        var attachmentId = ParseLongOrDefault(am.Groups["attachment_id"].ValueSpan);

        if (AttachmentPool.TryGet(attachmentId, out var cachedInfo))
            return cachedInfo;

        var newInfo = new AttachmentInfo
        {
            AttachmentId = attachmentId,
            DatabasePath = StringPool.Intern(am.Groups["database_path"].Value),
            User = StringPool.Intern(am.Groups["user"].Value),
            Role = StringPool.Intern(am.Groups["role"].Value),
            Charset = StringPool.Intern(am.Groups["charset"].Value),
            Protocol = StringPool.Intern(am.Groups["protocol"].Value.Trim()),
            Address = am.Groups["address"].Success
                ? StringPool.Intern(am.Groups["address"].Value)
                : StringPool.Intern("<internal>"),
            Port = am.Groups["port"].Success ? ParseIntOrDefault(am.Groups["port"].ValueSpan) : 0,
            ProcessPath = pm is not null ? StringPool.Intern(pm.Groups["process_path"].Value) : null,
            ProcessId = pm is not null ? ParseIntOrDefault(pm.Groups["process_id"].ValueSpan) : null
        };

        return AttachmentPool.Add(newInfo);
    }

    private static TransactionInfo? ParseTransactionInfo(string line,
        IReadOnlyDictionary<string, Regex> rules)
    {
        if (!line.Contains("(TRA_")) return null;

        var m = rules["transaction"].Match(line);
        if (!m.Success) return null;

        var tid = int.Parse(m.Groups["transaction_id"].ValueSpan);

        // Получаем ValueSpan вместо Value (нет аллокации строки для группы)
        var paramsSpan = m.Groups["params"].ValueSpan;

        // Дефолтные значения (строковые литералы уже интернированы самой CLR)
        var isolationLevel = "NONE";
        var consistencyMode = "NONE";
        var lockMode = "NONE";
        var accessMode = "NONE";

        // Безалокационный проход по токенам через Срезы (Slices)
        var start = 0;
        while (start < paramsSpan.Length)
        {
            var currentSlice = paramsSpan[start..];
            var nextPipe = currentSlice.IndexOf('|');

            var token = nextPipe == -1 ? currentSlice : currentSlice[..nextPipe];
            token = token.Trim(); // Сдвигает указатели span, выделения памяти нет

            if (!token.IsEmpty &&
                !token.Equals("NONE", StringComparison.OrdinalIgnoreCase) &&
                !token.Equals("(NONE)", StringComparison.OrdinalIgnoreCase))
            {
                // Сравнение span без ToUpperInvariant()
                // Проверяем уровни изоляции
                if (token.Equals("READ_COMMITTED", StringComparison.OrdinalIgnoreCase))
                    isolationLevel = "READ_COMMITTED";
                else if (token.Equals("CONCURRENCY", StringComparison.OrdinalIgnoreCase))
                    isolationLevel = "CONCURRENCY";
                else if (token.Equals("SNAPSHOT", StringComparison.OrdinalIgnoreCase)) isolationLevel = "SNAPSHOT";
                else if (token.Equals("SNAPSHOT_TABLE_STABILITY", StringComparison.OrdinalIgnoreCase))
                    isolationLevel = "SNAPSHOT_TABLE_STABILITY";

                // Проверяем режимы консистентности
                else if (token.Equals("READ_CONSISTENCY", StringComparison.OrdinalIgnoreCase))
                    consistencyMode = "READ_CONSISTENCY";
                else if (token.Equals("NO_RECORD_VERSION", StringComparison.OrdinalIgnoreCase))
                    consistencyMode = "NO_RECORD_VERSION";
                else if (token.Equals("RECORD_VERSION", StringComparison.OrdinalIgnoreCase))
                    consistencyMode = "RECORD_VERSION";

                // Проверяем режимы блокировки
                else if (token.Equals("NOWAIT", StringComparison.OrdinalIgnoreCase)) lockMode = "NOWAIT";
                else if (token.Equals("WAIT", StringComparison.OrdinalIgnoreCase)) lockMode = "WAIT";
                else if (token.Equals("LOCK_TIMEOUT", StringComparison.OrdinalIgnoreCase))
                    lockMode = "LOCK_TIMEOUT";

                // Проверяем режимы доступа
                else if (token.Equals("READ_WRITE", StringComparison.OrdinalIgnoreCase)) accessMode = "READ_WRITE";
                else if (token.Equals("READ_ONLY", StringComparison.OrdinalIgnoreCase)) accessMode = "READ_ONLY";
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

    private static IReadOnlyList<ErrorLines> ParseErrorChain(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules)
    {
        List<ErrorLines>? errors = null;

        foreach (var line in lines)
        {
            var match = rules["error_line"].Match(line.Trim());
            if (match.Success)
                (errors ??= new()).Add(new ErrorLines
                {
                    ErrorCode = ParseIntOrDefault(match.Groups[1].ValueSpan),
                    Message = StringPool.Intern(match.Groups[2].Value.Trim())
                });
        }

        return errors ?? (IReadOnlyList<ErrorLines>)Array.Empty<ErrorLines>();
    }

    // ==================== Вспомогательные методы для классификации ====================

    /// <summary>
    ///     Парсит одну строку SQL-параметра и возвращает объект SqlParameters или null.
    /// </summary>
    private static SqlParameters? ParseSqlParameter(string line, IReadOnlyDictionary<string, Regex> rules)
    {
        var paramM = rules["parameters"].Match(line);
        if (!paramM.Success)
            return null;

        var value = paramM.Groups["value"].Success
            ? paramM.Groups["value"].Value
            : paramM.Groups["value_null"].Value;

        return new SqlParameters
        {
            Name = StringPool.Intern(paramM.Groups["name"].Value),
            Dtype = StringPool.Intern(paramM.Groups["dtype"].Value),
            Value = StringPool.Intern(value)
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

    private static StatementData ParseStatementData(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules, bool includePerformance, bool isRestarted)
    {
        var attachment = ParseAttachmentInfo(lines, rules);
        TransactionInfo? transaction = null;
        long? statementId = null;
        string? sql = null;
        List<SqlParameters>? paramsList = null;
        PerformanceInfo? performance = null;
        PerformanceTable? performanceTable = null;
        var performanceTableSearched = false;
        int? restartCount = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (transaction is null)
            {
                transaction = ParseTransactionInfo(line, rules);
                if (transaction is not null) continue;
            }

            var sm = rules["statement"].Match(line);
            if (sm.Success)
            {
                statementId = ParseLongOrDefault(sm.Groups["statement_id"].ValueSpan);
                continue;
            }

            if (isRestarted && restartCount is null)
            {
                var rm = rules["restarted"].Match(line);
                if (rm.Success)
                {
                    restartCount = ParseIntOrDefault(rm.Groups["count"].ValueSpan);
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

                    if (isRestarted && rules["restarted"].IsMatch(l))
                    {
                        // Не добавляем эту строку в SQL, она будет обработана на следующей итерации основного цикла
                        i--; // Откатываем индекс, чтобы основной цикл обработал эту строку
                        break;
                    }

                    // break if param or performance/fetched
                    if (rules["parameters"].IsMatch(l) ||
                        (includePerformance && (rules["fetched"].IsMatch(l) || rules["performance"].IsMatch(l))))
                        break;

                    sqlLines.Add(l);
                    i++;
                }

                sql = StringPool.Intern(string.Join("\n", sqlLines).Trim());
                continue;
            }

            var param = ParseSqlParameter(line, rules);
            if (param is not null)
            {
                (paramsList ??= new()).Add(param);
                continue;
            }

            if (includePerformance && performance is null)
            {
                performance = ParsePerformance(line, rules);
                if (performance is not null) continue;
            }

            if (includePerformance && !performanceTableSearched)
            {
                // Сканируем хвост на таблицу производительности один раз, без копии массива
                performanceTableSearched = true;
                performanceTable = PerformanceTableParser.ParsePerformanceTable(lines, i, rules);
            }
        }

        return new StatementData(attachment, transaction, statementId, sql,
            paramsList ?? (IReadOnlyList<SqlParameters>)Array.Empty<SqlParameters>(),
            performance, performanceTable, restartCount);
    }

    private static ProcedureData ParseProcedureData(IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, Regex> rules, bool includePerformance)
    {
        var attachment = ParseAttachmentInfo(lines, rules);
        TransactionInfo? transaction = null;
        string? procedureName = null;
        List<SqlParameters>? paramsList = null;
        PerformanceInfo? performance = null;
        PerformanceTable? performanceTable = null;

        foreach (var line in lines)
        {
            if (transaction is null)
            {
                transaction = ParseTransactionInfo(line, rules);
                if (transaction is not null) continue;
            }

            var pmProc = rules["procedure"].Match(line);
            if (pmProc.Success)
            {
                procedureName = StringPool.Intern(pmProc.Groups["procedure_name"].Value);
                continue;
            }

            var param = ParseSqlParameter(line, rules);
            if (param is not null)
            {
                (paramsList ??= new()).Add(param);
                continue;
            }

            if (includePerformance && performance is null)
                performance = ParsePerformance(line, rules);
        }

        return new ProcedureData(attachment, transaction, procedureName,
            paramsList ?? (IReadOnlyList<SqlParameters>)Array.Empty<SqlParameters>(),
            performance, performanceTable);
    }

    private static TriggerData ParseTriggerData(IReadOnlyList<string> lines, IReadOnlyDictionary<string, Regex> rules,
        bool includePerformance)
    {
        var attachment = ParseAttachmentInfo(lines, rules);
        TransactionInfo? transaction = null;
        string? triggerName = null;
        string? table = null;
        string? timing = null;
        string? eventType = null;
        PerformanceInfo? performance = null;
        PerformanceTable? performanceTable = null;

        foreach (var line in lines)
        {
            if (transaction is null)
            {
                transaction = ParseTransactionInfo(line, rules);
                if (transaction is not null) continue;
            }

            var tm = rules["trigger"].Match(line);
            if (tm.Success)
            {
                triggerName = StringPool.Intern(tm.Groups["trigger_name"].Value);

                // проверяем тип триггера (DML или DDL)
                if (tm.Groups["table"].Success) // DML триггер (FOR table)
                {
                    table = StringPool.Intern(tm.Groups["table"].Value);
                    timing = StringPool.Intern(tm.Groups["timing"].Value);
                    eventType = StringPool.Intern(tm.Groups["event"].Value);
                }
                else if (tm.Groups["ddl_event"].Success) // DDL триггер (ON ...)
                {
                    table = null;
                    timing = null;
                    eventType = StringPool.Intern(tm.Groups["ddl_event"].Value); // "ON CONNECT", "ON DISCONNECT", etc.
                }

                continue;
            }

            if (includePerformance && performance is null)
                performance = ParsePerformance(line, rules);
        }

        return new TriggerData(attachment, transaction, triggerName, table, timing, eventType, performance,
            performanceTable);
    }

    private static PerformanceInfo? ParsePerformance(string line,
        IReadOnlyDictionary<string, Regex> rules)
    {
        var fetchCount = 0;
        
        var mFetched = rules["fetched"].Match(line);
        if (mFetched.Success)
            fetchCount = ParseIntOrDefault(mFetched.Groups["fetch_count"].ValueSpan);

        var mPerf = rules["performance"].Match(line);
        if (mPerf.Success)
            return new PerformanceInfo
            {
                ExecuteMs = ParseIntOrDefault(mPerf.Groups["execute_ms"].ValueSpan),
                FetchCount = fetchCount,
                ReadCount = mPerf.Groups["read"].Success ? ParseIntOrDefault(mPerf.Groups["read"].ValueSpan) : 0,
                WriteCount = mPerf.Groups["write"].Success ? ParseIntOrDefault(mPerf.Groups["write"].ValueSpan) : 0,
                MarkCount = mPerf.Groups["mark"].Success ? ParseIntOrDefault(mPerf.Groups["mark"].ValueSpan) : 0
            };

        return null;
    }
}