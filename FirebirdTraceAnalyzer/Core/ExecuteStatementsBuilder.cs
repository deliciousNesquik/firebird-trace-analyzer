using System.Text;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Core;

public sealed class ExecuteStatementsBuilder
{
    public static string Build(string sql, IReadOnlyList<SqlParameters?> parameters)
    {
        // Если параметры отсутвуют тогда можно вернуть только sql statements text
        if (parameters.Count == 0)
            return sql;

        // Создаем строителя строк для сборки параметров и запроса в единое целое
        var sqlBuilder = new StringBuilder();
        var indexParameter = 0;

        // Обязательно проверить нужно, что количество вопросов в запросе соответствует количеству парамеров иначе
        // добавь комментарий что программа не умеет сопоставлять нужные параметры с нужными местами и берет значения
        // только в том порядке каком указаны в trace log
        if (sql.Count(p => p.Equals('?')) != parameters.Count)
            sqlBuilder.AppendLine(
                "/* Внимание: количество параметров не соответствует количеству мест для подстановки '?' */");

        // Перебираем все символы и подставляем форматированное значение параметра
        foreach (var ch in sql)
            if (ch == '?' && indexParameter < parameters.Count)
            {
                sqlBuilder.Append(FormatParam(parameters[indexParameter]));
                indexParameter++;
            }
            else
            {
                sqlBuilder.Append(ch);
            }

        return sqlBuilder.ToString();
    }


    private static string FormatParam(SqlParameters parameter)
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