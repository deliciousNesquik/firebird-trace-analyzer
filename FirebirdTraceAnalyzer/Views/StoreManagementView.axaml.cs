using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встраиваемый вид диалога управления хранилищем событий (показывается в <c>DialogHost</c>).
/// Резолвится из <c>StoreManagementViewModel</c> через <c>ViewLocator</c>.
/// </summary>
public partial class StoreManagementView : UserControl
{
    public StoreManagementView()
    {
        InitializeComponent();
    }
}
