using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встраиваемый вид окна настроек (показывается в <c>DialogHost</c>).
/// Резолвится из <c>SettingsWindowViewModel</c> через <c>ViewLocator</c>
/// (<c>SettingsWindowViewModel</c> → <c>SettingsWindowView</c>).
/// <para>
/// Туннелирующий KeyDown перехватывает клавиши РАНЬШЕ фокусных элементов и отдаёт их
/// записи горячих клавиш (<see cref="SettingsWindowViewModel.HandleRecordKey"/>): иначе
/// TextBox'ы съели бы печатные клавиши, и записать сочетание вида Ctrl+K было бы невозможно.
/// Вне режима записи — no-op, обычная клавиатурная навигация работает как раньше.
/// </para>
/// </summary>
public partial class SettingsWindowView : UserControl
{
    public SettingsWindowView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel vm && vm.HandleRecordKey(e.Key, e.KeyModifiers))
            e.Handled = true;
    }
}
