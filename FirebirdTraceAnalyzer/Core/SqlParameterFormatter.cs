using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Core;

/// <summary>
/// Единое форматирование значения SQL-параметра в SQL-литерал — строго по типу поля (Dtype).
/// Заменяет разошедшиеся копии FormatParam и эвристику TryParse в ExecuteProcedure.
/// </summary>
public static class SqlParameterFormatter
{
    public static string Format(SqlParameters parameter)
    {
        var value = parameter.Value;

        if (value.Equals("<NULL>", StringComparison.CurrentCultureIgnoreCase) ||
            value.Equals("NULL", StringComparison.CurrentCultureIgnoreCase))
            return "NULL";

        if (parameter.Dtype.ToLower().StartsWith("varchar"))
            return $"'{value?.Replace("'", "''")}'";

        return parameter.Dtype.ToLower() switch
        {
            "blob" => $"'{value}'",

            "timestamp" =>
                $"'{value}'",

            "date" =>
                $"'{value}'",

            "time" =>
                $"'{value}'",

            "bigint" or "int" or "smallint" or "integer" =>
                value ?? "NULL",

            _ =>
                value ?? "NULL"
        };
    }
}
