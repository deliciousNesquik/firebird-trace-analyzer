using System;
using System.ComponentModel;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Окно «Анализ хранилища». Грид результата строится динамически (число/имена колонок известны
/// только в рантайме), поэтому собирается в code-behind по сигналу <c>ResultRevision</c>.
/// SQL-редактор — AvaloniaEdit с подсветкой; текст связан с VM.SqlText вручную (у TextEditor нет
/// удобного bindable-свойства текста).
/// </summary>
public partial class StorageAnalyticsView : UserControl
{
    private static readonly IBrush GridLineBrush = new SolidColorBrush(Color.Parse("#33808080"));
    private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.Parse("#22808080"));

    private StorageAnalyticsViewModel? _vm;
    private bool _syncingText;

    public StorageAnalyticsView()
    {
        InitializeComponent();
        SqlEditor.SyntaxHighlighting = LoadSqlHighlighting();
        SqlEditor.TextChanged += OnEditorTextChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private static IHighlightingDefinition? LoadSqlHighlighting()
    {
        try
        {
            using var stream = AssetLoader.Open(
                new Uri("avares://FirebirdTraceAnalyzer/Assets/SqlHighlighting.xshd"));
            using var reader = XmlReader.Create(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch
        {
            return null; // без подсветки редактор всё равно работает
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_vm is null || _syncingText)
            return;

        _syncingText = true;
        _vm.SqlText = SqlEditor.Text;
        _syncingText = false;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as StorageAnalyticsViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            SyncEditorFromVm();
        }

        RebuildGrid();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StorageAnalyticsViewModel.ResultRevision))
            RebuildGrid();
        else if (e.PropertyName == nameof(StorageAnalyticsViewModel.SqlText))
            SyncEditorFromVm();
    }

    // VM → редактор (напр. выбрали готовый запрос). Guard от эха при обычном вводе.
    private void SyncEditorFromVm()
    {
        if (_vm is null || _syncingText || SqlEditor.Text == _vm.SqlText)
            return;

        _syncingText = true;
        SqlEditor.Text = _vm.SqlText;
        _syncingText = false;
    }

    private void RebuildGrid()
    {
        var host = this.FindControl<ScrollViewer>("ResultHost");
        if (host is null)
            return;

        if (_vm is null || !_vm.HasResult)
        {
            host.Content = null;
            return;
        }

        var columns = _vm.ResultColumns;
        var rows = _vm.ResultRows;

        var grid = new Grid();
        for (var c = 0; c < columns.Count; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // заголовок
        for (var r = 0; r < rows.Count; r++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Заголовок
        for (var c = 0; c < columns.Count; c++)
        {
            var cell = MakeCell(columns[c], isHeader: true);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        // Данные
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < columns.Count; c++)
            {
                var text = c < row.Length ? row[c]?.ToString() ?? string.Empty : string.Empty;
                var cell = MakeCell(text, isHeader: false);
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        host.Content = grid;
    }

    private static Border MakeCell(string text, bool isHeader) => new()
    {
        BorderBrush = GridLineBrush,
        BorderThickness = new Thickness(0, 0, 1, 1),
        Background = isHeader ? HeaderBrush : null,
        Padding = new Thickness(8, 4),
        Child = new TextBlock
        {
            Text = text,
            FontWeight = isHeader ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        }
    };
}
