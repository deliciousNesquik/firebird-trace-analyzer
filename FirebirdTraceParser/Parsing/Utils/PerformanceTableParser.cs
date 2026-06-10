namespace FirebirdTraceParser.Parsing.Utils;

using System.Globalization;
using System.Text.RegularExpressions;
using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.ValueObjects;
using NLog;

/// <summary>
/// Парсер таблицы производительности (fixed-width колонки по заголовку).
/// </summary>
public static class PerformanceTableParser
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    
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
    
    public static PerformanceTable? ParsePerformanceTable(IReadOnlyList<string> lines, int startIndex,
        IReadOnlyDictionary<string, Regex> rules)
    {
        var items = new List<PerformanceTableItem>();
        ColumnPositions? positions = null;
        bool inTable = false;

        for (var idx = startIndex; idx < lines.Count; idx++)
        {
            var line = lines[idx];
            // Заголовок
            if (rules["performance_table_header"].IsMatch(line))
            {
                positions = DetectColumnPositions(line);
                inTable = true;
                Logger.Trace("Performance table header detected");
                continue;
            }
            
            if (line.Contains("***"))
                continue;
            
            if (inTable && positions is not null && !string.IsNullOrWhiteSpace(line))
            {
                // Признак конца таблицы: строка без отступов (начинается с имени таблицы)
                if (!char.IsWhiteSpace(line[0]) && line.TrimStart() == line)
                    break;
                
                var item = ParseRow(line, positions);
                if (item is not null)
                    items.Add(item);
            }
        }
        
        return items.Count > 0 ? new PerformanceTable { Items = items } : null;
    }
    
    private static ColumnPositions DetectColumnPositions(string header)
    {
        int tableIdx = header.IndexOf("Table", StringComparison.Ordinal);
        int naturalIdx = header.IndexOf("Natural", StringComparison.Ordinal);
        int indexIdx = header.IndexOf("Index", StringComparison.Ordinal);
        int updateIdx = header.IndexOf("Update", StringComparison.Ordinal);
        int insertIdx = header.IndexOf("Insert", StringComparison.Ordinal);
        int deleteIdx = header.IndexOf("Delete", StringComparison.Ordinal);
        int backoutIdx = header.IndexOf("Backout", StringComparison.Ordinal);
        int purgeIdx = header.IndexOf("Purge", StringComparison.Ordinal);
        int expungeIdx = header.IndexOf("Expunge", StringComparison.Ordinal);
        
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
    
    private static PerformanceTableItem? ParseRow(string line, ColumnPositions pos)
    {
        try
        {
            var span = line.AsSpan();

            var tableName = Slice(span, 0, pos.TableNameEnd).Trim();
            if (tableName.IsEmpty) return null;

            return new PerformanceTableItem
            {
                TableName = StringPool.Intern(tableName.ToString()),
                NaturalCount = ParseIntSafe(Slice(span, pos.NaturalStart, pos.NaturalEnd)),
                IndexCount = ParseIntSafe(Slice(span, pos.IndexStart, pos.IndexEnd)),
                UpdateCount = ParseIntSafe(Slice(span, pos.UpdateStart, pos.UpdateEnd)),
                InsertCount = ParseIntSafe(Slice(span, pos.InsertStart, pos.InsertEnd)),
                DeleteCount = ParseIntSafe(Slice(span, pos.DeleteStart, pos.DeleteEnd)),
                BackoutCount = ParseIntSafe(Slice(span, pos.BackoutStart, pos.BackoutEnd)),
                PurgeCount = ParseIntSafe(Slice(span, pos.PurgeStart, pos.PurgeEnd)),
                ExpungeCount = ParseIntSafe(Slice(span, pos.ExpungeStart, pos.ExpungeEnd))
            };
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to parse performance table row: {Line}", line);
            return null;
        }
    }

    // Срез фиксированной колонки без аллокации (семантика идентична прежнему Substring)
    private static ReadOnlySpan<char> Slice(ReadOnlySpan<char> line, int start, int end) =>
        start >= line.Length ? ReadOnlySpan<char>.Empty : line.Slice(start, Math.Min(end, line.Length) - start);

    private static int ParseIntSafe(ReadOnlySpan<char> value)
    {
        value = value.Trim();
        return value.IsEmpty ? 0 : int.Parse(value, CultureInfo.InvariantCulture);
    }
}