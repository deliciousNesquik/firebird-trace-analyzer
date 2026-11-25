// Данный файл это плагины для приложения Firebird Trace Analyzer
// Ниже описываются классы TemplateSortPlugin и TemplateFilterPlugin,
// которые реализуют интерфейсы ISortPlugin и IFilterPlugin соответственно.
// Для создания своего плагина используйте шаблон и руководствуйтесь инструкциями.

// Полное руководство (что это, как грузится, где брать типы, сборка и установка) — в README.md.

using FirebirdTraceAnalyzer.Interfaces.Plugins;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceAnalyzer.Services.Sorting;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.Events;

namespace TemplatePlugin;

/// <summary>
/// Пример плагина сортировки. Требует public-класс без параметров в конструкторе.
/// Показывает ОБА способа задать порядок: компаратор (полный контроль) и KeySelector
/// (ключ — простое значение; быстрее и проще, направление учитывает сервис).
/// </summary>
public class TemplateSortPlugin : ISortPlugin
{
    public string Id => "template_sort_plugin";
    public string Name => "Template Sort (Plugin)";
    public string Author => "system";
    public string Version => "1.0.0";

    /// <summary>Варианты сортировки плагина: SortDescriptor(id, displayName, comparer, isDefault, category, displayOrder).</summary>
    public IEnumerable<SortDescriptor> GetSorts()
    {
        // Способ 1: компаратор — произвольная логика сравнения пары событий (см. метод ниже).
        yield return new SortDescriptor(
            "template_sort_execute_time",
            "Execute time (Plugin)",
            CompareByExecuteTime,
            false,
            "Analytics",
            2);

        // Способ 2: KeySelector — когда порядок выражается ОДНИМ простым значением (число/строка/дата).
        // Ключ извлекается ОДИН раз на событие (decorate-sort-undecorate), а не на каждом из
        // O(N·logN) сравнений. Сервис сортировки при этом сам:
        //   - применяет флаг descending (компаратор плагина НЕ вызывается);
        //   - сохраняет стабильность при равных ключах;
        //   - события с null-ключом ставит в КОНЕЦ списка, направление это не переворачивает
        //     (здесь null = «событие не StatementFinish — не участвует в порядке»).
        // Сравнение ключей: однотипные IComparable — естественный CompareTo;
        // разнотипные — по ToString() (ordinal). Компаратор в конструкторе обязателен, но пока
        // задан KeySelector — не вызывается; передаём рабочую заглушку на тот же ключ.
        yield return new SortDescriptor(
            "template_sort_logical_reads",
            "Logical reads (Plugin)",
            CompareByLogicalReads,
            false,
            "Analytics",
            3)
        {
            KeySelector = e => e is StatementFinishEvent s ? s.Performance.ReadCount : (object?)null,
        };
    }

    /// <summary>
    /// Компаратор: &lt;0 если <paramref name="a"/> раньше <paramref name="b"/>, 0 — равны, &gt;0 — позже.
    /// Флаг <paramref name="descending"/> обязателен к учёту (обычно инвертированием знака результата).
    /// Поля конкретного события достаём через сопоставление с образцом.
    /// </summary>
    private static int CompareByExecuteTime(EventBase a, EventBase b, bool descending)
    {
        static int ExecuteMs(EventBase e) =>
            e is StatementFinishEvent s ? s.Performance.ExecuteMs : -1;

        var result = ExecuteMs(a).CompareTo(ExecuteMs(b));

        return descending ? -result : result;
    }

    /// <summary>Тот же контракт для «Logical reads» — fallback-компаратор сортировки с KeySelector.</summary>
    private static int CompareByLogicalReads(EventBase a, EventBase b, bool descending)
    {
        static int Reads(EventBase e) =>
            e is StatementFinishEvent s ? s.Performance.ReadCount : -1;

        var result = Reads(a).CompareTo(Reads(b));

        return descending ? -result : result;
    }
}

/// <summary>Пример плагина фильтрации. Требует public-класс без параметров в конструкторе.</summary>
public class TemplateFilterPlugin : IFilterPlugin
{
    public string Id => "template_filter_plugin";
    public string Name => "Template Filter (Plugin)";
    public string Author => "system";
    public string Version => "1.0.0";

    private const int SlowThresholdMs = 1000;

    /// <summary>
    /// Фильтры плагина. Для чистого предиката (Boolean, чекбокс вкл/выкл) берём короткий конструктор
    /// БЕЗ propertyPath: FilterDescriptor(id, displayName, predicate, category, displayOrder) —
    /// фильтрует только предикат. Полная форма с propertyPath нужна лишь для интерактивных типов.
    /// </summary>
    public IEnumerable<FilterDescriptor> GetFilters()
    {
        yield return new FilterDescriptor(
            "template_slow_statements",
            "Slow statements (>= 1000 ms)",
            IsSlowStatement,
            "Analytics",
            2);
    }

    /// <summary>Предикат фильтра: true — событие остаётся (когда фильтр активен), false — скрывается.</summary>
    private static bool IsSlowStatement(EventBase e)
        => e is StatementFinishEvent s && s.Performance.ExecuteMs >= SlowThresholdMs;
}

/// <summary>
/// Пример фильтра с ПАРАМЕТРОМ, настраиваемым пользователем в рантайме: «все StatementFinish на
/// WAIT-транзакции, у которых время исполнения попадает в заданный диапазон». Тип NumericRange
/// рисует редактор From/To; предикат читает ЖИВЫЕ границы дескриптора при каждом применении.
/// Полное объяснение — в разделе «Runtime-tunable filters» документации SDK.
/// </summary>
public class TemplateTunableFilterPlugin : IFilterPlugin
{
    public string Id => "template_tunable_filter_plugin";
    public string Name => "Template Tunable Filter (Plugin)";
    public string Author => "system";
    public string Version => "1.0.0";

    public IEnumerable<FilterDescriptor> GetFilters()
    {
        // NumericRange → редактор From/To; MinValue != null → редактор виден сразу.
        // propertyPath ('Performance.ExecuteMs') задаёт подпись и авто-подбор границ по данным —
        // САМУ фильтрацию делает предикат ниже, а не propertyPath.
        var descriptor = new FilterDescriptor(
            "template_slow_wait_statements",
            "Slow WAIT statements",
            FilterType.NumericRange,
            "Performance.ExecuteMs",
            _ => true,                 // плейсхолдер: реальный предикат ставим ниже (нужен дескриптор для замыкания)
            "Analytics",
            3)
        {
            MinValue = 0,
            MaxValue = 100_000,
        };

        // Предикат читает ЖИВОЕ состояние дескриптора: пользователь правит From/To — при следующем
        // применении фильтра видны новые границы (ApplyFilters пересобирает предикаты каждый раз).
        descriptor.UpdatePredicate(e => Match(e, descriptor));

        yield return descriptor;
    }

    /// <summary>Оставить событие, если это StatementFinish на WAIT-транзакции и ExecuteMs в границах [From, To].</summary>
    private static bool Match(EventBase e, FilterDescriptor filter)
    {
        if (e is not StatementFinishEvent s)
            return false;
        if (!string.Equals(s.Transaction?.LockMode, "WAIT", StringComparison.OrdinalIgnoreCase))
            return false;

        var ms = s.Performance.ExecuteMs;
        if (AsInt(filter.CurrentMinValue) is { } from && ms < from)
            return false;
        if (AsInt(filter.CurrentMaxValue) is { } to && ms > to)
            return false;
        return true;
    }

    // Границы приходят из TwoWay-биндинга TextBox — могут быть int ЛИБО строкой; непарсимое = «нет границы».
    private static int? AsInt(object? value) => value switch
    {
        null => null,
        int i => i,
        _ => int.TryParse(value.ToString(), out var n) ? n : null,
    };
}
