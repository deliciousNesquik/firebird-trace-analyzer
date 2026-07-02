using CommunityToolkit.Mvvm.ComponentModel;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>Состояние файла в очереди скачивания.</summary>
public enum DownloadItemStatus
{
    Pending,
    Downloading,
    Completed,
    Failed
}

/// <summary>
/// Один файл в списке загрузки: имя + текущий статус (для наглядного отображения прогресса
/// по каждому файлу в окне скачивания).
/// </summary>
public partial class DownloadFileItem : ObservableObject
{
    public required string FileName { get; init; }

    [ObservableProperty]
    private DownloadItemStatus _status = DownloadItemStatus.Pending;

    // Удобные флаги для биндинга видимости статус-иконок в XAML (без конвертеров).
    public bool IsPending => Status == DownloadItemStatus.Pending;
    public bool IsDownloading => Status == DownloadItemStatus.Downloading;
    public bool IsCompleted => Status == DownloadItemStatus.Completed;
    public bool IsFailed => Status == DownloadItemStatus.Failed;

    partial void OnStatusChanged(DownloadItemStatus value)
    {
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsFailed));
    }
}
