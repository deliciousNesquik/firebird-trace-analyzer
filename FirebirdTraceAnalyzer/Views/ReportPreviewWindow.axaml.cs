using Avalonia.Controls;
using Avalonia.Interactivity;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

public partial class ReportPreviewWindow : Window
{
    public ReportPreviewWindow()
    {
        InitializeComponent();
    }

    public ReportPreviewWindow(ReportPreviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}