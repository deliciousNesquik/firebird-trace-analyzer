using Avalonia.Controls;
using Avalonia.Interactivity;
using FirebirdTraceAnalyzer.ViewModels;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.UserControls;

/// <summary>
/// Секция отображения событий трассировки, вынесенная из MainWindow для разделения
/// ответственности. DataContext наследуется от хост-окна (<c>MainWindowViewModel</c>).
/// </summary>
public partial class EventsSection : UserControl
{
    public EventsSection()
    {
        InitializeComponent();

        // Клик по кнопке открытия инспектора (Classes="open-inspector") в шапке любой карточки
        // всплывает сюда. Одна подписка на всю секцию — карточек 15 типов и они переиспользуются
        // при прокрутке.
        AddHandler(Button.ClickEvent, OnCardActionClick, RoutingStrategies.Bubble);
    }

    /// <summary>Кнопка открытия инспектора в шапке карточки события.</summary>
    private void OnCardActionClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button button
            && button.Classes.Contains("open-inspector")
            && button.DataContext is EventBase evt)
        {
            OpenInspectorFor(evt);
            e.Handled = true;
        }
    }

    private void OpenInspectorFor(EventBase evt)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (vm.OpenEventInspectorCommand.CanExecute(evt))
            vm.OpenEventInspectorCommand.Execute(evt);
    }
}
