using System.Text;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Core;

public static class ExecuteProcedureBuilder
{
    /// <summary>
    /// Build the SQL command to execute a stored procedure with the given parameters.
    /// </summary>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">The list of parameters for the stored procedure. <see cref="SqlParameters"/></param>
    /// <returns>The SQL command as a string.</returns>
    /// <example>
    ///     <code>
    ///         var sql = ExecuteProcedureBuilder.Build(
    ///             "MyProcedure",
    ///             new List&lt;SqlParameters&gt; {
    ///                 new SqlParameters(){
    ///                     Name="param1",
    ///                     Value="10",
    ///                     Dtype="integer"},
    ///                 new SqlParameters(){
    ///                     Name="param2",
    ///                     Value="my procedure",
    ///                     Dtype="varchar"}
    ///             }
    ///         );
    ///     </code>
    /// results in <c>sql</c>'s having the value <c>"EXECUTE PROCEDURE MyProcedure(10, 'my procedure')"</c>.     
    /// </example>
    public static string Build(string procedureName, IReadOnlyList<SqlParameters>? parameters)
    {
        // TODO: anytime we add build rule, space around the comma or brackets. Uppercase keywords, etc. should be configurable.
        
        var execute = new StringBuilder();
        execute.Append($"EXECUTE PROCEDURE {procedureName}(");

        if (parameters is { Count: > 0 })
            execute.Append(string.Join(", ", parameters.Select(SqlParameterFormatter.Format)));

        execute.Append(')');

        return execute.ToString();
    }
}
