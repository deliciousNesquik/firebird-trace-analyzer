using Avalonia.Controls;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Services;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// A3: NavigationService.ShowDialogAsync резолвит VM из DI, применяет configure и показывает через
/// IDialogService, возвращая его результат. (ShowEventInspector создаёт реальное окно Avalonia —
/// в headless-тесте не проверяется.)
/// </summary>
public sealed class NavigationServiceTests
{
    private sealed class FakeVm : IDialogViewModel
    {
        public event EventHandler<object?>? CloseRequested;
        public bool Configured;
        public void RaiseCloseToSilenceWarning() => CloseRequested?.Invoke(this, null);
    }

    private sealed class SingleServiceProvider(object instance) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == instance.GetType() ? instance : null;
    }

    private sealed class FakeDialogService(object? result) : IDialogService
    {
        public object? ShownViewModel { get; private set; }
        public object? CurrentDialog => ShownViewModel;

        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel viewModel)
        {
            ShownViewModel = viewModel;
            // Как реальный DialogService: заданный результат либо default (отмена).
            return Task.FromResult(result is TResult typed ? typed : default);
        }

        public void Cancel() { }
    }

    private sealed class NullWindowProvider : IWindowProvider
    {
        public TopLevel? GetCurrent() => null;
    }

    [Fact]
    public async Task ShowDialogAsync_ResolvesConfiguresAndShows_ReturningResult()
    {
        var vm = new FakeVm();
        var dialog = new FakeDialogService(result: true);
        var nav = new NavigationService(new SingleServiceProvider(vm), dialog, new NullWindowProvider());

        var result = await nav.ShowDialogAsync<FakeVm, bool>(v =>
        {
            v.Configured = true;
            return Task.CompletedTask;
        });

        Assert.True(result);                     // результат проброшен из IDialogService
        Assert.Same(vm, dialog.ShownViewModel);  // показан именно резолвленный из DI экземпляр
        Assert.True(vm.Configured);              // configure применён до показа
    }

    [Fact]
    public async Task ShowDialogAsync_WithoutConfigure_StillShows()
    {
        var vm = new FakeVm();
        var dialog = new FakeDialogService(result: null);
        var nav = new NavigationService(new SingleServiceProvider(vm), dialog, new NullWindowProvider());

        var result = await nav.ShowDialogAsync<FakeVm, bool>();

        Assert.False(result);                    // default для bool
        Assert.Same(vm, dialog.ShownViewModel);
        Assert.False(vm.Configured);
    }
}
