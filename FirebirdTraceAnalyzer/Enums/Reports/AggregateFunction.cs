namespace FirebirdTraceAnalyzer.Enums.Reports;

/// <summary>
/// Агрегатная функция над группой событий (как GROUP BY в SQL).
/// </summary>
public enum AggregateFunction
{
    /// <summary>Количество событий в группе.</summary>
    Count,

    /// <summary>Количество уникальных значений поля в группе.</summary>
    CountDistinct,

    /// <summary>Сумма значений поля.</summary>
    Sum,

    /// <summary>Среднее значение поля.</summary>
    Average,

    /// <summary>Минимальное значение поля.</summary>
    Min,

    /// <summary>Максимальное значение поля.</summary>
    Max
}
