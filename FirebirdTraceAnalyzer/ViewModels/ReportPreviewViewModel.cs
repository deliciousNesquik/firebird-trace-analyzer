using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia.Threading;
using CsvHelper;
using CsvHelper.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces.EventProperties;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Reports;
using FirebirdTraceAnalyzer.Services.Reports.Exporters;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
///     ViewModel для превью отчёта
/// </summary>
public partial class ReportPreviewViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IReportGenerationService _generationService;

    // Та же проекция, что рисуют экспортёры (PDF/CSV/DOCX/XLSX) — благодаря ей превью совпадает
    // с итоговым файлом, включая группировку и агрегаты (WYSIWYG).
    private readonly IReportProjectionService _projectionService;

    #region Observable Properties

    [ObservableProperty] private ReportTemplate? _template;

    [ObservableProperty] private ReportMetadata? _metadata;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private string _statusMessage = Loc.Tr("Status.ReportPreview.Ready");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDocumentPreview))]
    [NotifyPropertyChangedFor(nameof(IsSheetPreview))]
    [NotifyPropertyChangedFor(nameof(IsTextPreview))]
    private ReportFormat _selectedFormat = ReportFormat.PDF;

    public List<ReportFormat> AvailableFormats { get; } = Enum.GetValues<ReportFormat>().ToList();

    /// <summary>PDF и DOCX рисуются как «документ» (лист А4).</summary>
    public bool IsDocumentPreview => SelectedFormat is ReportFormat.PDF or ReportFormat.DOCX;

    /// <summary>XLSX — как лист Excel (сетка с буквами колонок и номерами строк).</summary>
    public bool IsSheetPreview => SelectedFormat == ReportFormat.XLSX;

    /// <summary>CSV — как plain-текст файла.</summary>
    public bool IsTextPreview => SelectedFormat == ReportFormat.CSV;

    /// <summary>Точный текст CSV-файла (для превью формата CSV).</summary>
    [ObservableProperty] private string _csvText = string.Empty;

    /// <summary>Масштаб «листа» превью (1.0 = 100%).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomPercent))]
    private double _zoom = 1.0;

    public string ZoomPercent => $"{Zoom * 100:0}%";

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(3.0, Math.Round(Zoom + 0.1, 2));

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(0.5, Math.Round(Zoom - 0.1, 2));

    [RelayCommand]
    private void ZoomReset() => Zoom = 1.0;

    /// <summary>
    /// Инкрементируется после каждой перегенерации данных превью. Code-behind вью подписывается
    /// на изменение и перестраивает таблицу событий одним Grid (гарантированное выравнивание колонок
    /// заголовка и строк — как единая таблица в PDF).
    /// </summary>
    [ObservableProperty] private int _previewRevision;

    #endregion

    #region Preview Data

    /// <summary>Заголовок отчёта</summary>
    [ObservableProperty] private string _previewTitle = string.Empty;

    /// <summary>Подзаголовок отчёта</summary>
    [ObservableProperty] private string _previewSubtitle = string.Empty;

    /// <summary>Строка даты генерации ("Generated: ..."), как в PDF (пусто, если отключена).</summary>
    [ObservableProperty] private string _generatedDateText = string.Empty;

    /// <summary>Переменные заголовка с их значениями</summary>
    public ObservableCollection<PreviewVariableItem> HeaderVariables { get; } = new();

    /// <summary>Строки таблицы событий (ячейки по столбцам)</summary>
    public ObservableCollection<PreviewEventRow> PreviewEventRows { get; } = new();

    /// <summary>Столбцы таблицы событий</summary>
    public ObservableCollection<PreviewColumnItem> EventColumns { get; } = new();

    /// <summary>
    /// Общая раскладка колонок ("20*,50*,30*") — одна на заголовок и все строки, чтобы столбцы
    /// идеально выравнивались. Считается из WidthPercent колонок (равные доли, если не заданы).
    /// </summary>
    [ObservableProperty] private string _columnWidths = string.Empty;

    /// <summary>Статистика</summary>
    public ObservableCollection<PreviewStatItem> Statistics { get; } = new();

    /// <summary>Примечание об усечении числа строк в превью (пусто, если не усечено).</summary>
    [ObservableProperty] private string _rowNote = string.Empty;

    /// <summary>Футер</summary>
    [ObservableProperty] private string _footerText = string.Empty;

    /// <summary>Максимум строк в превью — это витрина дизайна, а не полный дамп данных.</summary>
    private const int PreviewRowLimit = 200;

    #endregion

    public ReportPreviewViewModel()
    {
        _generationService = null!;
        _projectionService = null!;
    }

    public ReportPreviewViewModel(
        IReportGenerationService generationService,
        IReportProjectionService projectionService)
    {
        _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
    }

    /// <summary>
    ///     Инициализирует превью с шаблоном и метаданными
    /// </summary>
    public async Task InitializeAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusMessage = Loc.Tr("Status.ReportPreview.Generating");

            Template = template;
            Metadata = metadata;

            // Генерируем превью
            await GeneratePreviewAsync(cancellationToken);

            StatusMessage = Loc.Tr("Status.ReportPreview.PreviewReady");
            Logger.Info("Preview initialized for template: {Name}", template.Name);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error initializing preview");
            StatusMessage = string.Format(Loc.Tr("Status.ReportPreview.Error"), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GeneratePreviewAsync(CancellationToken cancellationToken)
    {
        if (Template == null || Metadata == null)
            return;

        var title = Template.Header.Title;
        var subtitle = Template.Header.Subtitle ?? string.Empty;

        // Как в PDF: "Generated: {date}" (только если включено в шапке).
        var generatedDate = Template.Header.ShowGeneratedDate
            ? $"Generated: {Metadata.GeneratedAt.ToString(Template.Header.DateFormat)}"
            : string.Empty;

        var variables = Template.Header.Variables
            .Where(v => v.IsVisible)
            .OrderBy(v => v.DisplayOrder)
            .Select(variable => new PreviewVariableItem
            {
                Label = variable.DisplayName,
                Value = GetVariableValue(variable)
            })
            .ToList();

        // Строим ту же таблицу, что и экспортёры: группировка/агрегаты/порядок колонок — из проекции.
        var table = _projectionService.BuildTable(Template, Metadata.Events);

        var columns = table.Columns
            .Select((col, i) => new PreviewColumnItem
            {
                Index = i,
                Header = col.DisplayName,
                Format = col.Format,
                WidthPercent = col.WidthPercent,
                Alignment = col.Alignment
            })
            .ToList();

        // Общая строка ширин колонок для заголовка и строк (равные доли, если WidthPercent не задан).
        var columnWidths = columns.Count == 0
            ? string.Empty
            : string.Join(",", columns.Select(c => $"{(c.WidthPercent is > 0 ? c.WidthPercent.Value : 1)}*"));

        // Превью усекаем: показываем только первые PreviewRowLimit строк.
        var totalRows = table.Rows.Count;

        var rows = table.Rows
            .Take(PreviewRowLimit)
            .Select((cells, rowIndex) => new PreviewEventRow
            {
                IsAlternate = rowIndex % 2 == 1,
                ColumnWidths = columnWidths,
                Cells = cells
                    .Select((value, i) => new PreviewCell
                    {
                        Column = i,
                        Text = FormatCellValue(value, columns[i].Format),
                        Alignment = columns[i].Alignment
                    })
                    .ToList()
            })
            .ToList();

        var rowNote = totalRows > PreviewRowLimit
            ? $"Showing first {PreviewRowLimit:N0} of {totalRows:N0} rows — export for the full report"
            : $"{totalRows:N0} rows";

        var stats = new List<PreviewStatItem>();
        if (Template.Body.ShowSummary)
        {
            stats.Add(new PreviewStatItem { Label = "Total Files", Value = Metadata.Files.Count.ToString() });
            stats.Add(new PreviewStatItem
            {
                Label = "Total Events (before filters)",
                Value = Metadata.TotalEventsCount.ToString("N0")
            });
            stats.Add(new PreviewStatItem
            {
                Label = "Events in Report",
                Value = Metadata.Events.Count.ToString("N0")
            });

            if (!string.IsNullOrWhiteSpace(Metadata.ActiveFilters))
                stats.Add(new PreviewStatItem { Label = "Active Filters", Value = Metadata.ActiveFilters });

            if (!string.IsNullOrWhiteSpace(Metadata.ActiveSort))
                stats.Add(new PreviewStatItem { Label = "Active Sort", Value = Metadata.ActiveSort });
        }

        var footer = Template.Footer.Show ? Template.Footer.Text : string.Empty;

        // Точный текст CSV-файла (для превью формата CSV) — те же строки, что пишет CsvReportExporter.
        var csvText = BuildCsvText(columns, rows);

        cancellationToken.ThrowIfCancellationRequested();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            PreviewTitle = title;
            PreviewSubtitle = subtitle;
            GeneratedDateText = generatedDate;
            FooterText = footer;
            ColumnWidths = columnWidths;
            RowNote = rowNote;
            CsvText = csvText;

            HeaderVariables.Clear();
            foreach (var v in variables)
                HeaderVariables.Add(v);

            EventColumns.Clear();
            foreach (var c in columns)
                EventColumns.Add(c);

            PreviewEventRows.Clear();
            foreach (var row in rows)
                PreviewEventRows.Add(row);

            Statistics.Clear();
            foreach (var s in stats)
                Statistics.Add(s);

            // Сигнал вью перестроить таблицу событий (единый Grid).
            PreviewRevision++;
        });
    }

    private static string FormatCellValue(object? value, string? format)
        => ReportValueFormatter.Format(value, format);

    /// <summary>
    /// Собирает точный текст CSV-файла из колонок и (усечённых) строк превью — построчно повторяет
    /// вывод <c>CsvReportExporter</c> (метаданные с «#», заголовки, значения через запятую, summary).
    /// </summary>
    private string BuildCsvText(IReadOnlyList<PreviewColumnItem> columns, IReadOnlyList<PreviewEventRow> rows)
    {
        if (Template is null || Metadata is null)
            return string.Empty;

        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true
        });

        csv.WriteField($"# {Template.Header.Title}");
        csv.NextRecord();

        if (!string.IsNullOrWhiteSpace(Template.Header.Subtitle))
        {
            csv.WriteField($"# {Template.Header.Subtitle}");
            csv.NextRecord();
        }

        csv.WriteField($"# Generated: {Metadata.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        csv.NextRecord();
        csv.WriteField($"# Application: Flytic v{Metadata.ApplicationVersion}");
        csv.NextRecord();

        foreach (var variable in Template.Header.Variables.Where(v => v.IsVisible).OrderBy(v => v.DisplayOrder))
        {
            csv.WriteField($"# {variable.DisplayName}: {GetVariableValue(variable)}");
            csv.NextRecord();
        }

        csv.NextRecord(); // разделительная пустая строка

        foreach (var column in columns)
            csv.WriteField(column.Header);
        csv.NextRecord();

        foreach (var row in rows)
        {
            foreach (var cell in row.Cells)
                csv.WriteField(cell.Text);
            csv.NextRecord();
        }

        if (Template.Body.ShowSummary)
        {
            csv.NextRecord();
            csv.WriteField("# Summary Statistics");
            csv.NextRecord();
            csv.WriteField("# Total Files");
            csv.WriteField(Metadata.Files.Count);
            csv.NextRecord();
            csv.WriteField("# Total Events (before filters)");
            csv.WriteField(Metadata.TotalEventsCount);
            csv.NextRecord();
            csv.WriteField("# Events in Report");
            csv.WriteField(Metadata.Events.Count);
            csv.NextRecord();

            if (!string.IsNullOrWhiteSpace(Metadata.ActiveFilters))
            {
                csv.WriteField("# Active Filters");
                csv.WriteField(Metadata.ActiveFilters);
                csv.NextRecord();
            }

            if (!string.IsNullOrWhiteSpace(Metadata.ActiveSort))
            {
                csv.WriteField("# Active Sort");
                csv.WriteField(Metadata.ActiveSort);
                csv.NextRecord();
            }
        }

        csv.Flush();
        return sb.ToString();
    }

    /// <summary>
    ///     Генерирует и экспортирует отчёт
    /// </summary>
    [RelayCommand]
    private async Task ExportReportAsync(CancellationToken cancellationToken)
    {
        if (Template == null || Metadata == null)
        {
            StatusMessage = Loc.Tr("Status.ReportPreview.NoTemplate");
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = string.Format(Loc.Tr("Status.ReportPreview.Exporting"), SelectedFormat);

            var generatedReport = await _generationService.GenerateReportAsync(
                Template,
                Metadata,
                SelectedFormat,
                null,
                cancellationToken);

            StatusMessage = string.Format(Loc.Tr("Status.ReportPreview.Exported"), generatedReport.FilePath);
            Logger.Info("Report exported: {Path}", generatedReport.FilePath);

            // Открываем файл
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = generatedReport.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open exported report");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting report");
            StatusMessage = string.Format(Loc.Tr("Status.ReportPreview.ExportError"), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     Получает значение переменной
    /// </summary>
    private string GetVariableValue(ReportVariable variable)
        => Metadata is null ? "N/A" : ReportMetadataFormatter.GetVariableValue(variable, Metadata);
}

#region Helper Classes

public class PreviewVariableItem
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}

public class PreviewColumnItem
{
    public int Index { get; init; }
    public required string Header { get; init; }
    public string? Format { get; init; }
    public int? WidthPercent { get; init; }
    public TextAlignment Alignment { get; init; }
}

public class PreviewStatItem
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}

/// <summary>Одна ячейка строки превью: текст + индекс колонки (для Grid.Column) + выравнивание.</summary>
public class PreviewCell
{
    public int Column { get; init; }
    public required string Text { get; init; }
    public TextAlignment Alignment { get; init; }
}

public class PreviewEventRow
{
    public required IReadOnlyList<PreviewCell> Cells { get; init; }

    /// <summary>Чётность строки — для «зебры» (подсветка каждой второй строки).</summary>
    public bool IsAlternate { get; init; }

    /// <summary>Та же раскладка колонок, что и у заголовка — чтобы ячейки строки выравнивались.</summary>
    public required string ColumnWidths { get; init; }
}

#endregion