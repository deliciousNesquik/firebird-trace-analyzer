using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.Services.Shortcuts;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>Заголовок категории на странице горячих клавиш (строка локализации на момент сборки списка).</summary>
public sealed record ShortcutHeaderViewModel(string Title);

/// <summary>
/// Строка действия на странице «Горячие клавиши»: заголовок, текущий жест (платформозависимый
/// текст от KeyGesture.ToString), состояние записи сочетания и конфликт-баннер «Занять».
/// Логика записи/применения живёт в <see cref="SettingsWindowViewModel"/> — строка только хранит
/// состояние отображения.
/// </summary>
public sealed partial class ShortcutRowViewModel : ViewModelBase
{
    public ShortcutRowViewModel(ShortcutAction action, string gestureDisplay)
    {
        ActionId = action.Id;
        Title = action.DisplayName;
        IsRebindable = action.IsRebindable;
        _gestureDisplay = gestureDisplay;
    }

    /// <summary>Стабильный id действия («App.OpenLocalFile», «Report.Run:&lt;id&gt;»).</summary>
    public string ActionId { get; }

    public string Title { get; }

    /// <summary>false — системная конвенция (⌘, в меню приложения macOS), запись запрещена.</summary>
    public bool IsRebindable { get; }

    /// <summary>Текущий жест (платформозависимый текст) или «—».</summary>
    [ObservableProperty]
    private string _gestureDisplay;

    /// <summary>Строка находится в режиме записи сочетания.</summary>
    [ObservableProperty]
    private bool _isRecording;

    /// <summary>Превью записываемого сочетания (до подтверждения Enter'ом).</summary>
    [ObservableProperty]
    private string? _pendingDisplay;

    /// <summary>Каноничный спецификатор записанного сочетания — то, что уйдёт в файл.</summary>
    public GestureSpec? PendingSpec { get; set; }

    /// <summary>Подсказка во время записи: «нужен модификатор» и т.п.</summary>
    [ObservableProperty]
    private string? _hintText;

    /// <summary>Записанное сочетание занято другим действием — ждём решения «Занять»/отмена.</summary>
    [ObservableProperty]
    private bool _hasConflict;

    /// <summary>Заголовок действия-владельца конфликтующего жеста.</summary>
    [ObservableProperty]
    private string? _conflictTitle;
}
