using Avalonia.Controls;
using Avalonia.Interactivity;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

public partial class EventInspectorWindow : Window
{
    public EventInspectorWindow()
    {
        InitializeComponent();
    }

    public EventInspectorWindow(EventInspectorViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
