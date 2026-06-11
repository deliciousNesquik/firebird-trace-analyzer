using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FirebirdTraceAnalyzer.UserControls;

/// <summary>
/// In-window меню приложения, вынесенное из MainWindow для разделения ответственности.
/// DataContext наследуется от хост-окна (<c>MainWindowViewModel</c>).
/// </summary>
public partial class MainMenuView : UserControl
{
    public MainMenuView()
    {
        InitializeComponent();
    }
}
