using System.Text;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Core;

public sealed class ExecuteStatementsBuilder
{
    /// <summary>
    /// Builds a SQL statement by replacing placeholders with actual parameter values.
    /// </summary>
    /// <param name="sql">The SQL statement with placeholders.</param>
    /// <param name="parameters">The list of parameter values.</param>
    /// <returns>The SQL statement with parameters substituted.</returns>
    /// <example>
    ///     <code>
    ///         var sql = "SELECT * FROM users WHERE id = ? AND name = ?";
    ///         var parameters = new List&lt;SqlParameters?&gt; { 1, "John" };
    ///         var result = ExecuteStatementsBuilder.Build(sql, parameters);
    ///     </code>
    /// results in <c>sql</c>'s having the value <c>"SELECT * FROM users WHERE id = 1 AND name = "John""</c>.
    ///
    ///     <code>
    ///         var sql = "SELECT * FROM cities WHERE id = ? AND name = ? AND country = ?";
    ///         var parameters = new List&lt;SqlParameters?&gt; { 1, "New York" };
    ///         var result = ExecuteStatementsBuilder.Build(sql, parameters);
    ///     </code>
    /// results in <c>sql</c>'s having the value bellow:
    ///     <code>
    ///         /* Warning: the number of parameters does not match the number of placeholders. '?' */
    ///         SELECT * FROM cities WHERE id = 1 AND name = "New York" AND country = ?
    ///     </code>
    ///
    ///     <code>
    ///         var sql = "SELECT * FROM cities WHERE id = ? AND name = ? AND country = ?";
    ///         var parameters = new List&lt;SqlParameters?&gt; { 1, "New York", null };
    ///         var result = ExecuteStatementsBuilder.Build(sql, parameters);
    ///     </code>
    /// results in <c>sql</c>'s having the value bellow:
    ///     <code>
    ///         SELECT * FROM cities WHERE id = 1 AND name = "New York" AND country = /* not found parameter for this position */ ?
    ///     </code>
    /// </example>
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
            sqlBuilder.Append(parameter is null ? "/* not found parameter for this position '?' */" : $"{SqlParameterFormatter.Format(parameter)} ");
            indexOfParameter++;
        }
        return sqlBuilder.ToString();
    }
}