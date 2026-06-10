namespace FirebirdTraceParser.Parsing.Utils;

using System.Globalization;
using System.Text.RegularExpressions;
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
            var tableName = Extract(line, 0, pos.TableNameEnd).Trim();
            if (string.IsNullOrEmpty(tableName)) return null;
            
            return new PerformanceTableItem
            {
                TableName = tableName,
                NaturalCount = ParseIntSafe(Extract(line, pos.NaturalStart, pos.NaturalEnd)),
                IndexCount = ParseIntSafe(Extract(line, pos.IndexStart, pos.IndexEnd)),
                UpdateCount = ParseIntSafe(Extract(line, pos.UpdateStart, pos.UpdateEnd)),
                InsertCount = ParseIntSafe(Extract(line, pos.InsertStart, pos.InsertEnd)),
                DeleteCount = ParseIntSafe(Extract(line, pos.DeleteStart, pos.DeleteEnd)),
                BackoutCount = ParseIntSafe(Extract(line, pos.BackoutStart, pos.BackoutEnd)),
                PurgeCount = ParseIntSafe(Extract(line, pos.PurgeStart, pos.PurgeEnd)),
                ExpungeCount = ParseIntSafe(Extract(line, pos.ExpungeStart, pos.ExpungeEnd))
            };
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to parse performance table row: {Line}", line);
            return null;
        }
    }
    
    private static string Extract(string line, int start, int end) =>
        start >= line.Length ? string.Empty : line.Substring(start, Math.Min(end, line.Length) - start);
    
    private static int ParseIntSafe(string value) =>
        string.IsNullOrWhiteSpace(value) ? 0 : int.Parse(value.Trim(), CultureInfo.InvariantCulture);
}