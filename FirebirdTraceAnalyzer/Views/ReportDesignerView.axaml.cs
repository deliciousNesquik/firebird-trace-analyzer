using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Редактор отчётов (превью + инспектор) как in-window overlay. DataContext —
/// <c>ReportDesignerViewModel</c> (IDialogViewModel), показывается через IDialogService/DialogHost
/// поверх окна управления шаблонами.
/// </summary>
public partial class ReportDesignerView : UserControl
{
    public ReportDesignerView()
    {
        InitializeComponent();
    }
}
