using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.ViewModels;
using NLog;


namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel диалога выбора файлов на удалённом сервере
/// </summary>
public partial class RemoteFileSelectionViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    #region Observable Properties

    [ObservableProperty]
    private string _serverInfo = string.Empty;

    [ObservableProperty]
    private string _remoteDirectory = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private long _selectedTotalSize;

    [ObservableProperty]
    private bool _deleteAfterProcessing;

    #endregion

    private ObservableCollection<RemoteFileInfo> AllFiles { get; } = [];
    public RangeObservableCollection<RemoteFileInfo> FilteredFiles { get; } = [];

    /// <summary>Событие подтверждения выбора файлов</summary>
    public event EventHandler<IReadOnlyList<RemoteFileInfo>>? FilesSelected;

    /// <summary>Колбэк получения свежего списка файлов с сервера (задаётся владельцем).</summary>
    private Func<CancellationToken, Task<IReadOnlyList<RemoteFileInfo>>>? _refreshCallback;

    /// <summary>Назначает источник обновления списка файлов.</summary>
    public void SetRefreshCallback(Func<CancellationToken, Task<IReadOnlyList<RemoteFileInfo>>> callback)
        => _refreshCallback = callback;

    public void Initialize(string hostname, int port, string directory, IEnumerable<RemoteFileInfo> files)
    {
        ServerInfo = $"{hostname}:{port}";
        RemoteDirectory = directory;
        
        UnsubscribeFromFiles();
        AllFiles.Clear();
        FilteredFiles.Clear();

        foreach (var file in files)
        {
            AllFiles.Add(file);
            FilteredFiles.Add(file);
            file.PropertyChanged += OnFilePropertyChanged;
        }

        UpdateStatistics();
        StatusMessage = $"Found {AllFiles.Count} file(s)";
        
        Logger.Info("Initialized with {Count} files", AllFiles.Count);
    }
    
    /// <summary>
    /// Обновляет список файлов новыми данными
    /// </summary>
    public void UpdateFileList(IEnumerable<RemoteFileInfo> newFiles)
    {
        // Сохраняем состояние выбора по имени файла
        var previouslySelected = AllFiles
            .Where(f => f.IsSelected)
            .Select(f => f.FileName)
            .ToHashSet();

        UnsubscribeFromFiles();
        AllFiles.Clear();
        FilteredFiles.Clear();

        foreach (var file in newFiles)
        {
            // Восстанавливаем выбор, если файл был выбран ранее
            if (previouslySelected.Contains(file.FileName))
            {
                file.IsSelected = true;
            }

            AllFiles.Add(file);
            file.PropertyChanged += OnFilePropertyChanged;
        }
        
        // Применяем текущий поиск/фильтр
        ApplySearch();
        
        UpdateStatistics();
        StatusMessage = $"Refreshed: {AllFiles.Count} file(s) found";
        
        Logger.Info("File list updated with {Count} files", AllFiles.Count);
    }

    #region Commands

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var file in FilteredFiles)
        {
            file.IsSelected = true;
        }
        
        Logger.Debug("Selected all {Count} files", FilteredFiles.Count);
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var file in FilteredFiles)
        {
            file.IsSelected = false;
        }
        
        Logger.Debug("Deselected all files");
    }

    [RelayCommand]
    private void ToggleFileSelection(RemoteFileInfo? file)
    {
        if (file == null) return;
        
        Logger.Debug("Toggled selection for {FileName}: {IsSelected}", file.FileName, file.IsSelected);
    }

    [RelayCommand]
    private void ApplySearch()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            FilteredFiles.ReplaceRange(AllFiles);
        else
            FilteredFiles.ReplaceRange(AllFiles.Where(file => 
                file.FileName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)));

        StatusMessage = string.IsNullOrWhiteSpace(SearchText)
            ? $"Showing all {FilteredFiles.Count} file(s)"
            : $"Found {FilteredFiles.Count} file(s) matching '{SearchText}'";

        Logger.Debug("Search applied: '{Search}', results: {Count}", SearchText, FilteredFiles.Count);
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        ApplySearch();
    }

    [RelayCommand]
    private void FilterByDate(string period)
    {
        var now = DateTime.Now;
        var startDate = period switch
        {
            "today" => now.Date,
            "yesterday" => now.Date.AddDays(-1),
            "week" => now.Date.AddDays(-7),
            "month" => now.Date.AddMonths(-1),
            _ => DateTime.MinValue
        };
        
        FilteredFiles.ReplaceRange(AllFiles.Where(file => 
            file.LastModified >= startDate && file.LastModified <= now));

        StatusMessage = $"Showing {FilteredFiles.Count} file(s) from {period}";
        Logger.Debug("Date filter applied: {Period}, results: {Count}", period, FilteredFiles.Count);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_refreshCallback is null)
            return;

        IsLoading = true;
        StatusMessage = "Refreshing file list...";
        Logger.Info("Refresh requested");

        try
        {
            var updatedFiles = await _refreshCallback(cancellationToken);
            UpdateFileList(updatedFiles);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Refresh cancelled";
            Logger.Info("Refresh cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error refreshing file list");
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        var selectedFiles = AllFiles.Where(f => f.IsSelected).ToList();
        
        Logger.Info("Confirmed selection: {Count} files, total size: {Size} bytes", 
            selectedFiles.Count, selectedFiles.Sum(f => f.Size));

        FilesSelected?.Invoke(this, selectedFiles);
    }

    private bool CanConfirm()
    {
        return SelectedCount > 0;
    }

    #endregion

    #region Property Changed Handlers

    partial void OnSearchTextChanged(string value)
    {
        // Автоматический поиск при вводе
        if (string.IsNullOrWhiteSpace(value) || value.Length >= 2)
        {
            ApplySearch();
        }
    }

    #endregion

    #region Helper Methods

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RemoteFileInfo.IsSelected))
            UpdateStatistics();
    }

    /// <summary>Снимает подписки со всех текущих файлов (вызывать перед очисткой AllFiles).</summary>
    private void UnsubscribeFromFiles()
    {
        foreach (var file in AllFiles)
            file.PropertyChanged -= OnFilePropertyChanged;
    }

    private void UpdateStatistics()
    {
        var selected = AllFiles.Where(f => f.IsSelected).ToList();
        
        SelectedCount = selected.Count;
        SelectedTotalSize = selected.Sum(f => f.Size);

        ConfirmCommand.NotifyCanExecuteChanged();
    }

    #endregion
}