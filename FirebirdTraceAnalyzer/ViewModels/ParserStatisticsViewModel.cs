using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Services.Diagnostics;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// Диалог «Статистика парсера» (режим разработчика): тайминги обработки по фазам за текущую сессию —
/// сводка сверху + таблица по файлам. Данные берутся из <see cref="IParseTelemetry"/> (в памяти).
/// </summary>
public partial class ParserStatisticsViewModel : ViewModelBase, IDialogViewModel
{
    private readonly IParseTelemetry _telemetry;

    public event EventHandler<object?>? CloseRequested;

    public ObservableCollection<FileParseMetric> Files { get; } = [];

    [ObservableProperty] private int _fileCount;
    [ObservableProperty] private long _totalEvents;
    [ObservableProperty] private string _totalSizeText = "—";
    [ObservableProperty] private string _downloadText = "—";
    [ObservableProperty] private string _produceText = "—";
    [ObservableProperty] private string _storeWriteText = "—";
    [ObservableProperty] private string _uiText = "—";
    [ObservableProperty] private string _finalizeText = "—";
    [ObservableProperty] private string _perEventText = "—";
    [ObservableProperty] private string _throughputText = "—";
    [ObservableProperty] private string _summary = string.Empty;

    /// <summary>Конструктор только для XAML-дизайнера.</summary>
    public ParserStatisticsViewModel() => _telemetry = null!;

    public ParserStatisticsViewModel(IParseTelemetry telemetry)
        => _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

    public void Load() => Refresh();

    [RelayCommand]
    private void Refresh()
    {
        var items = _telemetry.Snapshot()
            .OrderByDescending(m => m.TotalMs)
            .ToList();

        Files.Clear();
        foreach (var m in items)
            Files.Add(m);

        FileCount = items.Count;
        TotalEvents = items.Sum(m => m.EventCount);

        var totalBytes = items.Sum(m => m.SizeBytes);
        var totalDownload = items.Sum(m => m.DownloadMs);
        var totalProduce = items.Sum(m => m.ProduceMs);
        var totalWrite = items.Sum(m => m.StoreWriteMs);
        var totalUi = items.Sum(m => m.UiMs);
        var finalize = _telemetry.FinalizeMs;

        TotalSizeText = ByteSizeFormatter.FormatBytes(totalBytes);
        DownloadText = Ms(totalDownload);
        ProduceText = Ms(totalProduce);
        StoreWriteText = Ms(totalWrite);
        UiText = Ms(totalUi);
        FinalizeText = Ms(finalize);
        PerEventText = TotalEvents > 0 ? $"{totalProduce * 1000.0 / TotalEvents:0.##} µs" : "—";
        ThroughputText = totalProduce > 0 ? $"{TotalEvents * 1000.0 / totalProduce:N0}/s" : "—";
        Summary = string.Format(Loc.Tr("Stats.Summary"), FileCount, Ms(totalDownload + totalProduce + totalWrite + totalUi + finalize));
    }

    [RelayCommand]
    private void ClearStats()
    {
        _telemetry.Clear();
        Refresh();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, null);

    /// <summary>Мс в человекочитаемый вид: &lt;1000 → «мс», иначе → «с».</summary>
    private static string Ms(long ms) => ms < 1000 ? $"{ms} ms" : $"{ms / 1000.0:0.0} s";
}
