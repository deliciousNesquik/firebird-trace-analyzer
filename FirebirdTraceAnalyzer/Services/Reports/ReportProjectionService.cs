using System.Globalization;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Services.Reports;

/// <summary>
/// Строит таблицу отчёта из событий. Без группировки (GroupByFields пуст) — одна строка на
/// событие (повторяет исходное поведение экспортёров). С группировкой — одна строка на группу:
/// колонки-ключи (Field/GroupKey) берутся из первого события группы, колонки-агрегаты
/// (Aggregate) считаются редьюсером над всеми событиями группы.
/// </summary>
public sealed class ReportProjectionService : IReportProjectionService
{
    // Разделитель частей составного ключа группировки (маловероятен в данных).
    private const char KeySeparator = '\u001f';

    private readonly IEventPropertyAccessor _propertyAccessor;

    public ReportProjectionService(IEventPropertyAccessor propertyAccessor)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
    }

    public ReportTable BuildTable(ReportTemplate template, IReadOnlyList<EventBase> events)
    {
        var fields = template.Body.VisibleFields
            .OrderBy(f => f.Order)
            .ToList();

        var columns = fields
            .Select(f => new ReportColumn(BuildColumnHeader(f), f.Format, f.WidthPercent, f.Alignment))
            .ToList();

        var groupByPaths = template.Body.GroupByFields;
        var isGrouped = groupByPaths is { Count: > 0 };

        var rows = isGrouped
            ? BuildGroupedRows(fields, groupByPaths!, events, template.Body.SortByColumn, template.SortDescending)
            : BuildEventRows(fields, events);

        // Для сгруппированного отчёта лимит применяется к строкам-группам ПОСЛЕ агрегации и
        // сортировки (топ-N групп). Для негруппированного лимит уже применён к событиям выше по
        // конвейеру (PrepareEventsForReport), поэтому здесь строки не режем.
        if (isGrouped && template.EventLimit is > 0)
            rows = rows.Take(template.EventLimit.Value).ToList();

        return new ReportTable(columns, rows);
    }

    /// <summary>
    /// Заголовок колонки для вывода: обычное поле — как есть; агрегат — с приставкой функции
    /// (напр. «Error count (Count)»); ключ группировки — с приставкой «(group key)».
    /// Внутренняя сортировка/логика по-прежнему работают с исходным DisplayName поля.
    /// </summary>
    private static string BuildColumnHeader(EventField field) => field.Kind switch
    {
        ColumnKind.GroupKey => $"{field.DisplayName} (group key)",
        ColumnKind.Aggregate => $"{field.DisplayName} ({AggregateLabel(field.Aggregate)})",
        _ => field.DisplayName
    };

    private static string AggregateLabel(AggregateFunction? aggregate) => aggregate switch
    {
        AggregateFunction.Count => "Count",
        AggregateFunction.CountDistinct => "Count distinct",
        AggregateFunction.Sum => "Sum",
        AggregateFunction.Average => "Avg",
        AggregateFunction.Min => "Min",
        AggregateFunction.Max => "Max",
        _ => "Aggregate"
    };

    /// <summary>Одна строка на событие — значения свойств по PropertyPath.</summary>
    private List<IReadOnlyList<object?>> BuildEventRows(
        IReadOnlyList<EventField> fields,
        IReadOnlyList<EventBase> events)
    {
        var rows = new List<IReadOnlyList<object?>>(events.Count);

        foreach (var evt in events)
        {
            var cells = new object?[fields.Count];

            for (var i = 0; i < fields.Count; i++)
                cells[i] = _propertyAccessor.GetValue(evt, fields[i].PropertyPath);

            rows.Add(cells);
        }

        return rows;
    }

    /// <summary>
    /// Одна строка на группу (GROUP BY по GroupByFields). По умолчанию порядок групп — по первому
    /// появлению; если задан <paramref name="sortByColumn"/> (DisplayName видимой колонки) — строки
    /// сортируются по значению этой колонки (включая агрегаты) в нужном направлении.
    /// </summary>
    private List<IReadOnlyList<object?>> BuildGroupedRows(
        IReadOnlyList<EventField> fields,
        IReadOnlyList<string> groupByPaths,
        IReadOnlyList<EventBase> events,
        string? sortByColumn,
        bool sortDescending)
    {
        var groups = events.GroupBy(e => BuildGroupKey(e, groupByPaths));

        var rows = new List<IReadOnlyList<object?>>();

        foreach (var group in groups)
        {
            var groupEvents = group.ToList();
            var first = groupEvents[0];

            var cells = new object?[fields.Count];

            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];

                cells[i] = field.Kind == ColumnKind.Aggregate
                    ? Reduce(field, groupEvents)
                    // Field/GroupKey: значение постоянно в пределах группы — берём из первого события.
                    : _propertyAccessor.GetValue(first, field.PropertyPath);
            }

            rows.Add(cells);
        }

        return SortRows(rows, fields, sortByColumn, sortDescending);
    }

    /// <summary>
    /// Сортирует строки-группы по колонке. Идентификатор колонки — её Order (уникален даже при
    /// дублирующихся DisplayName); для обратной совместимости со старыми шаблонами, где сохранён
    /// DisplayName, — откат на матч по имени. Колонка не найдена — без сортировки.
    /// </summary>
    private static List<IReadOnlyList<object?>> SortRows(
        List<IReadOnlyList<object?>> rows,
        IReadOnlyList<EventField> fields,
        string? sortByColumn,
        bool sortDescending)
    {
        if (string.IsNullOrWhiteSpace(sortByColumn))
            return rows;

        var index = -1;

        // Новые шаблоны: Order колонки (число).
        if (int.TryParse(sortByColumn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var order))
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (fields[i].Order == order)
                {
                    index = i;
                    break;
                }
            }
        }

        // Старые шаблоны (или коллизия): матч по DisplayName.
        if (index < 0)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (string.Equals(fields[i].DisplayName, sortByColumn, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }
        }

        if (index < 0)
            return rows;

        var sorted = sortDescending
            ? rows.OrderByDescending(r => r[index], CellComparer.Instance)
            : rows.OrderBy(r => r[index], CellComparer.Instance);

        return sorted.ToList();
    }

    /// <summary>Сравнивает значения ячеек: числа как числа, одинаковые IComparable-типы напрямую, иначе по строке.</summary>
    private sealed class CellComparer : IComparer<object?>
    {
        public static readonly CellComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            if (IsNumeric(x) && IsNumeric(y))
                return ToDouble(x).CompareTo(ToDouble(y));

            if (x.GetType() == y.GetType() && x is IComparable comparable)
                return comparable.CompareTo(y);

            return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNumeric(object value) =>
            value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    private string BuildGroupKey(EventBase evt, IReadOnlyList<string> paths)
    {
        var parts = new string[paths.Count];

        for (var i = 0; i < paths.Count; i++)
            parts[i] = _propertyAccessor.GetValue(evt, paths[i])?.ToString() ?? string.Empty;

        return string.Join(KeySeparator, parts);
    }

    /// <summary>Считает агрегат над группой событий. Группа всегда непустая (создаётся GroupBy).</summary>
    private object? Reduce(EventField field, IReadOnlyList<EventBase> group)
    {
        switch (field.Aggregate)
        {
            case AggregateFunction.Count:
                return group.Count;

            case AggregateFunction.CountDistinct:
                return group
                    .Select(e => _propertyAccessor.GetValue(e, field.PropertyPath)?.ToString())
                    .Distinct()
                    .Count();

            case AggregateFunction.Sum:
                return group.Sum(e => ToDouble(_propertyAccessor.GetValue(e, field.PropertyPath)));

            case AggregateFunction.Average:
                return group.Count == 0
                    ? 0d
                    : group.Sum(e => ToDouble(_propertyAccessor.GetValue(e, field.PropertyPath))) / group.Count;

            case AggregateFunction.Min:
                return MinMax(field, group, takeMin: true);

            case AggregateFunction.Max:
                return MinMax(field, group, takeMin: false);

            default:
                return null;
        }
    }

    /// <summary>
    /// Min/Max по исходным значениям (сохраняем оригинальный тип — DateTime/число и т.п., чтобы
    /// корректно применился Format колонки). Несравнимые/смешанные значения пропускаются.
    /// </summary>
    private object? MinMax(EventField field, IReadOnlyList<EventBase> group, bool takeMin)
    {
        object? best = null;

        foreach (var evt in group)
        {
            var value = _propertyAccessor.GetValue(evt, field.PropertyPath);

            if (value is not IComparable comparable)
                continue;

            if (best == null)
            {
                best = value;
                continue;
            }

            try
            {
                var cmp = comparable.CompareTo(best);
                if (takeMin ? cmp < 0 : cmp > 0)
                    best = value;
            }
            catch
            {
                // несравнимые типы внутри одного поля — игнорируем
            }
        }

        return best;
    }

    private static double ToDouble(object? value)
    {
        if (value is null)
            return 0d;

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0d;
        }
    }
}
