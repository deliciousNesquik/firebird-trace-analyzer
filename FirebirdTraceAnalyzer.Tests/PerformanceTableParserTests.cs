using System.Text.RegularExpressions;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.ValueObjects;
using FirebirdTraceParser.Parsing.Utils;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Регрессия на разбор таблицы производительности. Ранее условие «конец таблицы = строка без
/// отступов» обрывало парсинг на первой же строке данных (они flush-left) → таблица всегда пустая.
/// Блок ниже — реальный фрагмент трейса Firebird.
/// </summary>
public sealed class PerformanceTableParserTests
{
    private static readonly IReadOnlyDictionary<string, Regex> Rules = new Dictionary<string, Regex>
    {
        ["performance_table_header"] = new Regex(
            @"^Table\s+Natural\s+Index\s+Update\s+Insert\s+Delete\s+Backout\s+Purge\s+Expunge\s*$")
    };

    // Реальные строки (flush-left), внутренние пробелы сохранены; хвостовые не важны для fixed-width.
    private static readonly string[] Lines =
    [
        "Table                              Natural     Index    Update    Insert    Delete   Backout     Purge   Expunge",
        "****************************************************************************************************************",
        "RDB$INDICES                                       25",
        "RDB$RELATIONS                                      2",
        "RDB$FORMATS                                        1",
        "RDB$RELATION_CONSTRAINTS                           4",
        "HIS$FIELDS_VALUES                                                      1",
        "HIS$MAIN                                           1                   1",
        "HIS$RELATION_FIELDS                                1",
        "HIS$RELATIONS                                      1",
        "KKM$CDNSERVERS                                    11         1",
        "" // пустая строка — конец таблицы
    ];

    private static int Total(PerformanceTableItem i) =>
        i.NaturalCount + i.IndexCount + i.UpdateCount + i.InsertCount +
        i.DeleteCount + i.BackoutCount + i.PurgeCount + i.ExpungeCount;

    [Fact]
    public void ParsesRealTable_AllRows_NotEmpty()
    {
        var table = PerformanceTableParser.ParsePerformanceTable(Lines, 0, Rules, new ParsingContext());

        Assert.NotNull(table);
        Assert.NotNull(table!.Items);
        Assert.Equal(9, table.Items!.Count);

        // Итог по каждой таблице (устойчиво к тому, в какую именно колонку попал счётчик).
        var totals = table.Items.ToDictionary(i => i.TableName, Total);
        Assert.Equal(25, totals["RDB$INDICES"]);
        Assert.Equal(2, totals["RDB$RELATIONS"]);
        Assert.Equal(1, totals["RDB$FORMATS"]);
        Assert.Equal(4, totals["RDB$RELATION_CONSTRAINTS"]);
        Assert.Equal(1, totals["HIS$FIELDS_VALUES"]);
        Assert.Equal(2, totals["HIS$MAIN"]);
        Assert.Equal(1, totals["HIS$RELATION_FIELDS"]);
        Assert.Equal(1, totals["HIS$RELATIONS"]);
        Assert.Equal(12, totals["KKM$CDNSERVERS"]);
    }
}
