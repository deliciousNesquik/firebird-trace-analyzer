using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Мини-панель фоновых задач (снизу-справа в главном окне). DataContext — <c>IBackgroundTaskService</c>.
/// </summary>
public partial class BackgroundTasksView : UserControl
{
    public BackgroundTasksView()
    {
        InitializeComponent();
    }
}
