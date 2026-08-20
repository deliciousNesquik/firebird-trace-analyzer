using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Window;

/// <summary>
/// Defines a service for navigating between windows and dialogs in the application.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Show dialog window for the given view model type and return the result.
    /// </summary>
    /// <param name="configure">A function to configure the view model before showing the dialog.</param>
    /// <typeparam name="TViewModel">The type of the view model.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <returns>The result of the dialog or null if canceled.</returns>
    Task<TResult?> ShowDialogAsync<TViewModel, TResult>(Func<TViewModel, Task>? configure = null)
        where TViewModel : class, IDialogViewModel;

    /// <summary>
    /// Show event inspector window for the given event and its chain.
    /// </summary>
    /// <param name="evt">The event to inspect.</param>
    /// <param name="chain">The chain of events.</param>
    void ShowEventInspector(EventBase evt, IReadOnlyList<EventBase> chain);
}
