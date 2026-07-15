namespace FirebirdTraceAnalyzer.Models.Storage;

/// <summary>
/// Результат произвольного SELECT по хранилищу: динамические колонки + строки значений.
/// Отвязан от доменных типов — годится для показа таблицей и экспорта.
/// </summary>
/// <param name="Columns">Имена колонок в порядке выборки.</param>
/// <param name="Rows">Строки; каждая — массив значений по числу колонок (null = NULL).</param>
/// <param name="Truncated">true — строк было больше лимита, показана только часть.</param>
/// <param name="ElapsedMs">Время выполнения запроса, мс.</param>
public sealed record StorageQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<object?[]> Rows,
    bool Truncated,
    long ElapsedMs);
