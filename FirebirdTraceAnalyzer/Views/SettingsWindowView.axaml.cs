using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встраиваемый вид окна настроек (показывается в <c>DialogHost</c>).
/// Резолвится из <c>SettingsWindowViewModel</c> через <c>ViewLocator</c>
/// (<c>SettingsWindowViewModel</c> → <c>SettingsWindowView</c>).
/// <para>
/// Перехват клавиш для записи горячих клавиш вешается на САМ TopLevel (туннельная фаза,
/// корень маршрута). Хук на самом UserControl не работал: при старте записи кнопка «Set»
/// исчезает (IsVisible=!IsRecording), Avalonia возвращает фокус наверх, фокусный элемент
/// выходит из поддерева вида — туннельный маршрут TopLevel→цель перестаёт проходить через
/// вид, и клавиши не перехватывались вовсе. На TopLevel маршрут проходит всегда; вне режима
/// записи хук — прозрачный наблюдатель (ничего не помечает как обработанное).
/// </para>
/// </summary>
public partial class SettingsWindowView : UserControl
{
    private TopLevel? _topLevel;

    public SettingsWindowView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnTunnelKeyDown);
        _topLevel = null;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel vm && vm.HandleRecordKey(e.Key, e.KeyModifiers))
            e.Handled = true;
    }
}
