using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FirebirdTraceAnalyzer.ViewModels;
using ReportTextAlignment = FirebirdTraceAnalyzer.Enums.Reports.TextAlignment;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Живое превью отчёта (левая панель редактора). DataContext — <c>ReportPreviewViewModel</c>.
/// Рендерит ту же проекцию (<c>ReportProjectionService</c>), что и экспортёры, поэтому превью
/// совпадает с итоговым файлом (WYSIWYG). Таблица событий строится единым Grid, чтобы колонки
/// заголовка и строк были выровнены точно как в Pdf.
/// </summary>
public partial class ReportPreviewView : UserControl
{
    // Цвета из Pdf-экспорта (QuestPDF Material palette) — фиксированные, лист всегда белый.
    private static readonly IBrush HeaderCellBackground = new SolidColorBrush(Color.Parse("#EEEEEE")); // Grey.Lighten3
    private static readonly IBrush HeaderCellBorder = new SolidColorBrush(Color.Parse("#BDBDBD"));     // Grey.Lighten1
    private static readonly IBrush DataCellBorder = new SolidColorBrush(Color.Parse("#E0E0E0"));       // Grey.Lighten2

    // Цвета «экселевого» листа для Xlsx-превью.
    private static readonly IBrush SheetGutterBackground = new SolidColorBrush(Color.Parse("#F5F5F5"));
    private static readonly IBrush SheetGridLine = new SolidColorBrush(Color.Parse("#D4D4D4"));
    private static readonly IBrush SheetHeaderBackground = new SolidColorBrush(Color.Parse("#D9D9D9")); // LightGray
    private static readonly IBrush SheetGutterText = new SolidColorBrush(Color.Parse("#9E9E9E"));

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
        BuildSheet();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ReportPreviewViewModel.PreviewRevision):
                RebuildTable();
                BuildSheet();
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
    /// одни и те же звёздные колонки (веса = WidthPercent, как RelativeColumn в Pdf).
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

        // Колонки: звёздные веса = WidthPercent (или 1) — как columns.RelativeColumn(...) в Pdf.
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

        // Строки данных: границы, 8pt, левое выравнивание (как в Pdf).
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

    // ==================== Xlsx «лист Excel» ====================

    private int _sheetColumnCount;

    /// <summary>
    /// Строит превью формата Xlsx как лист Excel: гаттер с номерами строк, шапка с буквами колонок,
    /// затем содержимое как в <c>XlsxReportExporter</c> (заголовок, переменные, таблица, summary, футер).
    /// </summary>
    private void BuildSheet()
    {
        var grid = SheetGrid;
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        if (_viewModel is null || _viewModel.EventColumns.Count == 0)
            return;

        var columns = _viewModel.EventColumns;
        var rows = _viewModel.PreviewEventRows;

        // Колонки: гаттер номеров строк + N колонок данных (минимум 2, чтобы влезли метки/значения).
        _sheetColumnCount = Math.Max(columns.Count, 2);
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(46)));
        for (var i = 0; i < _sheetColumnCount; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        // Строка 0 — буквы колонок (A, B, C ...) с угловой ячейкой.
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.Children.Add(SheetLetterCell(string.Empty, 0));
        for (var c = 0; c < _sheetColumnCount; c++)
            grid.Children.Add(SheetLetterCell(ColumnLetter(c), c + 1));

        var row = 1;

        AddSpanRow(ref row, _viewModel.PreviewTitle, 16, bold: true, italic: false, TextAlignment.Center);

        if (!string.IsNullOrWhiteSpace(_viewModel.PreviewSubtitle))
            AddSpanRow(ref row, _viewModel.PreviewSubtitle, 12, false, true, TextAlignment.Center);

        if (!string.IsNullOrWhiteSpace(_viewModel.GeneratedDateText))
            AddSpanRow(ref row, _viewModel.GeneratedDateText, 9, false, false, TextAlignment.Right);

        AddBlankRow(ref row);

        foreach (var variable in _viewModel.HeaderVariables)
            AddLabelValueRow(ref row, $"{variable.Label}:", variable.Value);

        AddBlankRow(ref row);
        AddSpanRow(ref row, "Events", 14, true, false, TextAlignment.Left);

        // Таблица: шапка (серая, жирная) + строки данных, у каждой ячейки тонкие границы.
        AddSheetTableHeader(ref row, columns);
        foreach (var r in rows)
            AddSheetDataRow(ref row, r);

        if (_viewModel.Statistics.Count > 0)
        {
            AddBlankRow(ref row);
            AddSpanRow(ref row, "Summary Statistics", 14, true, false, TextAlignment.Left);
            foreach (var stat in _viewModel.Statistics)
                AddLabelValueRow(ref row, $"{stat.Label}:", stat.Value);
        }

        if (!string.IsNullOrWhiteSpace(_viewModel.FooterText))
        {
            AddBlankRow(ref row);
            AddSpanRow(ref row, _viewModel.FooterText, 8, false, true, TextAlignment.Center);
        }
    }

    private void AddSpanRow(ref int row, string text, double fontSize, bool bold, bool italic, TextAlignment alignment)
    {
        SheetGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        SheetGrid.Children.Add(SheetGutterCell(row));

        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
            Foreground = Brushes.Black,
            TextAlignment = alignment,
            Padding = new Thickness(6, 3),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, 1);
        Grid.SetColumnSpan(block, _sheetColumnCount);
        SheetGrid.Children.Add(block);

        row++;
    }

    private void AddLabelValueRow(ref int row, string label, string value)
    {
        SheetGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        SheetGrid.Children.Add(SheetGutterCell(row));

        var labelBlock = new TextBlock
        {
            Text = label, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brushes.Black,
            Padding = new Thickness(6, 3), TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(labelBlock, row);
        Grid.SetColumn(labelBlock, 1);
        SheetGrid.Children.Add(labelBlock);

        var valueBlock = new TextBlock
        {
            Text = value, FontSize = 11, Foreground = Brushes.Black,
            Padding = new Thickness(6, 3), TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(valueBlock, row);
        Grid.SetColumn(valueBlock, 2);
        Grid.SetColumnSpan(valueBlock, Math.Max(1, _sheetColumnCount - 1));
        SheetGrid.Children.Add(valueBlock);

        row++;
    }

    private void AddBlankRow(ref int row)
    {
        SheetGrid.RowDefinitions.Add(new RowDefinition(new GridLength(6)));
        SheetGrid.Children.Add(SheetGutterCell(row));
        row++;
    }

    private void AddSheetTableHeader(ref int row, System.Collections.Generic.IReadOnlyList<PreviewColumnItem> columns)
    {
        SheetGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        SheetGrid.Children.Add(SheetGutterCell(row));

        for (var i = 0; i < columns.Count; i++)
        {
            var cell = SheetBorderCell(columns[i].Header, 11, FontWeight.Bold, TextAlignment.Left, SheetHeaderBackground);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, i + 1);
            SheetGrid.Children.Add(cell);
        }

        row++;
    }

    private void AddSheetDataRow(ref int row, PreviewEventRow dataRow)
    {
        SheetGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        SheetGrid.Children.Add(SheetGutterCell(row));

        foreach (var cell in dataRow.Cells)
        {
            var alignment = cell.Alignment switch
            {
                ReportTextAlignment.Center => TextAlignment.Center,
                ReportTextAlignment.Right => TextAlignment.Right,
                _ => TextAlignment.Left
            };

            var border = SheetBorderCell(cell.Text, 11, FontWeight.Normal, alignment, null);
            Grid.SetRow(border, row);
            Grid.SetColumn(border, cell.Column + 1);
            SheetGrid.Children.Add(border);
        }

        row++;
    }

    private static Border SheetBorderCell(string text, double fontSize, FontWeight weight, TextAlignment alignment, IBrush? background)
        => new()
        {
            BorderBrush = SheetGridLine,
            BorderThickness = new Thickness(0.5),
            Background = background,
            Padding = new Thickness(6, 4),
            Child = new TextBlock
            {
                Text = text, FontSize = fontSize, FontWeight = weight,
                Foreground = Brushes.Black, TextAlignment = alignment, TextWrapping = TextWrapping.Wrap
            }
        };

    private Border SheetGutterCell(int number)
    {
        var border = new Border
        {
            Background = SheetGutterBackground,
            BorderBrush = SheetGridLine,
            BorderThickness = new Thickness(0.5),
            Child = new TextBlock
            {
                Text = number.ToString(),
                FontSize = 9,
                Foreground = SheetGutterText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetRow(border, number);
        Grid.SetColumn(border, 0);
        return border;
    }

    private static Border SheetLetterCell(string letter, int column)
    {
        var border = new Border
        {
            Background = SheetGutterBackground,
            BorderBrush = SheetGridLine,
            BorderThickness = new Thickness(0.5),
            Child = new TextBlock
            {
                Text = letter,
                FontSize = 9,
                FontWeight = FontWeight.SemiBold,
                Foreground = SheetGutterText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 3)
            }
        };
        Grid.SetRow(border, 0);
        Grid.SetColumn(border, column);
        return border;
    }

    private static string ColumnLetter(int index)
    {
        var letter = string.Empty;
        index++;
        while (index > 0)
        {
            var rem = (index - 1) % 26;
            letter = (char)('A' + rem) + letter;
            index = (index - 1) / 26;
        }
        return letter;
    }
}
