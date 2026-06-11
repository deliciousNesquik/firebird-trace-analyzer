using System.Text.RegularExpressions;

namespace FirebirdTraceParser.Parsing.Utils;

/// <summary>
/// Расширения для упрощения парсинга.
/// </summary>
public static class ParsingExtensions
{
    /// <summary>
    /// Безопасное извлечение значения группы regex.
    /// </summary>
    public static string GetGroupValue(this Match match, string groupName, string defaultValue = "")
    {
        return match.Groups[groupName].Success 
            ? match.Groups[groupName].Value 
            : defaultValue;
    }
    
    /// <summary>
    /// Безопасное извлечение числового значения группы.
    /// </summary>
    public static int GetGroupInt(this Match match, string groupName, int defaultValue = 0)
    {
        if (!match.Groups[groupName].Success) 
            return defaultValue;
        
        return int.TryParse(match.Groups[groupName].Value, out var result)
            ? result
            : defaultValue;
    }
}