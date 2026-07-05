// Данный файл это плагины для приложения Firebird Trace Analyzer
// Ниже описываются классы TemplateSortPlugin и TemplateFilterPlugin,
// которые реализуют интерфейсы ISortPlugin и IFilterPlugin соответственно.
// Для создания своего плагина используйте шаблон и руководствуйтесь инструкциями.

// Полное руководство (что это, как грузится, где брать типы, сборка и установка) — в README.md.

using FirebirdTraceAnalyzer.Interfaces.Plugins;
using FirebirdTraceAnalyzer.Services.Sorting;
using FirebirdTraceAnalyzer.Services.Filtering;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.Events;

namespace TemplatePlugin;

/// <summary>Пример плагина сортировки. Требует public-класс без параметров в конструкторе.</summary>
public class TemplateSortPlugin : ISortPlugin
{
    public string Id => "template_sort_plugin";
    public string Name => "Template Sort (Plugin)";
    public string Author => "system";
    public string Version => "1.0.0";

    /// <summary>Варианты сортировки плагина: SortDescriptor(id, displayName, comparer, isDefault, category, displayOrder).</summary>
    public IEnumerable<SortDescriptor> GetSorts()
    {
        yield return new SortDescriptor(
            "template_sort_execute_time",
            "Execute time (Plugin)",
            CompareByExecuteTime,
            false,
            "Analytics",
            2);
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
}

/// <summary>Пример плагина фильтрации. Требует public-класс без параметров в конструкторе.</summary>
public class TemplateFilterPlugin : IFilterPlugin
{
    public string Id => "template_filter_plugin";
    public string Name => "Template Filter (Plugin)";
    public string Author => "system";
    public string Version => "1.0.0";

    private const int SlowThresholdMs = 1000;

    /// <summary>Фильтры плагина: FilterDescriptor(id, displayName, filterType, propertyPath, predicate, category, displayOrder).</summary>
    public IEnumerable<FilterDescriptor> GetFilters()
    {
        yield return new FilterDescriptor(
            "template_slow_statements",
            "Slow statements (≥ 1000 ms)",
            FilterType.Boolean,
            "Performance.ExecuteMs",
            IsSlowStatement,
            "Analytics",
            2);
    }

    /// <summary>Предикат фильтра: true — событие остаётся (когда фильтр активен), false — скрывается.</summary>
    private static bool IsSlowStatement(EventBase e)
        => e is StatementFinishEvent s && s.Performance.ExecuteMs >= SlowThresholdMs;
}
