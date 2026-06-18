using Avalonia.Controls;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>Конструктор с ViewModel (для использования из кода).</summary>
    public SettingsWindow(SettingsWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Закрываем окно по запросу ViewModel. Результат диалога: были ли сохранены изменения.
        viewModel.CloseRequested += (_, changed) => Close(changed);
    }
}
