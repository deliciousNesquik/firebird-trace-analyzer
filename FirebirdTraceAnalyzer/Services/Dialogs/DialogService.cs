using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;

namespace FirebirdTraceAnalyzer.Services.Dialogs;

/// <inheritdoc cref="IDialogService" />
public sealed partial class DialogService : ObservableObject, IDialogService
{
    /// <summary>Верхний диалог-VM стека; биндится оверлеем <c>DialogHost</c>.</summary>
    [ObservableProperty]
    private object? _currentDialog;

    // Стек открытых диалогов: диалог может открываться ПОВЕРХ другого (напр. редактор отчёта
    // поверх окна управления шаблонами). Показывается верхний; при его закрытии возвращается
    // предыдущий. Верх стека — последний элемент списка.
    private readonly List<Entry> _stack = new();

    private sealed record Entry(IDialogViewModel ViewModel, TaskCompletionSource<object?> Completion);

    public async Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var entry = new Entry(viewModel, new TaskCompletionSource<object?>());
        viewModel.CloseRequested += OnCloseRequested;

        _stack.Add(entry);
        CurrentDialog = viewModel;

        var result = await entry.Completion.Task;
        return result is TResult typed ? typed : default;
    }

    /// <summary>Отменяет ВЕРХНИЙ диалог стека (Esc / клик по фону).</summary>
    public void Cancel()
    {
        if (_stack.Count > 0)
            Complete(_stack[^1].ViewModel, null);
    }

    private void OnCloseRequested(object? sender, object? result)
    {
        if (sender is IDialogViewModel viewModel)
            Complete(viewModel, result);
    }

    private void Complete(IDialogViewModel viewModel, object? result)
    {
        var index = _stack.FindIndex(e => ReferenceEquals(e.ViewModel, viewModel));
        if (index < 0)
            return;

        var entry = _stack[index];
        _stack.RemoveAt(index);
        entry.ViewModel.CloseRequested -= OnCloseRequested;

        // Показываем новый верх стека (или ничего, если стек пуст).
        CurrentDialog = _stack.Count > 0 ? _stack[^1].ViewModel : null;

        entry.Completion.TrySetResult(result);
    }
}
