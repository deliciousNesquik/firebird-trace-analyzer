using Avalonia.Controls;
using Avalonia.Input;
using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Встраиваемый вид диалога выбора файлов на удалённом сервере (показывается в <c>DialogHost</c>).
/// Резолвится из <c>RemoteFileSelectionViewModel</c> через <c>ViewLocator</c>.
/// </summary>
public partial class RemoteFileSelectionView : UserControl
{
    public RemoteFileSelectionView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Клик по строке файла (не только по чекбоксу) переключает выбор. Чекбокс в шаблоне —
    /// только индикатор (<c>IsHitTestVisible=False</c>), поэтому единственный путь переключения —
    /// этот обработчик; двойного тоггла нет. Реагируем только на левую кнопку.
    /// </summary>
    private void OnFileRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed &&
            sender is Control { DataContext: RemoteFileInfo file })
        {
            file.IsSelected = !file.IsSelected;
        }
    }
}
