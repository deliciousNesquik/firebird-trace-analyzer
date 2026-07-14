using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// Переиспользуемый диалог подтверждения (in-window overlay, стек диалогов).
/// Возвращает <c>true</c> при подтверждении, <c>false</c> при отмене/Esc/клике по фону.
/// Может показать список деталей (например, что именно будет удалено).
/// </summary>
public partial class ConfirmDialogViewModel : ViewModelBase, IDialogViewModel
{
    public string Title { get; }
    public string Message { get; }
    public IReadOnlyList<string> Details { get; }
    public bool HasDetails => Details.Count > 0;
    public string ConfirmText { get; }
    public string CancelText { get; }

    /// <summary>Стилизовать кнопку подтверждения как «опасную» (красную).</summary>
    public bool IsDanger { get; }

    public event EventHandler<object?>? CloseRequested;

    public ConfirmDialogViewModel(
        string title,
        string message,
        IReadOnlyList<string>? details = null,
        string confirmText = "OK",
        string cancelText = "Cancel",
        bool isDanger = false)
    {
        Title = title;
        Message = message;
        Details = details ?? Array.Empty<string>();
        ConfirmText = confirmText;
        CancelText = cancelText;
        IsDanger = isDanger;
    }

    public ConfirmDialogViewModel()
        : this("Confirm", "Are you sure?")
    {
    }

    [RelayCommand]
    private void Confirm() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
