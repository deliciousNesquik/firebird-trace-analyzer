using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встраиваемый вид диалога «Статистика парсера» (показывается в <c>DialogHost</c>).
/// Резолвится из <c>ParserStatisticsViewModel</c> через <c>ViewLocator</c>.
/// </summary>
public partial class ParserStatisticsView : UserControl
{
    public ParserStatisticsView()
    {
        InitializeComponent();
    }
}
