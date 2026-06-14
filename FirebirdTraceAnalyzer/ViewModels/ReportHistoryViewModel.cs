using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel для истории сгенерированных отчётов
/// </summary>
public partial class ReportHistoryViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IFileDialogService _fileDialogService;

    private readonly string _reportsDirectory;

    #region Observable Properties

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _searchText = string.Empty;

    #endregion

    public ObservableCollection<ReportHistoryItem> AllReports { get; } = new();
    public ObservableCollection<ReportHistoryItem> FilteredReports { get; } = new();

    public ReportHistoryViewModel(IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _reportsDirectory = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "Reports", "History");

        if (!Directory.Exists(_reportsDirectory))
        {
            Directory.CreateDirectory(_reportsDirectory);
            Logger.Info("Created reports history directory: {Path}", _reportsDirectory);
        }
    }

    public ReportHistoryViewModel()
    {
        _fileDialogService = null!;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _reportsDirectory = Path.Combine(appDataPath, "FirebirdTraceAnalyzer", "Reports", "History");

        if (!Directory.Exists(_reportsDirectory))
        {
            Directory.CreateDirectory(_reportsDirectory);
            Logger.Info("Created reports history directory: {Path}", _reportsDirectory);
        }
    }

    /// <summary>
    /// Загружает список сгенерированных отчётов
    /// </summary>
    [RelayCommand]
    private async Task LoadReportsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading reports...";

            AllReports.Clear();

            await Task.Run(() =>
            {
                var files = Directory.GetFiles(_reportsDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => IsReportFile(f))
                    .OrderByDescending(f => File.GetCreationTime(f));

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);

                        AllReports.Add(new ReportHistoryItem
                        {
                            FileName = fileInfo.Name,
                            FilePath = fileInfo.FullName,
                            FileSize = fileInfo.Length,
                            CreatedAt = fileInfo.CreationTime,
                            Format = GetFormatFromExtension(fileInfo.Extension)
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error loading report file: {File}", file);
                    }
                }
            });

            ApplyFilter();

            StatusMessage = $"Loaded {AllReports.Count} report(s)";
            Logger.Info("Loaded {Count} reports from history", AllReports.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading reports");
            StatusMessage = $"Error: {ex.Message}";
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
            StatusMessage = $"Error opening report: {ex.Message}";
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
            StatusMessage = $"Error opening folder: {ex.Message}";
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
            File.Delete(report.FilePath);

            AllReports.Remove(report);
            FilteredReports.Remove(report);

            StatusMessage = $"Deleted: {report.FileName}";
            Logger.Info("Deleted report: {Path}", report.FilePath);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting report: {Path}", report.FilePath);
            StatusMessage = $"Error deleting report: {ex.Message}";
        }
    }

    private bool IsReportFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".pdf" or ".docx" or ".xlsx" or ".csv";
    }

    private string GetFormatFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "PDF",
            ".docx" => "DOCX",
            ".xlsx" => "XLSX",
            ".csv" => "CSV",
            _ => "Unknown"
        };
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

    public string FormattedSize => FormatFileSize(FileSize);
    public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

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