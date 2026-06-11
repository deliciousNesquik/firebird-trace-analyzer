using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Models;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel окна прогресса загрузки
/// </summary>
public partial class DownloadProgressViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    #region Observable Properties - Current File

    [ObservableProperty]
    private int _currentFileIndex;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private string _currentFileName = string.Empty;

    [ObservableProperty]
    private long _currentFileBytes;

    [ObservableProperty]
    private long _currentFileTotalBytes;

    [ObservableProperty]
    private double _currentFileProgress;

    #endregion

    #region Observable Properties - Overall

    [ObservableProperty]
    private long _totalBytesTransferred;

    [ObservableProperty]
    private long _totalBytesOverall;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private double _downloadSpeed; // bytes per second

    [ObservableProperty]
    private TimeSpan _estimatedTimeRemaining;

    #endregion

    #region Observable Properties - Completed Files

    [ObservableProperty]
    private string _completedFilesList = string.Empty;

    [ObservableProperty]
    private string _pendingFilesList = string.Empty;

    #endregion

    #region Observable Properties - State

    [ObservableProperty]
    private bool _isDownloading = true;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _statusMessage = "Preparing download...";

    #endregion

    private DateTime _startTime;
    private readonly List<string> _completedFiles = new();
    private readonly List<string> _pendingFiles = new();

    // Фактический объём завершённых файлов и карта размеров — для точного общего прогресса
    private long _completedBytes;
    private readonly Dictionary<string, long> _fileSizes = new();

    public event EventHandler? CancelRequested;
    public event EventHandler? Completed;

    public DownloadProgressViewModel()
    {
    }

    public void Initialize(IReadOnlyList<RemoteFileInfo> filesToDownload)
    {
        TotalFiles = filesToDownload.Count;
        CurrentFileIndex = 0;
        TotalBytesOverall = filesToDownload.Sum(f => f.Size);
        
        _startTime = DateTime.Now;

        _completedFiles.Clear();
        _completedBytes = 0;

        _fileSizes.Clear();
        foreach (var f in filesToDownload)
            _fileSizes[f.FileName] = f.Size;

        _pendingFiles.Clear();
        _pendingFiles.AddRange(filesToDownload.Select(f => f.FileName));

        UpdatePendingFilesList();
        
        StatusMessage = $"Ready to download {TotalFiles} file(s)";
        Logger.Info("Download initialized: {Count} files, total size: {Size} bytes", 
            TotalFiles, TotalBytesOverall);
    }

    public void UpdateProgress(int fileIndex, int totalFiles, long bytesTransferred, long totalBytes)
    {
        CurrentFileIndex = fileIndex + 1;
        TotalFiles = totalFiles;
        CurrentFileBytes = bytesTransferred;
        CurrentFileTotalBytes = totalBytes;

        // Прогресс текущего файла
        CurrentFileProgress = totalBytes > 0
            ? Math.Min(100, (double)bytesTransferred / totalBytes * 100)
            : 0;

        // Общий прогресс: фактически скачанные байты завершённых файлов + прогресс текущего
        TotalBytesTransferred = _completedBytes + bytesTransferred;
        OverallProgress = TotalBytesOverall > 0
            ? Math.Min(100, (double)TotalBytesTransferred / TotalBytesOverall * 100)
            : 0;

        // Скорость и оставшееся время
        var elapsed = DateTime.Now - _startTime;
        if (elapsed.TotalSeconds > 0)
        {
            DownloadSpeed = TotalBytesTransferred / elapsed.TotalSeconds;
            
            var remainingBytes = TotalBytesOverall - TotalBytesTransferred;
            EstimatedTimeRemaining = DownloadSpeed > 0 
                ? TimeSpan.FromSeconds(remainingBytes / DownloadSpeed)
                : TimeSpan.Zero;
        }

        UpdateStatusMessage();
    }

    public void FileCompleted(string fileName)
    {
        _completedFiles.Add(fileName);
        _pendingFiles.Remove(fileName);

        if (_fileSizes.TryGetValue(fileName, out var size))
            _completedBytes += size;

        UpdateCompletedFilesList();
        UpdatePendingFilesList();

        Logger.Debug("File completed: {FileName}", fileName);
    }

    public void FileStarted(string fileName)
    {
        CurrentFileName = fileName;
        CurrentFileBytes = 0;
        CurrentFileProgress = 0;
        
        StatusMessage = $"Downloading {fileName}...";
        Logger.Debug("File started: {FileName}", fileName);
    }

    public void DownloadCompleted()
    {
        IsDownloading = false;
        IsCompleted = true;
        CurrentFileProgress = 100;
        OverallProgress = 100;
        StatusMessage = $"✓ Download completed: {TotalFiles} file(s)";
        
        Logger.Info("Download completed successfully");
        Completed?.Invoke(this, EventArgs.Empty);
    }

    public void DownloadFailed(string errorMessage)
    {
        IsDownloading = false;
        StatusMessage = $"✗ Download failed: {errorMessage}";
        
        Logger.Error("Download failed: {Error}", errorMessage);
    }

    [RelayCommand]
    private void Cancel()
    {
        Logger.Info("Download cancellation requested");
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateStatusMessage()
    {
        var speedMBps = DownloadSpeed / (1024 * 1024);
        var remaining = EstimatedTimeRemaining.TotalSeconds > 0 
            ? $"{EstimatedTimeRemaining:mm\\:ss}" 
            : "--:--";

        StatusMessage = $"Downloading {CurrentFileIndex}/{TotalFiles} • " +
                       $"{speedMBps:F2} MB/s • " +
                       $"Remaining: {remaining}";
    }

    private void UpdateCompletedFilesList()
    {
        CompletedFilesList = string.Join("\n", _completedFiles.Select(f => $"✓ {f}"));
    }

    private void UpdatePendingFilesList()
    {
        PendingFilesList = string.Join("\n", _pendingFiles.Select(f => $"• {f}"));
    }

    public string GetFormattedSpeed()
    {
        if (DownloadSpeed < 1024)
            return $"{DownloadSpeed:F0} B/s";
        
        if (DownloadSpeed < 1024 * 1024)
            return $"{DownloadSpeed / 1024:F2} KB/s";
        
        return $"{DownloadSpeed / (1024 * 1024):F2} MB/s";
    }

    public string GetFormattedProgress()
    {
        var current = FormatBytes(TotalBytesTransferred);
        var total = FormatBytes(TotalBytesOverall);
        return $"{current} / {total}";
    }

    private static string FormatBytes(long bytes)
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