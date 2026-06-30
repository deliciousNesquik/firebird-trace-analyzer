using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Services.Filtering;

/// <summary>
/// Представляет одно значение для фильтра (например, пункт в чекбоксе).
/// </summary>
public partial class FilterValueItem : ViewModelBase
{
    
    /// <summary>Внутреннее значение (например, enum или строка)</summary>
    public object Value { get; }
    
    /// <summary>Отображаемое имя</summary>
    public string DisplayName { get; }
    
    /// <summary>Количество событий с этим значением</summary>
    public int Count { get; set; }
    
    /// <summary>Значение включено в выборку (показывать только включённые)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecked))]
    public partial bool IsSelected { get; set; }

    /// <summary>Значение исключено из выборки. Взаимоисключающе с <see cref="IsSelected"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecked))]
    public partial bool IsExcluded { get; set; }

    /// <summary>
    /// Состояние для трёхпозиционного CheckBox (IsThreeState):
    /// false = не выбран, true = выбран, null = исключён.
    /// Цикл клика false→true→null совпадает с «не выбран → выбран → исключён».
    /// </summary>
    public bool? IsChecked
    {
        get => IsExcluded ? null : IsSelected;
        set
        {
            switch (value)
            {
                case true:
                    IsSelected = true;
                    IsExcluded = false;
                    break;
                case null:
                    IsSelected = false;
                    IsExcluded = true;
                    break;
                default:
                    IsSelected = false;
                    IsExcluded = false;
                    break;
            }
        }
    }

    public FilterValueItem(object value, string displayName, int count = 0)
    {
        Value = value;
        DisplayName = displayName;
        Count = count;
    }
}