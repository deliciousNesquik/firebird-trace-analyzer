using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;

namespace FirebirdTraceAnalyzer.Services.Dialogs;

/// <inheritdoc cref="IDialogService" />
public sealed partial class DialogService : ObservableObject, IDialogService
{
    /// <summary>Активный диалог-VM; биндится оверлеем <c>DialogHost</c>.</summary>
    [ObservableProperty]
    private object? _currentDialog;

    private IDialogViewModel? _active;
    private TaskCompletionSource<object?>? _completion;

    public async Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        // Один диалог за раз: если что-то уже открыто — отменяем.
        if (_active is not null)
            Cancel();

        _active = viewModel;
        _completion = new TaskCompletionSource<object?>();
        viewModel.CloseRequested += OnCloseRequested;
        CurrentDialog = viewModel;

        var result = await _completion.Task;
        return result is TResult typed ? typed : default;
    }

    public void Cancel() => Complete(null);

    private void OnCloseRequested(object? sender, object? result) => Complete(result);

    private void Complete(object? result)
    {
        if (_active is null)
            return;

        _active.CloseRequested -= OnCloseRequested;
        _active = null;
        CurrentDialog = null;

        var completion = _completion;
        _completion = null;
        completion?.TrySetResult(result);
    }
}
