using System.Text.RegularExpressions;
using FirebirdTraceParser.Parsing.Rules;
using FirebirdTraceParser.Parsing.Utils;

namespace FirebirdTraceParser.Tests;

/// <summary>
/// Тесты PerformanceTableParser напрямую (в обход хендлера) - на кастомных правилах,
/// которые shipped rules.json не допускает (полный header-regex требует все колонки).
/// </summary>
public sealed class PerformanceTableParserTests
{
    [Fact]
    public void Header_with_missing_column_parses_present_columns_missing_are_zero()
    {
        // Issue #38: частичный заголовок не роняет разбор - присутствующие колонки
        // парсятся, отсутствующие дают 0. До фикса Slice на отрицательных границах
        // бросал и терял всю строку таблицы.
        var rules = new Dictionary<string, Regex>(TestSupport.Rules)
        {
            ["performance_table_header"] = new(
                @"^Table\s+Natural\s+Index\s+Update\s*$", RegexOptions.None, TimeSpan.FromSeconds(1))
        };
        List<string> lines =
        [
            "Table      Natural     Index     Update",
            "RDB$INDICES      25     10"
        ];

        var table = new PerformanceTableParser(TestSupport.Logger)
            .ParsePerformanceTable(lines, 0, rules, TestSupport.NewContext());

        Assert.NotNull(table);
        var row = Assert.Single(table.Items);
        Assert.Equal("RDB$INDICE", row.TableName); // fixed-width: имя срезается по границе колонки
        Assert.Equal(25, row.NaturalCount);
        Assert.Equal(10, row.IndexCount);
        Assert.Equal(0, row.UpdateCount);  // колонки нет в заголовке -> срез пуст -> 0
        Assert.Equal(0, row.InsertCount);
    }

    [Fact]
    public void Trailing_junk_after_table_does_not_create_phantom_rows()
    {
        // Прямой вызов с усечённым header: таблица заканчивается на первой не-строке
        // (issue #39), мусорная строка не попадает фантомом.
        var rules = new Dictionary<string, Regex>(TestSupport.Rules)
        {
            ["performance_table_header"] = new(
                @"^Table\s+Natural\s+Index\s+Update\s*$", RegexOptions.None, TimeSpan.FromSeconds(1))
        };
        List<string> lines =
        [
            "Table      Natural     Index     Update",
            "RDB$INDICES      25     10     1",
            "some trailing text"
        ];

        var table = new PerformanceTableParser(TestSupport.Logger)
            .ParsePerformanceTable(lines, 0, rules, TestSupport.NewContext());

        Assert.NotNull(table);
        Assert.Single(table.Items);
        Assert.Equal("RDB$INDICE", table.Items[0].TableName); // не фантом "some trail..."
    }
}
