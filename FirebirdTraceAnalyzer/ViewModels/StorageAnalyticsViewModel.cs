using System.Globalization;
using System.Text;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Storage;
using FirebirdTraceAnalyzer.Services.Persistence;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// Окно «Анализ хранилища»: произвольный SELECT по накопительному хранилищу (агрегаты считаются
/// в SQLite, без загрузки событий в память) + готовые запросы + динамический грид результата и
/// экспорт в CSV. Доступ к БД — только через <see cref="EventStoreDispatcher"/> (одно соединение).
/// </summary>
public partial class StorageAnalyticsViewModel : ViewModelBase, IDialogViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>Максимум строк на один прогон (аналитика — небольшие наборы; сырой SELECT ограничиваем).</summary>
    private const int MaxRows = 1000;

    /// <summary>Колонки-времена: значения (Ticks) конвертируем в дату при показе.</summary>
    private static readonly HashSet<string> TimeColumns =
        new(StringComparer.OrdinalIgnoreCase) { "ts", "start_ts", "end_ts", "imported_ts" };

    private readonly EventStoreDispatcher _dispatcher;
    private readonly IWindowProvider _windowProvider;

    public event EventHandler<object?>? CloseRequested;

    [ObservableProperty] private string _sqlText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasResult;

    /// <summary>Инкрементируется при новом результате — сигнал code-behind перестроить грид.</summary>
    [ObservableProperty] private int _resultRevision;

    /// <summary>Колонки/строки последнего результата (читает code-behind при перестроении грида).</summary>
    public IReadOnlyList<string> ResultColumns { get; private set; } = [];
    public IReadOnlyList<object?[]> ResultRows { get; private set; } = [];

    /// <summary>Готовые запросы — отправная точка (выбор в тулбаре подставляет в редактор).</summary>
    public IReadOnlyList<PrebuiltQuery> Prebuilt { get; } = BuildPrebuilt();

    /// <summary>Выбор готового запроса из тулбара: подставляет SQL и сбрасывается (как меню).</summary>
    [ObservableProperty] private PrebuiltQuery? _selectedPrebuilt;

    /// <summary>Полная схема БД (все таблицы/колонки) — используется автодополнением редактора.
    /// Грузится из реальной DDL в <see cref="LoadAsync"/> (PRAGMA table_info), а не хардкодится.</summary>
    [ObservableProperty] private IReadOnlyList<SchemaTable> _schema = [];

    /// <summary>Базовый размер шрифта редактора (100% масштаба).</summary>
    private const double BaseFontSize = 13;

    /// <summary>Масштаб шрифта SQL-редактора (как «масштаб» в превью отчётов): 0.5–3.0.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorFontSize))]
    [NotifyPropertyChangedFor(nameof(EditorZoomPercent))]
    private double _editorZoom = 1.0;

    /// <summary>Итоговый размер шрифта редактора (привязан к TextEditor.FontSize).</summary>
    public double EditorFontSize => BaseFontSize * EditorZoom;

    /// <summary>Подпись бейджа масштаба.</summary>
    public string EditorZoomPercent => $"{EditorZoom * 100:0}%";

    [RelayCommand] private void FontZoomIn() => EditorZoom = Math.Min(3.0, Math.Round(EditorZoom + 0.1, 2));
    [RelayCommand] private void FontZoomOut() => EditorZoom = Math.Max(0.5, Math.Round(EditorZoom - 0.1, 2));
    [RelayCommand] private void FontZoomReset() => EditorZoom = 1.0;

    /// <summary>Конструктор только для XAML-дизайнера.</summary>
    public StorageAnalyticsViewModel()
    {
        _dispatcher = null!;
        _windowProvider = null!;
    }

    public StorageAnalyticsViewModel(EventStoreDispatcher dispatcher, IWindowProvider windowProvider)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));

        // Стартовый запрос — активность пользователей.
        SqlText = Prebuilt[0].Sql;
    }

    /// <summary>Вызывать до показа диалога: подгружает схему БД для автодополнения.</summary>
    public async Task LoadAsync()
    {
        try
        {
            Schema = await _dispatcher.RunAsync(store => store.GetSchema());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Storage analytics: failed to load schema");
        }

        StatusMessage = Loc.Tr("Store.Analyze.Ready");
    }

    private bool _applyingPrebuilt;

    // Выбор в комбобоксе тулбара подставляет SQL и сбрасывается — пункт работает как меню.
    partial void OnSelectedPrebuiltChanged(PrebuiltQuery? value)
    {
        if (_applyingPrebuilt || value is null)
            return;

        _applyingPrebuilt = true;
        SqlText = value.Sql;
        SelectedPrebuilt = null;
        _applyingPrebuilt = false;
    }

    [RelayCommand]
    private async Task RunQueryAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        StatusMessage = Loc.Tr("Store.Analyze.Running");
        try
        {
            var sql = SqlText;
            var result = await _dispatcher.RunAsync(store => store.ExecuteQuery(sql, MaxRows, cancellationToken));

            ApplyResult(result);

            StatusMessage = result.Truncated
                ? string.Format(Loc.Tr("Store.Analyze.RowsTruncated"), result.Rows.Count, result.ElapsedMs)
                : string.Format(Loc.Tr("Store.Analyze.Rows"), result.Rows.Count, result.ElapsedMs);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.Tr("Store.Analyze.Cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Storage analytics: query failed");
            StatusMessage = string.Format(Loc.Tr("Store.Analyze.Error"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (!HasResult)
        {
            StatusMessage = Loc.Tr("Store.Analyze.NothingToExport");
            return;
        }

        var topLevel = _windowProvider.GetCurrent();
        if (topLevel?.StorageProvider is null)
            return;

        var picked = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc.Tr("Store.Analyze.ExportTitle"),
            SuggestedFileName = "storage-analysis.csv",
            DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
        });
        if (picked is null)
            return;

        try
        {
            var csv = BuildCsv(ResultColumns, ResultRows);
            await using var stream = await picked.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(true)); // BOM — для Excel
            await writer.WriteAsync(csv);

            StatusMessage = string.Format(Loc.Tr("Store.Analyze.Exported"), ResultRows.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Storage analytics: CSV export failed");
            StatusMessage = string.Format(Loc.Tr("Store.Analyze.Error"), ex.Message);
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, null);

    // ---------------------------------------------------------------- helpers

    private void ApplyResult(StorageQueryResult result)
    {
        var isTime = result.Columns.Select(c => TimeColumns.Contains(c)).ToArray();

        // Ticks-колонки времени показываем как дату.
        var rows = new List<object?[]>(result.Rows.Count);
        foreach (var raw in result.Rows)
        {
            var row = new object?[raw.Length];
            for (var i = 0; i < raw.Length; i++)
                row[i] = isTime[i] && raw[i] is long ticks && ticks > 0
                    ? new DateTime(ticks).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    : raw[i];
            rows.Add(row);
        }

        ResultColumns = result.Columns;
        ResultRows = rows;
        HasResult = result.Columns.Count > 0;
        ResultRevision++;
    }

    private static string BuildCsv(IReadOnlyList<string> columns, IReadOnlyList<object?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(v => Escape(v?.ToString() ?? string.Empty))));
        return sb.ToString();

        static string Escape(string s) =>
            s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r')
                ? $"\"{s.Replace("\"", "\"\"")}\""
                : s;
    }

    private static IReadOnlyList<PrebuiltQuery> BuildPrebuilt() =>
    [
        new(Loc.Tr("Store.Analyze.Prebuilt.UserActivity"),
            """
            SELECT a.user               AS "Пользователь",
                   COUNT(*)             AS "Событий",
                   COUNT(DISTINCT e.file_id) AS "Файлов",
                   MIN(e.ts)            AS start_ts,
                   MAX(e.ts)            AS end_ts
            FROM event e
            JOIN attachment a ON a.id = e.attachment_ref
            GROUP BY a.user
            ORDER BY "Событий" DESC
            """),
        new(Loc.Tr("Store.Analyze.Prebuilt.TopSlow"),
            """
            SELECT s.text              AS "SQL",
                   COUNT(*)            AS "Вызовов",
                   MAX(e.perf_execute_ms) AS "Макс, мс",
                   AVG(e.perf_execute_ms) AS "Средн, мс"
            FROM event e
            JOIN sql_text s ON s.id = e.sql_ref
            WHERE e.perf_execute_ms IS NOT NULL
            GROUP BY e.sql_ref
            ORDER BY "Макс, мс" DESC
            LIMIT 50
            """),
        new(Loc.Tr("Store.Analyze.Prebuilt.ErrorsByUser"),
            """
            SELECT a.user      AS "Пользователь",
                   COUNT(*)    AS "Ошибок"
            FROM error_line el
            JOIN event e      ON e.seq = el.event_seq
            LEFT JOIN attachment a ON a.id = e.attachment_ref
            GROUP BY a.user
            ORDER BY "Ошибок" DESC
            """),
    ];

}
