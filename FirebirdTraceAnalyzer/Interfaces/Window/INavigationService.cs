using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces.Window;

/// <summary>
/// Навигация из ViewModel без прямого создания окон/резолва из App.Services. Диалоговые VM
/// резолвятся из DI и показываются модально; отдельные окна создаёт сам сервис (VM не трогает View).
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Резолвит <typeparamref name="TViewModel"/> из DI, даёт настроить (в т.ч. асинхронно —
    /// загрузка данных, подписка на события) и показывает модальным диалогом в оверлее, возвращая результат.
    /// </summary>
    Task<TResult?> ShowDialogAsync<TViewModel, TResult>(Func<TViewModel, Task>? configure = null)
        where TViewModel : class, IDialogViewModel;

    /// <summary>
    /// Открывает окно «Инспектор события». Создание VM и окна инкапсулировано здесь — ViewModel
    /// главного окна больше не создаёт <c>Window</c> напрямую (соблюдение MVVM).
    /// </summary>
    void ShowEventInspector(EventBase evt, IReadOnlyList<EventBase> chain);
}
