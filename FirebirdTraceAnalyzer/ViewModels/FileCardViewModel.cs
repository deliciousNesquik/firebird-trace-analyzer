using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.ViewModels;

public partial class FileCardViewModel : ViewModelBase
{
    // Используем Func<Task> вместо Action — позволяет правильно await'ить async операции
    private readonly Func<FileCardViewModel, Task> _onRemoveRequested;
    private readonly Func<FileCardViewModel, Task> _onOpenRequested;

    [ObservableProperty]
    public partial TraceFileInfoModel FileInfo { get; set; }

    public FileCardViewModel(
        TraceFileInfoModel fileInfo,
        Func<FileCardViewModel, Task> onRemoveRequested,
        Func<FileCardViewModel, Task> onOpenRequested)
    {
        FileInfo = fileInfo;
        _onRemoveRequested = onRemoveRequested ?? throw new ArgumentNullException(nameof(onRemoveRequested));
        _onOpenRequested = onOpenRequested ?? throw new ArgumentNullException(nameof(onOpenRequested));
    }

    [RelayCommand]
    private Task RemoveFileAsync() => _onRemoveRequested(this);

    [RelayCommand]
    private Task OpenFileAsync() => _onOpenRequested(this);
}