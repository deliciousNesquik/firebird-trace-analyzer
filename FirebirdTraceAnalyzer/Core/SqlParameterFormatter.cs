using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Core;

public static class SqlParameterFormatter
{
    /// <summary>
    /// Formatting SQL parameter value for use in SQL queries.
    /// </summary>
    /// <param name="parameter">The SQL parameter to format.</param>
    /// <returns>The formatted SQL parameter value.</returns>
    /// <remarks>Exists three type returned: NULL; "string"; 100. Any type alike string return in brackets.</remarks>
    public static string Format(SqlParameters parameter)
    {
        var value = parameter.Value.ToUpper()
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("«", "")
            .Replace("»", "")
            .Replace("„", "")
            .Replace("“", "");
        
        var type = parameter.Dtype.ToUpper();

        if (value.Equals("<NULL>") || value.Equals("NULL"))
            return "NULL";
        
        if (type.Equals("BIGINT") || type.Equals("INT") || type.Equals("SMALLINT") || type.Equals("INTEGER"))
            return value;
        
        return $"'{value.Replace("'", "").Replace("\"", "")}'";
    }
}
