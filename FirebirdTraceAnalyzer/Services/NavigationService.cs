using Avalonia.Controls;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.ViewModels;
using FirebirdTraceAnalyzer.Views;
using FirebirdTraceParser.Models.Events;
using Microsoft.Extensions.DependencyInjection;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Навигация из ViewModel. Диалоговые VM резолвятся из DI-контейнера (а не из статического
/// App.Services), окна создаёт сам сервис. Инъекция <see cref="IServiceProvider"/> здесь уместна:
/// это фабрика/композиция навигации, а не бизнес-VM с глобальным сервис-локатором.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly IDialogService _dialogService;
    private readonly IWindowProvider _windowProvider;

    public NavigationService(IServiceProvider services, IDialogService dialogService, IWindowProvider windowProvider)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));
    }

    public async Task<TResult?> ShowDialogAsync<TViewModel, TResult>(Func<TViewModel, Task>? configure = null)
        where TViewModel : class, IDialogViewModel
    {
        var viewModel = _services.GetRequiredService<TViewModel>();

        if (configure is not null)
            await configure(viewModel);

        return await _dialogService.ShowDialogAsync<TResult>(viewModel);
    }

    public void ShowEventInspector(EventBase evt, IReadOnlyList<EventBase> chain)
    {
        var window = new EventInspectorWindow(new EventInspectorViewModel(evt, chain));

        var owner = _windowProvider.GetCurrent() as Window;
        if (owner is not null)
            window.Show(owner);
        else
            window.Show();
    }
}
