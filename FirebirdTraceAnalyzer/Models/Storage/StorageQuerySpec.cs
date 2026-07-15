namespace FirebirdTraceAnalyzer.Models.Storage;

/// <summary>Пресет периода для фильтра по времени в конструкторе запросов.</summary>
public enum QueryPeriod
{
    AllTime,
    Today,
    Week,
    Month
}

/// <summary>Разрез (колонка группировки): идентификатор + отображаемое имя (оно же alias в SQL).</summary>
public sealed record QueryDimensionOption(string Id, string DisplayName);

/// <summary>Показатель (агрегат): идентификатор + отображаемое имя (оно же alias в SQL).</summary>
public sealed record QueryMeasureOption(string Id, string DisplayName);
