using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Живое превью отчёта (левая панель редактора). DataContext — <c>ReportPreviewViewModel</c>.
/// Рендерит ту же проекцию (<c>ReportProjectionService</c>), что и экспортёры, поэтому превью
/// совпадает с итоговым файлом (WYSIWYG). Таблица событий строится единым Grid, чтобы колонки
/// заголовка и строк были выровнены точно как в PDF.
/// </summary>
public partial class ReportPreviewView : UserControl
{
    // Цвета из PDF-экспорта (QuestPDF Material palette) — фиксированные, лист всегда белый.
    private static readonly IBrush HeaderCellBackground = new SolidColorBrush(Color.Parse("#EEEEEE")); // Grey.Lighten3
    private static readonly IBrush HeaderCellBorder = new SolidColorBrush(Color.Parse("#BDBDBD"));     // Grey.Lighten1
    private static readonly IBrush DataCellBorder = new SolidColorBrush(Color.Parse("#E0E0E0"));       // Grey.Lighten2

    private ReportPreviewViewModel? _viewModel;

    public ReportPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as ReportPreviewViewModel;

        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyZoom();
        RebuildTable();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ReportPreviewViewModel.PreviewRevision):
                RebuildTable();
                break;
            case nameof(ReportPreviewViewModel.Zoom):
                ApplyZoom();
                break;
        }
    }

    private void ApplyZoom()
    {
        var zoom = _viewModel?.Zoom ?? 1.0;
        PageTransform.LayoutTransform = new ScaleTransform(zoom, zoom);
    }

    /// <summary>
    /// Перестраивает таблицу событий одним Grid: строка-заголовок + строки данных используют
    /// одни и те же звёздные колонки (веса = WidthPercent, как RelativeColumn в PDF).
    /// </summary>
    private void RebuildTable()
    {
        var grid = EventsTableGrid;
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        if (_viewModel is null)
            return;

        var columns = _viewModel.EventColumns;
        var rows = _viewModel.PreviewEventRows;

        if (columns.Count == 0)
            return;

        // Колонки: звёздные веса = WidthPercent (или 1) — как columns.RelativeColumn(...) в PDF.
        foreach (var col in columns)
        {
            var weight = col.WidthPercent is > 0 ? col.WidthPercent.Value : 1;
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(weight, GridUnitType.Star)));
        }

        // Строка-заголовок: серая подложка, жирный 9pt.
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var i = 0; i < columns.Count; i++)
        {
            var cell = CreateCell(columns[i].Header, 9, FontWeight.Bold, HeaderCellBorder, HeaderCellBackground);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        // Строки данных: границы, 8pt, левое выравнивание (как в PDF).
        var rowIndex = 1;
        foreach (var row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            foreach (var cell in row.Cells)
            {
                var border = CreateCell(cell.Text, 8, FontWeight.Normal, DataCellBorder, null);
                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, cell.Column);
                grid.Children.Add(border);
            }

            rowIndex++;
        }
    }

    private static Border CreateCell(string text, double fontSize, FontWeight weight, IBrush border, IBrush? background)
        => new()
        {
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            Background = background,
            Padding = new Thickness(5),
            Child = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = weight,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap
            }
        };
}
