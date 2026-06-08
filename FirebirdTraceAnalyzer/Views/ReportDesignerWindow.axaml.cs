using Avalonia.Controls;
using Avalonia.Interactivity;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

public partial class ReportDesignerWindow : Window
{
    public ReportDesignerWindow()
    {
        InitializeComponent();
    }

    public ReportDesignerWindow(ReportDesignerViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Подписываемся на успешное сохранение
        viewModel.TemplateSaved += (_, template) =>
        {
            Close(template); // Закрываем окно и возвращаем сохранённый шаблон
        };

        // Предупреждение при закрытии с несохранёнными изменениями
        Closing += (_, e) =>
        {
            if (DataContext is ReportDesignerViewModel vm && vm.HasUnsavedChanges)
            {
                // Можно показать диалог подтверждения
                // Пока просто разрешаем закрытие
            }
        };
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}