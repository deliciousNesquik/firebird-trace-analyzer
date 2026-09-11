using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Localization;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel для истории сгенерированных отчётов
/// </summary>
public partial class ReportHistoryViewModel : ViewModelBase, IDialogViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IFileDialogService _fileDialogService;

    private readonly IReportHistoryStore? _store;

    private readonly IToastService? _toasts;

    #region Observable Properties

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = Loc.Tr("Status.ReportHistory.Ready");

    [ObservableProperty]
    private string _searchText = string.Empty;

    #endregion

    public ObservableCollection<ReportHistoryItem> AllReports { get; } = new();
    public ObservableCollection<ReportHistoryItem> FilteredReports { get; } = new();

    public ReportHistoryViewModel(IFileDialogService fileDialogService, IReportHistoryStore store,
        IToastService? toasts = null)
    {
        _fileDialogService = fileDialogService;
        _store = store;
        _toasts = toasts;
    }

    public ReportHistoryViewModel()
    {
        _fileDialogService = null!;
        _store = null;
    }

    /// <summary>Диалог просит закрыться (результат не используется).</summary>
    public event EventHandler<object?>? CloseRequested;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, null);

    /// <summary>
    /// Загружает список сгенерированных отчётов
    /// </summary>
    [RelayCommand]
    private async Task LoadReportsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = Loc.Tr("Status.ReportHistory.Loading");

            AllReports.Clear();

            // Файловый ввод-вывод инкапсулирован в IReportHistoryStore (SoC/тестируемость).
            var entries = await Task.Run(() => _store?.List() ?? []);

            foreach (var e in entries)
            {
                AllReports.Add(new ReportHistoryItem
                {
                    FileName = e.FileName,
                    FilePath = e.FilePath,
                    FileSize = e.FileSize,
                    CreatedAt = e.CreatedAt,
                    Format = e.Format
                });
            }

            ApplyFilter();

            StatusMessage = string.Format(Loc.Tr("Status.ReportHistory.Loaded"), AllReports.Count);
            Logger.Info("Loaded {Count} reports from history", AllReports.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading reports");
            var msg = string.Format(Loc.Tr("Status.ReportHistory.Error"), ex.Message);
            StatusMessage = msg;
            _toasts?.Error(msg);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Применяет фильтр по поисковому запросу
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredReports.Clear();

        var query = AllReports.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(r => r.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var report in query)
        {
            FilteredReports.Add(report);
        }
    }

    /// <summary>
    /// Открывает отчёт
    /// </summary>
    [RelayCommand]
    private void OpenReport(ReportHistoryItem? report)
    {
        if (report == null)
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = report.FilePath,
                UseShellExecute = true
            });

            Logger.Info("Opened report: {Path}", report.FilePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening report: {Path}", report.FilePath);
            var msg = string.Format(Loc.Tr("Status.ReportHistory.OpenError"), ex.Message);
            StatusMessage = msg;
            _toasts?.Error(msg);
        }
    }

    /// <summary>
    /// Открывает папку с отчётом
    /// </summary>
    [RelayCommand]
    private async Task<bool> OpenReportFolder(ReportHistoryItem? report)
    {
        if (report == null)
            return false;

        try
        {
            Logger.Info("Open folder: {Path}", report.FilePath);
            return await _fileDialogService.RevealInFileManagerAsync(report.FilePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening folder");
            var msg = string.Format(Loc.Tr("Status.ReportHistory.OpenFolderError"), ex.Message);
            StatusMessage = msg;
            _toasts?.Error(msg);
        }
        
        return false;
    }

    /// <summary>
    /// Удаляет отчёт
    /// </summary>
    [RelayCommand]
    private async Task DeleteReportAsync(ReportHistoryItem? report)
    {
        if (report == null)
            return;

        try
        {
            _store?.Delete(report.FilePath);

            AllReports.Remove(report);
            FilteredReports.Remove(report);

            var msg = string.Format(Loc.Tr("Status.ReportHistory.Deleted"), report.FileName);
            StatusMessage = msg;
            _toasts?.Success(msg);
            Logger.Info("Deleted report: {Path}", report.FilePath);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting report: {Path}", report.FilePath);
            var msg = string.Format(Loc.Tr("Status.ReportHistory.DeleteError"), ex.Message);
            StatusMessage = msg;
            _toasts?.Error(msg);
        }
    }

}

public partial class ReportHistoryItem : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private long _fileSize;

    [ObservableProperty]
    private DateTime _createdAt;

    [ObservableProperty]
    private string _format = string.Empty;

    public string FormattedSize => ByteSizeFormatter.FormatBytes(FileSize);
    public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
}