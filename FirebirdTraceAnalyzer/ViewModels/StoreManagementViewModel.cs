using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services.Persistence;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// Диалог управления хранилищем событий: сводная статистика (размер БД, сжатие, уникальность,
/// диапазон) + список файлов с возможностью удалить выбранные или очистить всё. Все обращения к
/// хранилищу сериализованы тем же шлюзом, что и запись/чтение (одно SQLite-соединение).
/// </summary>
public partial class StoreManagementViewModel : ViewModelBase, IDialogViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IEventStore _store;
    private readonly SemaphoreSlim _gate;

    public event EventHandler<object?>? CloseRequested;

    public ObservableCollection<RestorableFileInfo> Files { get; } = [];

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _selectedCount;

    // Статистика (форматированная для отображения).
    [ObservableProperty] private int _fileCount;
    [ObservableProperty] private long _eventCount;
    [ObservableProperty] private long _uniqueSqlCount;
    [ObservableProperty] private long _uniqueAttachmentCount;
    [ObservableProperty] private string _rangeText = string.Empty;
    [ObservableProperty] private string _dbSizeText = string.Empty;
    [ObservableProperty] private string _rawSizeText = string.Empty;
    [ObservableProperty] private string _compressionText = string.Empty;

    /// <summary>Конструктор только для XAML-дизайнера.</summary>
    public StoreManagementViewModel()
    {
        _store = null!;
        _gate = null!;
    }

    public StoreManagementViewModel(IEventStore store, SemaphoreSlim gate)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    /// <summary>Первичная загрузка статистики и списка файлов (вызывать до показа диалога).</summary>
    public Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            EventStoreStatistics stats;
            IReadOnlyList<TraceFileInfoModel> files;

            await _gate.WaitAsync();
            try
            {
                stats = await Task.Run(() => _store.GetStatistics());
                files = await Task.Run(() => _store.ListFiles());
            }
            finally
            {
                _gate.Release();
            }

            foreach (var item in Files)
                item.PropertyChanged -= OnItemChanged;
            Files.Clear();
            foreach (var file in files)
            {
                var item = new RestorableFileInfo(file, selected: false);
                item.PropertyChanged += OnItemChanged;
                Files.Add(item);
            }

            ApplyStatistics(stats);
            UpdateSelection();
            StatusMessage = string.Format(Loc.Tr("Store.Manage.LoadedSummary"), FileCount);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Store management: refresh failed");
            StatusMessage = string.Format(Loc.Tr("Store.Manage.Error"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        var hashes = Files.Where(f => f.IsSelected).Select(f => f.File.FileHash).ToList();
        if (hashes.Count == 0)
            return;

        IsBusy = true;
        try
        {
            await _gate.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    foreach (var hash in hashes)
                        _store.DeleteFile(hash);
                });
            }
            finally
            {
                _gate.Release();
            }

            Logger.Info("Store management: deleted {Count} file(s)", hashes.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Store management: delete failed");
            StatusMessage = string.Format(Loc.Tr("Store.Manage.Error"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanClearAll))]
    private async Task ClearAllAsync()
    {
        IsBusy = true;
        try
        {
            await _gate.WaitAsync();
            try
            {
                await Task.Run(() => _store.Clear());
            }
            finally
            {
                _gate.Release();
            }

            Logger.Info("Store management: cleared all");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Store management: clear failed");
            StatusMessage = string.Format(Loc.Tr("Store.Manage.Error"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var file in Files)
            file.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var file in Files)
            file.IsSelected = false;
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, null);

    private void ApplyStatistics(EventStoreStatistics stats)
    {
        FileCount = stats.FileCount;
        EventCount = stats.EventCount;
        UniqueSqlCount = stats.UniqueSqlCount;
        UniqueAttachmentCount = stats.UniqueAttachmentCount;
        DbSizeText = ByteSizeFormatter.FormatBytes(stats.DbSizeBytes);
        RawSizeText = ByteSizeFormatter.FormatBytes(stats.RawSizeBytes);
        CompressionText = stats.CompressionRatio > 0 ? $"{stats.CompressionRatio:0.##}×" : "—";
        RangeText = stats is { RangeStart: { } start, RangeEnd: { } end }
            ? $"{start:yyyy-MM-dd HH:mm:ss} — {end:yyyy-MM-dd HH:mm:ss}"
            : "—";
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RestorableFileInfo.IsSelected))
            UpdateSelection();
    }

    private void UpdateSelection()
    {
        SelectedCount = Files.Count(f => f.IsSelected);
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        ClearAllCommand.NotifyCanExecuteChanged();
    }

    private bool CanDeleteSelected() => SelectedCount > 0 && !IsBusy;

    private bool CanClearAll() => Files.Count > 0 && !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        ClearAllCommand.NotifyCanExecuteChanged();
    }
}
