using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;

namespace FirebirdTraceAnalyzer.UserControls;

/// <summary>
/// Оверлей для модальных диалогов внутри главного окна. DataContext — <see cref="IDialogService"/>.
/// Показывает активный диалог поверх затемнения; Esc и клик по фону отменяют диалог.
/// </summary>
public partial class DialogHost : UserControl
{
    public DialogHost()
    {
        InitializeComponent();

        // Tunnel — чтобы поймать Esc раньше, чем его обработает поле ввода внутри диалога.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is IDialogService { CurrentDialog: not null } service)
        {
            service.Cancel();
            e.Handled = true;
        }
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is IDialogService service)
            service.Cancel();
    }
}
