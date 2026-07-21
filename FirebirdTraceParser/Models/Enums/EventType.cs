using System.ComponentModel;

namespace FirebirdTraceParser.Models.Enums;

/// <summary>
/// Типы событий трассировки Firebird Database.
/// <para>
/// ВНИМАНИЕ: значения сохраняются в хранилище как целочисленный ординал (см. EventStoreService,
/// <c>(int)EventType</c>). Поэтому существующие значения НЕЛЬЗЯ переупорядочивать или удалять —
/// это повредит уже записанные events.db. Новые типы добавляются только в конец с новым числом.
/// </para>
/// </summary>
public enum EventType
{
    /// <summary>Инициализация trace‑сессии</summary>
    [Description("TRACE_INIT")] TraceInit = 0,

    /// <summary>Завершение trace‑сессии</summary>
    [Description("TRACE_FINI")] TraceFinish = 1,

    /// <summary>Подключение к базе данных</summary>
    [Description("ATTACH_DATABASE")] AttachDatabase = 2,

    /// <summary>Отключение от базы данных</summary>
    [Description("DETACH_DATABASE")] DetachDatabase = 3,

    /// <summary>Начало выполнения statement</summary>
    [Description("EXECUTE_STATEMENT_START")] ExecuteStatementStart = 4,

    /// <summary>Повторное выполнение statement</summary>
    [Description("EXECUTE_STATEMENT_RESTART")] ExecuteStatementRestart = 5,

    /// <summary>Завершение выполнения statement</summary>
    [Description("EXECUTE_STATEMENT_FINISH")] ExecuteStatementFinish = 6,

    /// <summary>Начало выполнения процедуры</summary>
    [Description("EXECUTE_PROCEDURE_START")] ExecuteProcedureStart = 7,

    /// <summary>
    /// Зарезервировано. Firebird не эмитит EXECUTE_PROCEDURE_RESTART для процедур; обработчика нет,
    /// такой блок (если встретится) распознаётся заголовком и фиксируется как пропуск (Warning).
    /// Значение сохранено ради стабильности ординалов и совместимости хранилища — не удалять.
    /// </summary>
    [Description("EXECUTE_PROCEDURE_RESTART")] ExecuteProcedureRestart = 8,

    /// <summary>Завершение выполнения процедуры</summary>
    [Description("EXECUTE_PROCEDURE_FINISH")] ExecuteProcedureFinish = 9,

    /// <summary>Начало выполнения триггера</summary>
    [Description("EXECUTE_TRIGGER_START")] ExecuteTriggerStart = 10,

    /// <summary>Завершение выполнения триггера</summary>
    [Description("EXECUTE_TRIGGER_FINISH")] ExecuteTriggerFinish = 11,

    /// <summary>Ошибка завершения выполнения statement</summary>
    [Description("FAILED EXECUTE_STATEMENT_FINISH")] FailedExecuteStatementFinish = 12,

    /// <summary>Ошибка завершения выполнения процедуры</summary>
    [Description("FAILED EXECUTE_PROCEDURE_FINISH")] FailedExecuteProcedureFinish = 13,

    /// <summary>Ошибка завершения выполнения триггера</summary>
    [Description("FAILED EXECUTE_TRIGGER_FINISH")] FailedExecuteTriggerFinish = 14,

    /// <summary>Любая ошибка возникшая в определенном модуле</summary>
    [Description("ERROR")] Error = 15,
}
