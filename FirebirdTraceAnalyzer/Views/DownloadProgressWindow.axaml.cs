using Avalonia.Controls;
using Avalonia.Interactivity;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

public partial class DownloadProgressWindow : Window
{
    public DownloadProgressWindow()
    {
        InitializeComponent();
    }

    public DownloadProgressWindow(DownloadProgressViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Закрытие окна во время загрузки не отменяет её: возврат в док-панель делает владелец
        // (MainWindowViewModel.PopOutDownload вешает свой обработчик Closing). Здесь ничего не
        // блокируем — иначе окно нельзя было бы закрыть.
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}