using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встраиваемый вид окна настроек (показывается в <c>DialogHost</c>).
/// Резолвится из <c>SettingsWindowViewModel</c> через <c>ViewLocator</c>
/// (<c>SettingsWindowViewModel</c> → <c>SettingsWindowView</c>).
/// </summary>
public partial class SettingsWindowView : UserControl
{
    public SettingsWindowView()
    {
        InitializeComponent();
    }
}
