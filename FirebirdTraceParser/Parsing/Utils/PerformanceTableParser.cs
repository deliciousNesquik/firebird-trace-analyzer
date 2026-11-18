namespace FirebirdTraceParser.Parsing.Utils;

using System.Globalization;
using System.Text.RegularExpressions;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.ValueObjects;
using FirebirdTraceParser.Parsing.Rules;
using NLog;

/// <summary>
/// Парсер таблицы производительности (fixed-width колонки по заголовку).
/// </summary>
public sealed class PerformanceTableParser(ILogger logger) : IPerformanceTableParser
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private sealed record ColumnPositions(
        int TableNameEnd,
        int NaturalStart, int NaturalEnd,
        int IndexStart, int IndexEnd,
        int UpdateStart, int UpdateEnd,
        int InsertStart, int InsertEnd,
        int DeleteStart, int DeleteEnd,
        int BackoutStart, int BackoutEnd,
        int PurgeStart, int PurgeEnd,
        int ExpungeStart, int ExpungeEnd
    );
    
    public PerformanceTable? ParsePerformanceTable(IReadOnlyList<string> lines, int startIndex,
        IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
    {
        var items = new List<PerformanceTableItem>();
        ColumnPositions? positions = null;
        bool inTable = false;

        var headerRx = rules[RuleKeys.PerformanceTableHeader]; // lookup один раз вне цикла
        for (var idx = startIndex; idx < lines.Count; idx++)
        {
            var line = lines[idx];
            // Заголовок
            if (headerRx.IsMatch(line))
            {
                positions = DetectColumnPositions(line);
                inTable = true;
                _logger.Trace("Performance table header detected");
                continue;
            }
            
            if (line.Contains("***"))
                continue;

            if (inTable && positions is not null)
            {
                // Строки таблицы (имя таблицы + счётчики) идут flush-left и подряд; конец блока —
                // первая пустая строка. Прежнее условие «строка без отступов = конец» ошибочно
                // обрывало парсинг на самой первой строке данных (они тоже flush-left).
                if (string.IsNullOrWhiteSpace(line))
                    break;

                var item = ParseRow(line, positions, context);
                if (item is null)
                    break; // первая строка, не являющаяся строкой таблицы, заканчивает её (issue #39)
                items.Add(item);
            }
        }
        
        return items.Count > 0 ? new PerformanceTable { Items = items } : null;
    }
    
    private static ColumnPositions DetectColumnPositions(string header)
    {
        int naturalIdx = header.IndexOf("Natural", StringComparison.Ordinal);
        int indexIdx = header.IndexOf("Index", StringComparison.Ordinal);
        int updateIdx = header.IndexOf("Update", StringComparison.Ordinal);
        int insertIdx = header.IndexOf("Insert", StringComparison.Ordinal);
        int deleteIdx = header.IndexOf("Delete", StringComparison.Ordinal);
        int backoutIdx = header.IndexOf("Backout", StringComparison.Ordinal);
        int purgeIdx = header.IndexOf("Purge", StringComparison.Ordinal);
        int expungeIdx = header.IndexOf("Expunge", StringComparison.Ordinal);

        // Отсутствующая колонка (idx = -1) не фатальна: Slice даёт пустой срез, счётчик 0 -
        // парсер толерантен к подмножеству колонок (issue #38). Мусорных границ нет.
        return new ColumnPositions(
            TableNameEnd: naturalIdx - 1,
            NaturalStart: naturalIdx, NaturalEnd: indexIdx - 1,
            IndexStart: indexIdx, IndexEnd: updateIdx - 1,
            UpdateStart: updateIdx, UpdateEnd: insertIdx - 1,
            InsertStart: insertIdx, InsertEnd: deleteIdx - 1,
            DeleteStart: deleteIdx, DeleteEnd: backoutIdx - 1,
            BackoutStart: backoutIdx, BackoutEnd: purgeIdx - 1,
            PurgeStart: purgeIdx, PurgeEnd: expungeIdx - 1,
            ExpungeStart: expungeIdx, ExpungeEnd: header.Length
        );
    }
    
    private PerformanceTableItem? ParseRow(string line, ColumnPositions pos, ParsingContext context)
    {
        try
        {
            var span = line.AsSpan();

            var tableName = Slice(span, 0, pos.TableNameEnd).Trim();
            if (tableName.IsEmpty) return null;

            var natural = Slice(span, pos.NaturalStart, pos.NaturalEnd);
            var index = Slice(span, pos.IndexStart, pos.IndexEnd);
            var update = Slice(span, pos.UpdateStart, pos.UpdateEnd);
            var insert = Slice(span, pos.InsertStart, pos.InsertEnd);
            var delete = Slice(span, pos.DeleteStart, pos.DeleteEnd);
            var backout = Slice(span, pos.BackoutStart, pos.BackoutEnd);
            var purge = Slice(span, pos.PurgeStart, pos.PurgeEnd);
            var expunge = Slice(span, pos.ExpungeStart, pos.ExpungeEnd);

            // Строка таблицы обязана содержать хотя бы одно распознанное число (у реальных
            // строк это даже нули - "0" парсится). Ни одного числа - мусор после таблицы,
            // сигнал "таблица кончилась" (issue #39).
            if (!TryCount(natural, out var naturalCount)
                & !TryCount(index, out var indexCount)
                & !TryCount(update, out var updateCount)
                & !TryCount(insert, out var insertCount)
                & !TryCount(delete, out var deleteCount)
                & !TryCount(backout, out var backoutCount)
                & !TryCount(purge, out var purgeCount)
                & !TryCount(expunge, out var expungeCount))
                return null;

            return new PerformanceTableItem
            {
                TableName = context.Intern(tableName.ToString()),
                NaturalCount = naturalCount,
                IndexCount = indexCount,
                UpdateCount = updateCount,
                InsertCount = insertCount,
                DeleteCount = deleteCount,
                BackoutCount = backoutCount,
                PurgeCount = purgeCount,
                ExpungeCount = expungeCount
            };
        }
        catch (Exception ex)
        {
            // Не пишем сырой недоверенный ввод в лог — только длину и исключение.
            _logger.Warn(ex, "Failed to parse performance table row (len={Length})", line.Length);
            return null;
        }
    }

    // Срез фиксированной колонки без аллокации. Отсутствующая колонка (отрицательный start
    // от IndexOf -1, issue #38) и выход за длину строки дают пустой срез, а не исключение.
    private static ReadOnlySpan<char> Slice(ReadOnlySpan<char> line, int start, int end) =>
        start < 0 || start >= line.Length
            ? ReadOnlySpan<char>.Empty
            : line.Slice(start, Math.Max(0, Math.Min(end, line.Length) - start));

    private static bool TryCount(ReadOnlySpan<char> value, out int count) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
}