using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.ViewModels;
using FirebirdTraceParser.Enums;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Services.Filtering;

public partial class FilterDescriptor : ViewModelBase
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Category { get; }

    /// <summary>
    /// Порядок в СПИСКЕ фильтров (меньше — выше). Только позиция пункта в UI-списке; к самой
    /// фильтрации отношения не имеет.
    /// </summary>
    public int DisplayOrder { get; }
    public FilterType FilterType { get; }
    public string PropertyPath { get; }

    /// <summary>
    /// true — фильтр «содержит подстроку» (<see cref="FilterType.TextSearch"/>): в UI вместо списка
    /// значений/диапазона показывается поле ввода, а фильтрация идёт по <see cref="SearchText"/>.
    /// </summary>
    public bool IsTextSearch => FilterType == FilterType.TextSearch;

    public ObservableCollection<FilterValueItem> AvailableValues { get; } = [];
    public ObservableCollection<FilterValueItem> FilteredValues { get; } = [];

    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }

    [ObservableProperty]
    private object? _currentMinValue;

    [ObservableProperty]
    private object? _currentMaxValue;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private Func<EventBase, bool> _filterPredicate;

    /// Поиск внутри значений фильтра
    [ObservableProperty]
    private string _valueSearchText = string.Empty;

    public FilterDescriptor(
        string id,
        string displayName,
        FilterType filterType,
        string propertyPath,
        Func<EventBase, bool> filterPredicate,
        string category = "General",
        int displayOrder = 100)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        FilterType = filterType;
        PropertyPath = propertyPath;
        FilterPredicate = filterPredicate ?? throw new ArgumentNullException(nameof(filterPredicate));
        Category = category;
        DisplayOrder = displayOrder;
    }

    /// <summary>
    /// Фильтр с произвольным предикатом (Boolean-стиль: чекбокс вкл/выкл). <c>propertyPath</c> не нужен —
    /// фильтрует только предикат, а не путь к свойству. Для интерактивных типов (списки значений и
    /// диапазоны) берите основной конструктор с явным <c>propertyPath</c>: по нему приложение подбирает
    /// значения/границы и решает, какой редактор показать.
    /// </summary>
    public FilterDescriptor(
        string id,
        string displayName,
        Func<EventBase, bool> filterPredicate,
        string category = "General",
        int displayOrder = 100)
        : this(id, displayName, FilterType.Boolean, string.Empty, filterPredicate, category, displayOrder)
    {
    }

    public void UpdatePredicate(Func<EventBase, bool> newPredicate)
    {
        FilterPredicate = newPredicate ?? throw new ArgumentNullException(nameof(newPredicate));
    }

    public void Reset()
    {
        IsActive = false;
        CurrentMinValue = MinValue;
        CurrentMaxValue = MaxValue;
        SearchText = null;
        ValueSearchText = string.Empty;

        foreach (var value in AvailableValues)
        {
            value.IsSelected = false;
            value.IsExcluded = false;
        }

        UpdateFilteredValues();
    }

    /// Инициализация FilteredValues (вызывать после заполнения AvailableValues)
    public void InitializeFilteredValues()
    {
        UpdateFilteredValues();
    }

    /// Обновление отфильтрованного списка при поиске
    partial void OnValueSearchTextChanged(string value)
    {
        UpdateFilteredValues();
    }

    private void UpdateFilteredValues()
    {
        var query = AvailableValues.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(ValueSearchText))
        {
            query = query.Where(v =>
                v.DisplayName.Contains(ValueSearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Сортируем: сначала отмеченные (включённые/исключённые), потом по количеству
        var ordered = query
            .OrderByDescending(v => v.IsSelected || v.IsExcluded)
            .ThenByDescending(v => v.Count)
            .ToList();

        // Если состав не изменился — не трогаем коллекцию: иначе каждый ввод в поиске значений даёт
        // лавину Clear/Add-уведомлений и перерисовку списка впустую.
        if (FilteredValues.SequenceEqual(ordered))
            return;

        FilteredValues.Clear();
        foreach (var item in ordered)
            FilteredValues.Add(item);
    }
}