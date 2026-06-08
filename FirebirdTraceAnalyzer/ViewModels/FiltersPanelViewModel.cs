using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceParser.Attributes;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Filtering;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

public partial class FiltersPanelViewModel(
    Action onFiltersApplied,
    IEventPropertyAccessor propertyAccessor) : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Action _onFiltersApplied = onFiltersApplied ?? throw new ArgumentNullException(nameof(onFiltersApplied));
    private readonly IEventPropertyAccessor _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
    private readonly Dictionary<string, FilterState> _filterStates = new();

    /// <summary>Все доступные фильтры</summary>
    public ObservableCollection<FilterDescriptor> AvailableFilters { get; } = [];

    /// <summary>Фильтры, сгруппированные по категориям</summary>
    public ObservableCollection<IGrouping<string, FilterDescriptor>> FiltersByCategory { get; } = [];

    /// <summary>Количество активных фильтров</summary>
    [ObservableProperty] private int _activeFiltersCount;

    /// <summary>Есть ли изменения (нужно применить фильтры)</summary>
    [ObservableProperty] private bool _hasUnappliedChanges;

    /// <summary>
    /// ЯВНОЕ применение фильтров (по кнопке)
    /// </summary>
    [RelayCommand]
    private void ApplyFilters()
    {
        _onFiltersApplied();
        HasUnappliedChanges = false;
        Logger.Info("All activity filters apply manualy");
    }

    private class FilterState
    {
        public bool IsActive { get; set; }
        public HashSet<object> SelectedValues { get; set; } = new();
        public object? CurrentMinValue { get; set; }
        public object? CurrentMaxValue { get; set; }
    }

    /// <summary>
    ///     Загружает фильтры и подписывается на изменения.
    /// </summary>
    // Сохраняем состояние перед обновлением
    public void LoadFilters(IEnumerable<FilterDescriptor> filters)
    {
        // Сохраняем текущее состояние
        SaveCurrentFilterStates();

        // Отписываемся от старых
        foreach (var filter in AvailableFilters)
        {
            filter.PropertyChanged -= OnFilterPropertyChanged;
            foreach (var value in filter.AvailableValues)
                value.PropertyChanged -= OnFilterValueChanged;
        }

        AvailableFilters.Clear();
        FiltersByCategory.Clear();

        // Добавляем новые фильтры
        foreach (var filter in filters)
        {
            AvailableFilters.Add(filter);

            // Восстанавливаем состояние
            RestoreFilterState(filter);

            // Подписываемся на изменения
            filter.PropertyChanged += OnFilterPropertyChanged;
            foreach (var value in filter.AvailableValues)
                value.PropertyChanged += OnFilterValueChanged;
        }

        // Группируем по категориям
        var grouped = AvailableFilters
            .GroupBy(f => f.Category)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
            FiltersByCategory.Add(group);

        UpdateActiveFiltersCount();
        HasUnappliedChanges = false;
    }

    private void SaveCurrentFilterStates()
    {
        foreach (var filter in AvailableFilters)
            _filterStates[filter.Id] = new FilterState
            {
                IsActive = filter.IsActive,
                SelectedValues = filter.AvailableValues
                    .Where(v => v.IsSelected)
                    .Select(v => v.Value)
                    .ToHashSet(),
                CurrentMinValue = filter.CurrentMinValue,
                CurrentMaxValue = filter.CurrentMaxValue
            };
    }

    private void RestoreFilterState(FilterDescriptor filter)
    {
        if (!_filterStates.TryGetValue(filter.Id, out var state))
            return;

        // Восстанавливаем IsActive
        filter.IsActive = state.IsActive;

        // Восстанавливаем выбранные значения
        foreach (var item in filter.AvailableValues)
            if (state.SelectedValues.Contains(item.Value))
                item.IsSelected = true;

        // Восстанавливаем диапазоны
        filter.CurrentMinValue = state.CurrentMinValue ?? filter.MinValue;
        filter.CurrentMaxValue = state.CurrentMaxValue ?? filter.MaxValue;
    }

    /// <summary>
    ///     Обновляет счётчики фильтров на основе отфильтрованных событий
    /// </summary>
    public void UpdateFilterCounts(IEnumerable<EventBase> filteredEvents)
    {
        var eventsList = filteredEvents.ToList();

        foreach (var filter in AvailableFilters)
        {
            // Пропускаем активные фильтры (не пересчитываем их счётчики)
            if (filter.IsActive)
                continue;

            // Обновляем счётчики для Enum/String фильтров
            if (filter.FilterType is FilterType.EnumMultiSelect or FilterType.StringMultiSelect)
                UpdateMultiSelectCounts(filter, eventsList);
        }

        Logger.Debug("Filter counters updated for {Count} events", eventsList.Count);
    }

    private void UpdateMultiSelectCounts(FilterDescriptor filter, List<EventBase> events)
    {
        var valueCounts = new Dictionary<object, int>();

        foreach (var evt in events)
        {
            var value = _propertyAccessor.GetValue(evt, filter.PropertyPath);
            if (value != null)
            {
                valueCounts.TryGetValue(value, out var count);
                valueCounts[value] = count + 1;
            }
        }

        // Обновляем счётчики в UI
        foreach (var item in filter.AvailableValues)
            item.Count = valueCounts.TryGetValue(item.Value, out var count) ? count : 0;
    }

    private void OnFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilterDescriptor.IsActive))
        {
            UpdateActiveFiltersCount();
            MarkAsChanged(); // Помечаем, что нужно применить
        }
        else if (e.PropertyName is nameof(FilterDescriptor.CurrentMinValue) or nameof(FilterDescriptor.CurrentMaxValue))
        {
            MarkAsChanged();
        }
    }

    private void OnFilterValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilterValueItem.IsSelected))
        {
            MarkAsChanged();
        }
    }

    private void MarkAsChanged()
    {
        HasUnappliedChanges = true;
    }

    [RelayCommand]
    private void ResetAllFilters()
    {
        foreach (var filter in AvailableFilters) filter.Reset();

        UpdateActiveFiltersCount();
        HasUnappliedChanges = true; // Нужно применить сброс

        Logger.Info("All filters reset");
    }

    [RelayCommand]
    private void ToggleFilter(FilterDescriptor filter)
    {
        filter.IsActive = !filter.IsActive;
    }

    private void UpdateActiveFiltersCount()
    {
        ActiveFiltersCount = AvailableFilters.Count(f => f.IsActive);
    }
}