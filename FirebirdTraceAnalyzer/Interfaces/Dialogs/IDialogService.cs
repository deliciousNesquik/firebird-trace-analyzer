namespace FirebirdTraceAnalyzer.Interfaces.Dialogs;

/// <summary>
/// Represents a service for managing dialogs in the application.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Returns the currently open dialog view model, or <c>null</c> if no dialog is open.
    /// </summary>
    object? CurrentDialog { get; }

    
    // Показывает диалог в оверлее и асинхронно ждёт результат. Отмена (Esc / клик по фону /
    // <see cref="IDialogViewModel.CloseRequested"/> с <c>null</c>) возвращает <c>default</c>.
    
    
    /// <summary>
    /// Shows a dialog in an overlay and asynchronously waits for the result.
    /// Cancellation (Esc / click on the background / <see cref="IDialogViewModel.CloseRequested"/> with <c>null</c>) returns <c>default</c>.
    /// </summary>
    /// <param name="viewModel">The view model for the dialog to show.</param>
    /// <typeparam name="TResult">The type of the result expected from the dialog.</typeparam>
    /// <returns>A task representing the asynchronous operation, with a nullable result.</returns>
    Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel viewModel);

    /// <summary>
    /// Close the currently open dialog, if any. This will cause <see cref="ShowDialogAsync{TResult}"/> to return <c>default</c>.
    /// </summary>
    void Cancel();
}
