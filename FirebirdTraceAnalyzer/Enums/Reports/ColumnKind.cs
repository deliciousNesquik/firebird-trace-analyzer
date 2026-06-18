namespace FirebirdTraceAnalyzer.Enums.Reports;

/// <summary>
/// Роль колонки отчёта.
/// </summary>
public enum ColumnKind
{
    /// <summary>Обычное поле события (значение по PropertyPath). Поведение по умолчанию.</summary>
    Field,

    /// <summary>Ключ группировки (значение, по которому события группируются).</summary>
    GroupKey,

    /// <summary>Агрегат над группой (Count/Sum/Average/…).</summary>
    Aggregate
}
