using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;

namespace FirebirdTraceAnalyzer.UserControls;

/// <summary>
/// Оверлей для модальных диалогов внутри главного окна. DataContext — <see cref="IDialogService"/>.
/// Показывает активный диалог поверх затемнения; Esc и клик по фону отменяют диалог.
/// <para>
/// Анимация универсальна (для любого диалога, даже пустого), т.к. анимируется сам контейнер-карточка
/// по центру, а не конкретный диалог. Показ: скрим затемняется, карточка «съезжает сверху» с fade.
/// Закрытие: контент убирается сразу (<see cref="PART_Content"/> IsVisible=false), а пустая рамка
/// карточки гаснет и уезжает. Мгновенное скрытие контента нужно из-за контрола <c>Svg</c>
/// (Avalonia.Svg.Skia): он игнорирует <c>Opacity</c> предка (иконки не затухают и «залипают» на fade),
/// но исчезает вместе с контентом при снятии видимости — без «хвоста».
/// </para>
/// </summary>
public partial class DialogHost : UserControl
{
    private static readonly TransformOperations Above = TransformOperations.Parse("translateY(-24px)");
    private static readonly TransformOperations Center = TransformOperations.Parse("translateY(0px)");
    private static readonly TimeSpan CardDuration = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(200);

    private readonly Transitions _cardTransitions;
    private INotifyPropertyChanged? _service;
    private int _generation;

    public DialogHost()
    {
        InitializeComponent();

        _cardTransitions =
        [
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = CardDuration, Easing = new CubicEaseOut() },
            new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = CardDuration, Easing = new CubicEaseOut() },
        ];

        PART_Card.Transitions = _cardTransitions;

        // Tunnel — чтобы поймать Esc раньше, чем его обработает поле ввода внутри диалога.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_service != null)
            _service.PropertyChanged -= OnServicePropertyChanged;

        _service = DataContext as INotifyPropertyChanged;

        if (_service != null)
            _service.PropertyChanged += OnServicePropertyChanged;

        Sync();
    }

    private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IDialogService.CurrentDialog))
            Sync();
    }

    private void Sync()
    {
        var dialog = (DataContext as IDialogService)?.CurrentDialog;
        if (dialog != null)
            Open(dialog);
        else
            Close();
    }

    private void Open(object dialog)
    {
        _generation++; // отменяем возможное отложенное закрытие
        PART_Content.Content = dialog;
        PART_Content.IsVisible = true; // могли скрыть на прошлом закрытии

        IsVisible = true;
        PART_Scrim.Opacity = 1;

        // Карточку мгновенно ставим в скрытое состояние (transition отключены), затем на следующем кадре —
        // в показанное: transition проигрывает slide-in. Так анимируется и первый показ, и смена диалога
        // в стеке (вложенные диалоги).
        PART_Card.Transitions = null;
        PART_Card.Opacity = 0;
        PART_Card.RenderTransform = Above;

        Dispatcher.UIThread.Post(() =>
        {
            PART_Card.Transitions = _cardTransitions;
            PART_Card.Opacity = 1;
            PART_Card.RenderTransform = Center;
        }, DispatcherPriority.Render);
    }

    private void Close()
    {
        if (!IsVisible)
            return;

        // Контент (в т.ч. SVG-иконки, которые не уважают Opacity) убираем сразу, чтобы не «залипал».
        // Дальше гаснет только пустая рамка карточки + затемнение.
        PART_Content.IsVisible = false;

        PART_Card.Transitions ??= _cardTransitions;
        PART_Scrim.Opacity = 0;
        PART_Card.Opacity = 0;
        PART_Card.RenderTransform = Above;

        var generation = ++_generation;
        DispatcherTimer.RunOnce(() =>
        {
            // Прячем и чистим, только если за время анимации не открыли новый диалог.
            if (generation != _generation || (DataContext as IDialogService)?.CurrentDialog != null)
                return;

            IsVisible = false;
            PART_Content.Content = null;
        }, CloseDuration);
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
