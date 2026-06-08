using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceAnalyzer.ViewModels;
using FirebirdTraceParser.Enums;

namespace FirebirdTraceAnalyzer.Services.Filtering;

public partial class FilterDescriptor : ViewModelBase
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public int Priority { get; }
    public FilterType FilterType { get; }
    public string PropertyPath { get; }

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
        int priority = 100)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        FilterType = filterType;
        PropertyPath = propertyPath;
        FilterPredicate = filterPredicate ?? throw new ArgumentNullException(nameof(filterPredicate));
        Category = category;
        Priority = priority;
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
            value.IsSelected = false;

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
        FilteredValues.Clear();

        var query = AvailableValues.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(ValueSearchText))
        {
            query = query.Where(v =>
                v.DisplayName.Contains(ValueSearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Сортируем: сначала выбранные, потом по количеству
        foreach (var item in query
                     .OrderByDescending(v => v.IsSelected)
                     .ThenByDescending(v => v.Count))
        {
            FilteredValues.Add(item);
        }
    }
}