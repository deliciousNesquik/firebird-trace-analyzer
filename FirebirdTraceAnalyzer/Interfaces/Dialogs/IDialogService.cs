namespace FirebirdTraceAnalyzer.Interfaces.Dialogs;

/// <summary>
/// Показ модальных диалогов внутри главного окна (через оверлей <c>DialogHost</c>) с возвратом
/// результата, как у <c>Window.ShowDialog&lt;T&gt;</c>, но без отдельного ОС-окна.
/// </summary>
public interface IDialogService
{
    /// <summary>Активный диалог-VM (для биндинга оверлея) или <c>null</c>, если диалог не открыт.</summary>
    object? CurrentDialog { get; }

    /// <summary>
    /// Показывает диалог в оверлее и асинхронно ждёт результат. Отмена (Esc / клик по фону /
    /// <see cref="IDialogViewModel.CloseRequested"/> с <c>null</c>) возвращает <c>default</c>.
    /// </summary>
    Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel viewModel);

    /// <summary>Отменить текущий диалог (Esc / клик по затемнению).</summary>
    void Cancel();
}
