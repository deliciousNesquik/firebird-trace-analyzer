using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встроенное окно управления кастомными шаблонами отчётов. DataContext — ManageTemplatesViewModel.
/// Показывается как in-window overlay через IDialogService/DialogHost.
/// </summary>
public partial class ManageTemplatesView : UserControl
{
    public ManageTemplatesView()
    {
        InitializeComponent();
    }
}
