namespace FirebirdTraceAnalyzer.Services.Filtering;

/// <summary>
/// Результат O(N)-скана событий для фильтров: считается в фоне, применяется к дескрипторам на
/// UI-потоке (см. <see cref="IFilteringService.ScanFilterValues"/> / <see cref="IFilteringService.ApplyFilterValues"/>).
/// Разделение нужно, чтобы тяжёлый проход по событиям не блокировал UI.
/// </summary>
public sealed class FilterValueScan
{
    /// <summary>Id фильтра → (значение → количество) для мультиселект-фильтров (Enum/String/Boolean).</summary>
    public Dictionary<string, Dictionary<object, int>> MultiSelectCounts { get; } = new();

    /// <summary>Id фильтра → (min, max) для диапазонных фильтров (числа/даты).</summary>
    public Dictionary<string, (IComparable Min, IComparable Max)> Ranges { get; } = new();
}
