using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
    }
}
