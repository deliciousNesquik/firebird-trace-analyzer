using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FirebirdTraceAnalyzer.UserControls;

/// <summary>
/// Секция со списком карточек загруженных trace-файлов, вынесенная из MainWindow
/// для разделения ответственности. DataContext наследуется от хост-окна (<c>MainWindowViewModel</c>).
/// </summary>
public partial class FileCardsSection : UserControl
{
    public FileCardsSection()
    {
        InitializeComponent();
    }
}
