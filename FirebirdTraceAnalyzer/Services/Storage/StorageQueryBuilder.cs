using System.Text;
using FirebirdTraceAnalyzer.Models.Storage;

namespace FirebirdTraceAnalyzer.Services.Storage;

/// <summary>
/// Собирает агрегатный SQL по схеме хранилища из выбора конструктора (разрезы + показатели +
/// период/пользователь). Чистая функция без побочных эффектов — легко тестируется. Времена в БД
/// хранятся как <c>DateTime.Ticks</c> (INTEGER), поэтому фильтр периода сравнивает по тикам, а
/// разрез «День» конвертирует тики в дату.
/// </summary>
public static class StorageQueryBuilder
{
    [Flags]
    private enum Joins { None = 0, Attachment = 1, Sql = 2, Files = 4 }

    private sealed record Def(string Expr, Joins Join);

    // Тики Unix-эпохи (1970-01-01) = DateTime.UnixEpoch.Ticks — для перевода ts (тики) в 'unixepoch'.
    private const long UnixEpochTicks = 621355968000000000L;

    // Порядок важен — в этом порядке пункты показываются в UI.
    private static readonly (string Id, Def Def)[] DimensionDefs =
    [
        ("user",       new("a.user", Joins.Attachment)),
        ("event_type", new("e.event_type", Joins.None)),
        ("procedure",  new("e.procedure_name", Joins.None)),
        ("database",   new("a.db_path", Joins.Attachment)),
        ("sql",        new("s.text", Joins.Sql)),
        ("file",       new("f.name", Joins.Files)),
        ("day",        new($"date((e.ts - {UnixEpochTicks}) / 10000000, 'unixepoch')", Joins.None)),
    ];

    private static readonly (string Id, Def Def)[] MeasureDefs =
    [
        ("count",     new("COUNT(*)", Joins.None)),
        ("files",     new("COUNT(DISTINCT e.file_id)", Joins.None)),
        ("users",     new("COUNT(DISTINCT e.attachment_ref)", Joins.None)),
        ("sum_exec",  new("SUM(e.perf_execute_ms)", Joins.None)),
        ("avg_exec",  new("AVG(e.perf_execute_ms)", Joins.None)),
        ("max_exec",  new("MAX(e.perf_execute_ms)", Joins.None)),
        ("sum_read",  new("SUM(e.perf_read)", Joins.None)),
        ("sum_write", new("SUM(e.perf_write)", Joins.None)),
    ];

    private static readonly Dictionary<string, Def> Dims =
        DimensionDefs.ToDictionary(x => x.Id, x => x.Def);

    private static readonly Dictionary<string, Def> Measures =
        MeasureDefs.ToDictionary(x => x.Id, x => x.Def);

    /// <summary>Идентификаторы разрезов в порядке отображения.</summary>
    public static IReadOnlyList<string> DimensionIds { get; } = DimensionDefs.Select(x => x.Id).ToList();

    /// <summary>Идентификаторы показателей в порядке отображения.</summary>
    public static IReadOnlyList<string> MeasureIds { get; } = MeasureDefs.Select(x => x.Id).ToList();

    public static string Build(
        IReadOnlyList<QueryDimensionOption> dimensions,
        IReadOnlyList<QueryMeasureOption> measures,
        QueryPeriod period,
        string? user,
        int limit,
        DateTime now)
    {
        var joins = Joins.None;
        var select = new List<string>();
        var groupBy = new List<string>();

        foreach (var d in dimensions)
        {
            if (!Dims.TryGetValue(d.Id, out var def))
                continue;
            joins |= def.Join;
            select.Add($"{def.Expr} AS {Quote(d.DisplayName)}");
            groupBy.Add(def.Expr);
        }

        string? firstMeasureAlias = null;
        foreach (var m in measures)
        {
            if (!Measures.TryGetValue(m.Id, out var def))
                continue;
            joins |= def.Join;
            var alias = Quote(m.DisplayName);
            select.Add($"{def.Expr} AS {alias}");
            firstMeasureAlias ??= alias;
        }

        if (select.Count == 0)
            select.Add("COUNT(*) AS \"count\"");

        var where = new List<string>();
        if (PeriodCutoff(period, now) is { } cutoff)
            where.Add($"e.ts >= {cutoff.Ticks}");
        if (!string.IsNullOrWhiteSpace(user))
        {
            joins |= Joins.Attachment;
            where.Add($"a.user = '{user.Replace("'", "''")}'");
        }

        var sb = new StringBuilder();
        sb.Append("SELECT ").Append(string.Join(",\n       ", select)).Append('\n');
        sb.Append("FROM event e");
        if (joins.HasFlag(Joins.Attachment)) sb.Append("\nLEFT JOIN attachment a ON a.id = e.attachment_ref");
        if (joins.HasFlag(Joins.Sql)) sb.Append("\nLEFT JOIN sql_text s ON s.id = e.sql_ref");
        if (joins.HasFlag(Joins.Files)) sb.Append("\nLEFT JOIN files f ON f.id = e.file_id");
        if (where.Count > 0) sb.Append("\nWHERE ").Append(string.Join("\n  AND ", where));
        if (groupBy.Count > 0) sb.Append("\nGROUP BY ").Append(string.Join(", ", groupBy));
        if (firstMeasureAlias is not null) sb.Append($"\nORDER BY {firstMeasureAlias} DESC");
        else if (groupBy.Count > 0) sb.Append("\nORDER BY 1");
        if (limit > 0) sb.Append($"\nLIMIT {limit}");

        return sb.ToString();
    }

    private static DateTime? PeriodCutoff(QueryPeriod period, DateTime now) => period switch
    {
        QueryPeriod.Today => now.Date,
        QueryPeriod.Week => now.Date.AddDays(-6),
        QueryPeriod.Month => now.Date.AddMonths(-1),
        _ => null
    };

    private static string Quote(string alias) => "\"" + alias.Replace("\"", "\"\"") + "\"";
}
