using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceAnalyzer.Services.Reports;
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
    private readonly IEventPropertyAccessor _propertyAccessor;

    #region Observable Properties

    [ObservableProperty] private ReportTemplate? _template;

    [ObservableProperty] private ReportMetadata? _metadata;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private string _statusMessage = "Ready";

    [ObservableProperty] private ReportFormat _selectedFormat = ReportFormat.PDF;

    public List<ReportFormat> AvailableFormats { get; } = Enum.GetValues<ReportFormat>().ToList();

    #endregion

    #region Preview Data

    /// <summary>Заголовок отчёта</summary>
    [ObservableProperty] private string _previewTitle = string.Empty;

    /// <summary>Подзаголовок отчёта</summary>
    [ObservableProperty] private string _previewSubtitle = string.Empty;

    /// <summary>Переменные заголовка с их значениями</summary>
    public ObservableCollection<PreviewVariableItem> HeaderVariables { get; } = new();

    /// <summary>События для отображения</summary>
    public ObservableCollection<EventBase> PreviewEvents { get; } = new();

    /// <summary>Строки таблицы событий (ячейки по столбцам)</summary>
    public ObservableCollection<PreviewEventRow> PreviewEventRows { get; } = new();

    /// <summary>Столбцы таблицы событий</summary>
    public ObservableCollection<PreviewColumnItem> EventColumns { get; } = new();

    /// <summary>Статистика</summary>
    public ObservableCollection<PreviewStatItem> Statistics { get; } = new();

    /// <summary>Футер</summary>
    [ObservableProperty] private string _footerText = string.Empty;

    #endregion

    public ReportPreviewViewModel()
    {
        _generationService = null!;
        _propertyAccessor = new EventPropertyAccessor();
    }

    public ReportPreviewViewModel(
        IReportGenerationService generationService,
        IEventPropertyAccessor propertyAccessor)
    {
        _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
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
            StatusMessage = "Generating preview...";

            Template = template;
            Metadata = metadata;

            // Генерируем превью
            await GeneratePreviewAsync(cancellationToken);

            StatusMessage = "Preview ready";
            Logger.Info("Preview initialized for template: {Name}", template.Name);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error initializing preview");
            StatusMessage = $"Error: {ex.Message}";
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

        var variables = Template.Header.Variables
            .Where(v => v.IsVisible)
            .OrderBy(v => v.DisplayOrder)
            .Select(variable => new PreviewVariableItem
            {
                Label = variable.DisplayName,
                Value = GetVariableValue(variable)
            })
            .ToList();

        var columns = Template.Body.VisibleFields
            .OrderBy(f => f.Order)
            .Select(field => new PreviewColumnItem
            {
                Header = field.DisplayName,
                PropertyPath = field.PropertyPath,
                Format = field.Format,
                Alignment = field.Alignment
            })
            .ToList();

        var events = Metadata.Events.ToList();

        var rows = events
            .Select(evt => new PreviewEventRow
            {
                Cells = columns
                    .Select(col => FormatCellValue(
                        _propertyAccessor.GetValue(evt, col.PropertyPath),
                        col.Format))
                    .ToList()
            })
            .ToList();

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

        cancellationToken.ThrowIfCancellationRequested();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            PreviewTitle = title;
            PreviewSubtitle = subtitle;
            FooterText = footer;

            HeaderVariables.Clear();
            foreach (var v in variables)
                HeaderVariables.Add(v);

            EventColumns.Clear();
            foreach (var c in columns)
                EventColumns.Add(c);

            PreviewEvents.Clear();
            foreach (var e in events)
                PreviewEvents.Add(e);

            PreviewEventRows.Clear();
            foreach (var row in rows)
                PreviewEventRows.Add(row);

            Statistics.Clear();
            foreach (var s in stats)
                Statistics.Add(s);
        });
    }

    private static string FormatCellValue(object? value, string? format)
    {
        if (value == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(format) && value is IFormattable formattable)
            return formattable.ToString(format, CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    ///     Генерирует превью данные
    /// </summary>
    private Task GeneratePreviewAsync1(CancellationToken cancellationToken)
    {
        if (Template == null || Metadata == null)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            // Заголовок
            PreviewTitle = Template.Header.Title;
            PreviewSubtitle = Template.Header.Subtitle ?? string.Empty;

            // Переменные заголовка
            HeaderVariables.Clear();
            foreach (var variable in Template.Header.Variables.Where(v => v.IsVisible).OrderBy(v => v.DisplayOrder))
            {
                var value = GetVariableValue(variable);
                HeaderVariables.Add(new PreviewVariableItem
                {
                    Label = variable.DisplayName,
                    Value = value
                });
            }

            // Столбцы событий
            EventColumns.Clear();
            foreach (var field in Template.Body.VisibleFields.OrderBy(f => f.Order))
                EventColumns.Add(new PreviewColumnItem
                {
                    Header = field.DisplayName,
                    PropertyPath = field.PropertyPath,
                    Format = field.Format,
                    Alignment = field.Alignment
                });

            // События
            PreviewEvents.Clear();
            foreach (var evt in Metadata.Events) PreviewEvents.Add(evt);

            // Статистика
            Statistics.Clear();
            if (Template.Body.ShowSummary)
            {
                Statistics.Add(new PreviewStatItem { Label = "Total Files", Value = Metadata.Files.Count.ToString() });
                Statistics.Add(new PreviewStatItem
                    { Label = "Total Events (before filters)", Value = Metadata.TotalEventsCount.ToString("N0") });
                Statistics.Add(new PreviewStatItem
                    { Label = "Events in Report", Value = Metadata.Events.Count.ToString("N0") });

                if (!string.IsNullOrWhiteSpace(Metadata.ActiveFilters))
                    Statistics.Add(new PreviewStatItem { Label = "Active Filters", Value = Metadata.ActiveFilters });

                if (!string.IsNullOrWhiteSpace(Metadata.ActiveSort))
                    Statistics.Add(new PreviewStatItem { Label = "Active Sort", Value = Metadata.ActiveSort });
            }

            // Футер
            FooterText = Template.Footer.Show ? Template.Footer.Text : string.Empty;

            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);
    }

    /// <summary>
    ///     Генерирует и экспортирует отчёт
    /// </summary>
    [RelayCommand]
    private async Task ExportReportAsync(CancellationToken cancellationToken)
    {
        if (Template == null || Metadata == null)
        {
            StatusMessage = "No template or metadata available";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Exporting to {SelectedFormat}...";

            var generatedReport = await _generationService.GenerateReportAsync(
                Template,
                Metadata,
                SelectedFormat,
                null,
                cancellationToken);

            StatusMessage = $"Report exported: {generatedReport.FilePath}";
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
            StatusMessage = $"Export error: {ex.Message}";
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
    {
        if (Metadata == null)
            return "N/A";

        return variable.Type switch
        {
            ReportVariableType.FileNames => string.Join(", ", Metadata.Files.Select(f => f.FileName)),
            ReportVariableType.FilePaths => string.Join(", ", Metadata.Files.Select(f => f.FilePath)),
            ReportVariableType.FileCount => Metadata.Files.Count.ToString(),
            ReportVariableType.FileSizeTotal => FormatFileSize(Metadata.Files.Sum(f => f.FileSize)),

            ReportVariableType.TotalEventsCount => Metadata.TotalEventsCount.ToString("N0"),
            ReportVariableType.FilteredEventsCount => Metadata.Events.Count.ToString("N0"),
            ReportVariableType.VisibleEventsCount => Metadata.Events.Count.ToString("N0"),

            ReportVariableType.TraceStartTime => Metadata.Files.Count > 0
                ? Metadata.Files.Min(f => f.StartTrace).ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A",
            ReportVariableType.TraceEndTime => Metadata.Files.Count > 0
                ? Metadata.Files.Max(f => f.EndTrace).ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A",
            ReportVariableType.TraceDuration => GetTraceDuration(),

            ReportVariableType.ActiveFilters => Metadata.ActiveFilters ?? "None",
            ReportVariableType.ActiveSort => Metadata.ActiveSort ?? "None",

            ReportVariableType.GeneratedDate => Metadata.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            ReportVariableType.GeneratedBy => Environment.UserName,
            ReportVariableType.ApplicationVersion => Metadata.ApplicationVersion,

            _ => "N/A"
        };
    }

    private string GetTraceDuration()
    {
        if (Metadata == null || Metadata.Files.Count == 0)
            return "N/A";

        var start = Metadata.Files.Min(f => f.StartTrace);
        var end = Metadata.Files.Max(f => f.EndTrace);
        var duration = end - start;

        return $"{duration.TotalHours:F2} hours";
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        var size = (double)bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}

#region Helper Classes

public class PreviewVariableItem
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}

public class PreviewColumnItem
{
    public required string Header { get; init; }
    public required string PropertyPath { get; init; }
    public string? Format { get; init; }
    public TextAlignment Alignment { get; init; }
}

public class PreviewStatItem
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}

public class PreviewEventRow
{
    public required IReadOnlyList<string> Cells { get; init; }
}

#endregion