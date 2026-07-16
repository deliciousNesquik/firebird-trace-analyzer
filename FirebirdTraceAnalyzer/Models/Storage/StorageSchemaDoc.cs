namespace FirebirdTraceAnalyzer.Models.Storage;

/// <summary>
/// Человекочитаемые описания таблиц и колонок хранилища событий — для подсказок автодополнения
/// в окне «Анализ хранилища». Колонки ключуются по имени (без таблицы): в схеме имена почти
/// уникальны, а совпадающие (id/sha/ord/event_seq) описаны обобщённо.
/// </summary>
public static class StorageSchemaDoc
{
    private static readonly IReadOnlyDictionary<string, string> TableDocs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["files"] = "Manifest of imported files (one row per trace file)",
            ["event"] = "Trace events (single table, nullable columns per subtype)",
            ["sql_text"] = "SQL text dictionary (deduplicated across files)",
            ["attachment"] = "Attachment dictionary (deduplicated across files)",
            ["sql_parameter"] = "SQL parameters per event",
            ["error_line"] = "Error lines per event",
            ["perf_table_item"] = "Per-table access stats (Natural/Index/…) per event",
        };

    private static readonly IReadOnlyDictionary<string, string> ColumnDocs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // shared
            ["id"] = "Row primary key (files/sql_text/attachment)",
            ["sha"] = "SHA-256 of the content (dedup key)",
            ["event_seq"] = "Reference to event.seq — the parent event",
            ["ord"] = "Position within the list (0..N)",

            // files
            ["hash"] = "SHA-256 hash of the file (unique)",
            ["name"] = "File name / parameter name",
            ["path"] = "Path to the trace file",
            ["size"] = "File size, bytes",
            ["start_ts"] = "First event time of the file (DateTime.Ticks)",
            ["end_ts"] = "Last event time of the file (DateTime.Ticks)",
            ["event_count"] = "Number of events in the file",
            ["imported_ts"] = "When the file was written to the store (Ticks, UTC)",

            // event
            ["seq"] = "Event primary key",
            ["file_id"] = "Reference to files.id — which file the event belongs to",
            ["ts"] = "Event timestamp (DateTime.Ticks)",
            ["trace_id"] = "Trace identifier (decimal)",
            ["hex_trace_id"] = "Trace identifier (hex)",
            ["event_type"] = "Event type — int from the EventType enum",
            ["attachment_ref"] = "Reference to attachment.id — the connection",
            ["session_id"] = "Session identifier",
            ["sql_ref"] = "Reference to sql_text.id — the SQL text",
            ["statement_id"] = "Statement identifier",
            ["txn_present"] = "Whether a transaction is present (0/1)",
            ["txn_id"] = "Transaction identifier",
            ["txn_isolation"] = "Transaction isolation level",
            ["txn_consistency"] = "Consistency mode (e.g. REC_VERSION)",
            ["txn_lock"] = "Lock mode (WAIT/NOWAIT)",
            ["txn_access"] = "Access mode (READ/READ_WRITE)",
            ["restart_count"] = "Statement restart count",
            ["procedure_name"] = "Stored procedure name",
            ["trigger_name"] = "Trigger name",
            ["trigger_table"] = "Trigger table (DML)",
            ["trigger_timing"] = "Trigger timing (BEFORE/AFTER)",
            ["trigger_event"] = "Trigger event (INSERT/UPDATE/…) or DDL event",
            ["component"] = "Event component",
            ["perf_present"] = "Whether performance metrics are present (0/1)",
            ["perf_execute_ms"] = "Execution time, ms",
            ["perf_fetch"] = "Fetch count",
            ["perf_read"] = "Read count",
            ["perf_write"] = "Write count",
            ["perf_mark"] = "Mark count",
            ["perf_table_state"] = "Performance-table state: 0 none / 1 present without rows / 2 with rows",

            // sql_text
            ["text"] = "SQL statement text",

            // attachment
            ["att_id"] = "Firebird attachment identifier",
            ["db_path"] = "Database path",
            ["user"] = "Connection user",
            ["role"] = "Connection role",
            ["charset"] = "Connection charset",
            ["protocol"] = "Protocol (e.g. TCPv4)",
            ["address"] = "Client network address",
            ["port"] = "Client port",
            ["process_path"] = "Client process path",
            ["process_id"] = "Client process id (PID)",

            // sql_parameter
            ["dtype"] = "SQL parameter data type",
            ["value"] = "Parameter / statistic value",

            // error_line
            ["code"] = "Firebird error code",
            ["message"] = "Error message text",

            // perf_table_item
            ["table_name"] = "Table name (access statistics)",
            ["natural_count"] = "Natural reads — full scan (no index)",
            ["index_count"] = "Index reads",
            ["update_count"] = "Update count",
            ["insert_count"] = "Insert count",
            ["delete_count"] = "Delete count",
            ["backout_count"] = "Backout count",
            ["purge_count"] = "Purge count",
            ["expunge_count"] = "Expunge count",
        };

    /// <summary>Описание таблицы по имени или null.</summary>
    public static string? Table(string name) => TableDocs.GetValueOrDefault(name);

    /// <summary>Описание колонки по имени (без таблицы) или null.</summary>
    public static string? Column(string name) => ColumnDocs.GetValueOrDefault(name);
}
