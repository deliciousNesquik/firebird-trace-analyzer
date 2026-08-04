using System.Text;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Core;

public sealed class ExecuteStatementsBuilder
{
    public static string Build(string sql, IReadOnlyList<SqlParameters?>? parameters)
    {
        // Нет параметров (или список null) — возвращаем сам текст запроса. Null-safe, как ExecuteProcedureBuilder
        // (иначе parameters.Count бросал бы NRE: у карточек ParamsProperty без дефолта = null).
        if (parameters is not { Count: > 0 })
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
                var parameter = parameters[indexParameter];
                // null-элемент оставляем как '?': подставить нечего (в trace значение отсутствовало).
                sqlBuilder.Append(parameter is null ? "?" : SqlParameterFormatter.Format(parameter));
                indexParameter++;
            }
            else
            {
                sqlBuilder.Append(ch);
            }

        return sqlBuilder.ToString();
    }
}