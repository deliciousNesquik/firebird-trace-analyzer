using System.Text;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Core;

/// <summary>
/// Собирает вызов «EXECUTE PROCEDURE name(p1, p2, …)» с форматированием параметров по типу.
/// Аналог <see cref="ExecuteStatementsBuilder"/>, но для процедур. Заменяет три копии свойства
/// ExecuteProcedure в карточках событий (ранее угадывавших тип через TryParse).
/// </summary>
public static class ExecuteProcedureBuilder
{
    public static string Build(string procedureName, IReadOnlyList<SqlParameters>? parameters)
    {
        var execute = new StringBuilder();
        execute.Append($"EXECUTE PROCEDURE {procedureName}(");

        if (parameters is { Count: > 0 })
            execute.Append(string.Join(", ", parameters.Select(SqlParameterFormatter.Format)));

        execute.Append(')');

        return execute.ToString();
    }
}
