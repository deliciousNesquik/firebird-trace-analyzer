using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встраиваемый вид диалога восстановления сессии (показывается в <c>DialogHost</c>).
/// Резолвится из <c>SessionRecoveryViewModel</c> через <c>ViewLocator</c>.
/// </summary>
public partial class SessionRecoveryView : UserControl
{
    public SessionRecoveryView()
    {
        InitializeComponent();
    }
}
