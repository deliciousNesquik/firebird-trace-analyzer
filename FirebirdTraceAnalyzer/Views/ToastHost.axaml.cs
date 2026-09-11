using Avalonia.Controls;
using Avalonia.Interactivity;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Панель тост-уведомлений (снизу-справа). DataContext — <see cref="IToastService"/>.
/// Анимация въезда/выезда описана стилями в ToastHost.axaml; здесь только закрытие по крестику.
/// </summary>
public partial class ToastHost : UserControl
{
    public ToastHost() => InitializeComponent();

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ToastItem item } && DataContext is IToastService service)
            service.Dismiss(item);
    }
}
