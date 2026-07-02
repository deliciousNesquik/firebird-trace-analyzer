using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встраиваемый вид диалога выбора файлов на удалённом сервере (показывается в <c>DialogHost</c>).
/// Резолвится из <c>RemoteFileSelectionViewModel</c> через <c>ViewLocator</c>.
/// </summary>
public partial class RemoteFileSelectionDialogView : UserControl
{
    public RemoteFileSelectionDialogView()
    {
        InitializeComponent();
    }
}
