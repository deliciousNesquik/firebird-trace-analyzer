using Avalonia;
using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Core;

/// <summary>
/// Attached-свойство для установки <see cref="Grid.ColumnDefinitions"/> из строки-спецификации
/// (например "20*,50*,30*") через биндинг. Обычный биндинг на саму коллекцию ColumnDefinitions
/// в Avalonia не поддерживается, поэтому раскладку присваиваем императивно в обработчике.
/// Нужно, чтобы заголовок таблицы отчёта и все её строки делили ОДНУ раскладку колонок и ячейки
/// идеально выравнивались.
/// </summary>
public static class GridColumnsBehavior
{
    public static readonly AttachedProperty<string?> ColumnsProperty =
        AvaloniaProperty.RegisterAttached<Grid, string?>(
            "Columns", typeof(GridColumnsBehavior));

    static GridColumnsBehavior()
    {
        ColumnsProperty.Changed.AddClassHandler<Grid>((grid, e) =>
        {
            var spec = e.GetNewValue<string?>();

            grid.ColumnDefinitions = string.IsNullOrWhiteSpace(spec)
                ? new ColumnDefinitions()
                : ColumnDefinitions.Parse(spec);
        });
    }

    public static void SetColumns(Grid element, string? value) => element.SetValue(ColumnsProperty, value);

    public static string? GetColumns(Grid element) => element.GetValue(ColumnsProperty);
}
