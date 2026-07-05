using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встроенное окно управления плагинами (in-window overlay). DataContext — PluginsViewModel.
/// </summary>
public partial class PluginsView : UserControl
{
    public PluginsView()
    {
        InitializeComponent();
    }
}
