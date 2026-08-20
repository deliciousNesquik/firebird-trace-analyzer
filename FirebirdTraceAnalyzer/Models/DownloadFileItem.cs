using CommunityToolkit.Mvvm.ComponentModel;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>State of a file in the download queue.</summary>
public enum DownloadItemStatus
{
    /// <summary>Queued, not started yet.</summary>
    Pending,

    /// <summary>Currently downloading.</summary>
    Downloading,

    /// <summary>Downloaded successfully.</summary>
    Completed,

    /// <summary>Download failed.</summary>
    Failed
}

/// <summary>
/// A single file in the download list: name plus current status (to show per-file progress in the
/// download window).
/// </summary>
public partial class DownloadFileItem : ObservableObject
{
    /// <summary>Name of the file being downloaded.</summary>
    public required string FileName { get; init; }

    /// <summary>Current download status of the file.</summary>
    [ObservableProperty]
    private DownloadItemStatus _status = DownloadItemStatus.Pending;

    // Удобные флаги для биндинга видимости статус-иконок в XAML (без конвертеров).
    /// <summary>Whether the file is queued and not started yet.</summary>
    public bool IsPending => Status == DownloadItemStatus.Pending;

    /// <summary>Whether the file is currently downloading.</summary>
    public bool IsDownloading => Status == DownloadItemStatus.Downloading;

    /// <summary>Whether the file downloaded successfully.</summary>
    public bool IsCompleted => Status == DownloadItemStatus.Completed;

    /// <summary>Whether the file download failed.</summary>
    public bool IsFailed => Status == DownloadItemStatus.Failed;

    partial void OnStatusChanged(DownloadItemStatus value)
    {
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsFailed));
    }
}
