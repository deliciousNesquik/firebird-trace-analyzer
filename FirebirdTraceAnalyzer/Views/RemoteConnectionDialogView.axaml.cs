using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встраиваемый вид диалога подключения к удалённому серверу (показывается в <c>DialogHost</c>).
/// Резолвится из <c>RemoteConnectionDialogViewModel</c> через <c>ViewLocator</c>.
/// </summary>
public partial class RemoteConnectionDialogView : UserControl
{
    public RemoteConnectionDialogView()
    {
        InitializeComponent();
    }
}
