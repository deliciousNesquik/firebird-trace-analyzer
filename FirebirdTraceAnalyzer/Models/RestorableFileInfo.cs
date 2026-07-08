using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Строка списка восстановления сессии: файл, найденный в хранилище событий и доступный для
/// загрузки обратно в рабочий набор. Оборачивает манифест <see cref="TraceFileInfoModel"/>,
/// добавляя флаг выбора и человекочитаемое форматирование для диалога.
/// </summary>
public partial class RestorableFileInfo : ViewModelBase
{
    public RestorableFileInfo(TraceFileInfoModel file) => File = file;

    /// <summary>Исходная запись манифеста хранилища.</summary>
    public TraceFileInfoModel File { get; }

    /// <summary>Выбран ли файл для восстановления (по умолчанию — да).</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    public string FileName => File.FileName;
    public long EventCount => File.EventCount;
    public string FormattedSize => ByteSizeFormatter.FormatBytes(File.FileSize);
    public string TimeRange => $"{File.StartTrace:yyyy-MM-dd HH:mm:ss} — {File.EndTrace:HH:mm:ss}";
}
