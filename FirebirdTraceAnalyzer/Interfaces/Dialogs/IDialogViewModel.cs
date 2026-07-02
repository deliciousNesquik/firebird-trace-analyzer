namespace FirebirdTraceAnalyzer.Interfaces.Dialogs;

/// <summary>
/// ViewModel диалога, встраиваемого в модальный оверлей (<c>DialogHost</c>).
/// Сообщает хосту о желании закрыться и отдаёт результат.
/// </summary>
public interface IDialogViewModel
{
    /// <summary>
    /// Диалог просит себя закрыть. Аргумент — результат диалога (<c>null</c> трактуется как отмена).
    /// </summary>
    event EventHandler<object?>? CloseRequested;
}
