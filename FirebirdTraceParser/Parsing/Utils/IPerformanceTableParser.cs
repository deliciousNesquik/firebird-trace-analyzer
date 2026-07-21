using System.Text.RegularExpressions;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceParser.Parsing.Utils;

/// <summary>
/// Парсер таблицы производительности (fixed-width колонки по заголовку). Абстрагирован интерфейсом,
/// чтобы внедряться в обработчик и подменяться в тестах, а не вызываться статически.
/// </summary>
public interface IPerformanceTableParser
{
    PerformanceTable? ParsePerformanceTable(IReadOnlyList<string> lines, int startIndex,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context);
}
