using Avalonia.Controls;
using Avalonia.Interactivity;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Единый редактор отчётов: слева живое превью, справа инспектор атрибутов. Заменяет пару
/// окон «дизайнер + превью». Возвращает сохранённый <c>ReportTemplate</c> через <c>ShowDialog</c>.
/// </summary>
public partial class ReportEditorWindow : Window
{
    public ReportEditorWindow()
    {
        InitializeComponent();
    }

    public ReportEditorWindow(ReportDesignerViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Успешное сохранение закрывает редактор и возвращает шаблон в главное окно.
        viewModel.TemplateSaved += (_, template) => Close(template);

        // Стартовая отрисовка превью по текущим настройкам (сессия уже передана владельцем).
        viewModel.MarkPreviewDirty();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
