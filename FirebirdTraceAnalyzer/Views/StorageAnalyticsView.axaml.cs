using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
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

    // Ключевые слова/функции SQL для автодополнения (имена таблиц/колонок добавляются из схемы VM).
    private static readonly string[] SqlKeywords =
    [
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "NULL", "IS", "IN", "LIKE", "BETWEEN", "EXISTS",
        "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "CROSS", "ON", "USING", "GROUP", "BY", "ORDER",
        "ASC", "DESC", "HAVING", "LIMIT", "OFFSET", "DISTINCT", "AS", "CASE", "WHEN", "THEN", "ELSE",
        "END", "UNION", "ALL", "WITH", "COLLATE",
        "COUNT", "SUM", "AVG", "MIN", "MAX", "CAST", "COALESCE", "LENGTH", "UPPER", "LOWER", "SUBSTR",
        "TRIM", "ROUND", "DATE", "DATETIME", "STRFTIME", "IFNULL", "ABS"
    ];

    private StorageAnalyticsViewModel? _vm;
    private bool _syncingText;
    private List<string> _completionWords = [.. SqlKeywords];
    private Dictionary<string, IReadOnlyList<string>> _tables = new(StringComparer.OrdinalIgnoreCase);
    private CompletionWindow? _completionWindow;

    public StorageAnalyticsView()
    {
        InitializeComponent();
        SqlEditor.SyntaxHighlighting = LoadSqlHighlighting();
        SqlEditor.TextChanged += OnEditorTextChanged;
        SqlEditor.TextArea.TextEntered += OnEditorTextEntered;

        // Зум шрифта — через те же команды VM, что и бейдж масштаба. Tunnel: перехват до внутреннего
        // скролла редактора, чтобы Ctrl+колесо не прокручивало текст.
        SqlEditor.AddHandler(InputElement.PointerWheelChangedEvent, OnEditorPointerWheel, RoutingStrategies.Tunnel);
        SqlEditor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);

        DataContextChanged += OnDataContextChanged;
    }

    private void OnEditorPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_vm is null || (e.KeyModifiers & KeyModifiers.Control) == 0)
            return;

        if (e.Delta.Y > 0)
            _vm.FontZoomInCommand.Execute(null);
        else
            _vm.FontZoomOutCommand.Execute(null);

        e.Handled = true;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null || (e.KeyModifiers & KeyModifiers.Control) == 0)
            return;

        switch (e.Key)
        {
            case Key.OemPlus or Key.Add:
                _vm.FontZoomInCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract:
                _vm.FontZoomOutCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D0 or Key.NumPad0:
                _vm.FontZoomResetCommand.Execute(null);
                e.Handled = true;
                break;
        }
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
            BuildCompletionWords();
        }

        RebuildGrid();
    }

    // Кандидаты автодополнения: ключевые слова + имена таблиц и колонок из схемы.
    private void BuildCompletionWords()
    {
        var words = new List<string>(SqlKeywords);
        if (_vm is not null)
            foreach (var table in _vm.Schema)
            {
                words.Add(table.Name);
                words.AddRange(table.Columns);
            }

        _completionWords = words.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _tables = _vm?.Schema.ToDictionary(t => t.Name, t => t.Columns, StringComparer.OrdinalIgnoreCase)
                  ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    }

    private void OnEditorTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_completionWindow is not null || string.IsNullOrEmpty(e.Text))
            return;

        // «table.» / «alias.» → колонки этой таблицы
        if (e.Text == ".")
        {
            ShowMemberCompletion();
            return;
        }

        var ch = e.Text[0];
        if (char.IsLetter(ch) || ch == '_')
            ShowWordCompletion();
    }

    // Обычное автодополнение по префиксу слова (ключевые слова + таблицы/колонки).
    private void ShowWordCompletion()
    {
        var prefix = GetWordBeforeOffset(SqlEditor.CaretOffset);
        if (prefix.Length < 1)
            return;

        var matches = _completionWords
            .Where(w => w.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        if (matches.Count == 0)
            return;

        OpenCompletion(matches, replacePrefixLength: prefix.Length);
    }

    // Колонки таблицы после точки: слово перед точкой — имя таблицы или её алиас из FROM/JOIN.
    private void ShowMemberCompletion()
    {
        var dotOffset = SqlEditor.CaretOffset - 1; // позиция только что введённой точки
        var ident = GetWordBeforeOffset(dotOffset);
        if (ident.Length == 0)
            return;

        var columns = ResolveTableColumns(ident);
        if (columns is null || columns.Count == 0)
            return;

        // Вставка сразу после точки — префикс заменять не нужно.
        OpenCompletion(columns, replacePrefixLength: 0);
    }

    private void OpenCompletion(IEnumerable<string> items, int replacePrefixLength)
    {
        _completionWindow = new CompletionWindow(SqlEditor.TextArea);
        if (replacePrefixLength > 0)
            _completionWindow.StartOffset -= replacePrefixLength;

        foreach (var item in items)
            _completionWindow.CompletionList.CompletionData.Add(new SqlCompletionData(item));

        _completionWindow.Closed += (_, _) => _completionWindow = null;
        _completionWindow.Show();
    }

    /// <summary>Разрешает слово перед точкой в колонки таблицы: прямое имя таблицы, иначе алиас,
    /// найденный в тексте как «FROM/JOIN &lt;table&gt; [AS] &lt;alias&gt;».</summary>
    private IReadOnlyList<string>? ResolveTableColumns(string ident)
    {
        if (_tables.TryGetValue(ident, out var direct))
            return direct;

        var table = FindAliasTable(ident);
        return table is not null && _tables.TryGetValue(table, out var byAlias) ? byAlias : null;
    }

    private string? FindAliasTable(string alias)
    {
        // Ищем «<word> [AS] <alias>» и проверяем, что <word> — известная таблица.
        var rx = new Regex($@"\b(\w+)\s+(?:AS\s+)?{Regex.Escape(alias)}\b", RegexOptions.IgnoreCase);
        foreach (Match m in rx.Matches(SqlEditor.Text))
        {
            var candidate = m.Groups[1].Value;
            if (_tables.ContainsKey(candidate))
                return candidate;
        }

        return null;
    }

    private string GetWordBeforeOffset(int offset)
    {
        var doc = SqlEditor.Document;
        var start = offset;
        while (start > 0)
        {
            var ch = doc.GetCharAt(start - 1);
            if (char.IsLetterOrDigit(ch) || ch == '_')
                start--;
            else
                break;
        }

        return doc.GetText(start, offset - start);
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

    /// <summary>Элемент списка автодополнения — вставляет слово, заменяя набранный префикс.</summary>
    private sealed class SqlCompletionData(string text) : ICompletionData
    {
        public IImage? Image => null;
        public string Text { get; } = text;
        public object Content => Text;
        public object Description => Text;
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            => textArea.Document.Replace(completionSegment, Text);
    }
}
