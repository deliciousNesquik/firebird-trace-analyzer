using System.Text.RegularExpressions;

namespace FirebirdTraceParser.Parsing.Utils;

/// <summary>
/// Расширения для безопасного чтения групп regex при разборе. Числовые перегрузки читают ValueSpan
/// (без аллокации строки), поэтому годятся для горячего пути парсера. На отсутствующей/несовпавшей
/// группе возвращают defaultValue, а не бросают.
/// </summary>
public static class ParsingExtensions
{
    /// <summary>Значение группы или <paramref name="defaultValue"/>, если группа не совпала.</summary>
    public static string GetGroupValue(this Match match, string groupName, string defaultValue = "")
    {
        var group = match.Groups[groupName];
        return group.Success ? group.Value : defaultValue;
    }

    /// <summary>Целое из группы (по ValueSpan, без аллокации) или <paramref name="defaultValue"/>.</summary>
    public static int GetGroupInt(this Match match, string groupName, int defaultValue = 0)
        => int.TryParse(match.Groups[groupName].ValueSpan, out var result) ? result : defaultValue;

    /// <summary>Long из группы (по ValueSpan, без аллокации) или <paramref name="defaultValue"/>.</summary>
    public static long GetGroupLong(this Match match, string groupName, long defaultValue = 0)
        => long.TryParse(match.Groups[groupName].ValueSpan, out var result) ? result : defaultValue;
}
