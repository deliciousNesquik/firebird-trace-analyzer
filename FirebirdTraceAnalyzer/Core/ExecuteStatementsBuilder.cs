using System.Text;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Core;

public sealed class ExecuteStatementsBuilder
{
    public static string Build(string sql, IReadOnlyList<SqlParameters?>? parameters)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;
        
        if (parameters is not { Count: > 0 })
            return sql;
        
        var sqlBuilder = new StringBuilder();
        var indexOfParameter = 0;

        // You must verify that the number of `?` placeholders in the query matches the number of parameters; otherwise, issue a warning.
        if (sql.Count(p => p.Equals('?')) != parameters.Count)
            sqlBuilder.AppendLine(
                "/* Warning: the number of parameters does not match the number of placeholders. '?' */");

        foreach (var ch in sql)
        {
            if (ch != '?' || indexOfParameter >= parameters.Count)
            {
                sqlBuilder.Append(ch);
                continue;
            }
            
            var parameter = parameters[indexOfParameter];
            // We leave the null element as '?': there is nothing to substitute (the value was missing in the trace).
            sqlBuilder.Append(parameter is null ? "/* not found parameter for this position */ ?" : $"{SqlParameterFormatter.Format(parameter)} ");
            indexOfParameter++;
        }
        return sqlBuilder.ToString();
    }
}