namespace FirebirdTraceAnalyzer.Interfaces.Dialogs;

/// <summary>
/// Represents a view model for a dialog, which can request to close itself and provide a result.
/// </summary>
public interface IDialogViewModel
{
    /// <summary>
    /// The dialog requests to close itself. The argument is the result of the dialog (<c>null</c> is treated as cancellation).
    /// </summary>
    event EventHandler<object?>? CloseRequested;
}
