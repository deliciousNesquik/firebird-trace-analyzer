using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Мини-панель прогресса загрузки, закреплённая снизу-справа в главном окне.
/// DataContext — <c>DownloadProgressViewModel</c> (передаётся из MainWindow как ActiveDownload).
/// </summary>
public partial class DownloadDockView : UserControl
{
    public DownloadDockView()
    {
        InitializeComponent();
    }
}
