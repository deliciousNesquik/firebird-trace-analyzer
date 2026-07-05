using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Services.Sorting;

/// <summary>
/// Описывает один вариант сортировки.
/// </summary>
public partial class SortDescriptor : ViewModelBase
{
    
    /// <summary>Уникальный идентификатор сортировки</summary>
    public string Id { get; }
    
    /// <summary>Отображаемое имя сортировки</summary>
    public string DisplayName { get; }
    
    /// <summary>Категория сортировки</summary>
    public string Category { get; }
    
    /// <summary>
    /// Порядок в СПИСКЕ сортировок (меньше — выше). Это только позиция пункта в UI-списке;
    /// к тому, как события сортируются, отношения не имеет. Для будущего мульти-сорта порядок
    /// применения задаётся порядком выбора пользователем (runtime), а не этим полем.
    /// </summary>
    public int DisplayOrder { get; }
    
    /// <summary>Функция сравнения событий для сортировки</summary>
    public Func<EventBase, EventBase, bool, int> Comparer { get; }
    
    /// <summary>Является ли сортировкой по умолчанию</summary>
    public bool IsDefault { get; init; }

    /// <summary>Выбрана ли эта сортировка в данный момент</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public SortDescriptor(
        string id,
        string displayName,
        Func<EventBase, EventBase, bool, int> comparer,
        bool isDefault,
        string category = "General",
        int displayOrder = 100)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        IsDefault = isDefault;
        Category = category;
        DisplayOrder = displayOrder;
    }
}