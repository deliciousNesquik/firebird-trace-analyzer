using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Window;
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

    private readonly EventStoreDispatcher _dispatcher;
    private readonly IWindowProvider _windowProvider;

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

    // Разбивка размера (грузится по кнопке — тяжёлый полный скан).
    [ObservableProperty] private bool _isBreakdownLoaded;
    [ObservableProperty] private bool _isBreakdownLoading;
    [ObservableProperty] private string _sqlTextInfo = "—";
    [ObservableProperty] private string _paramInfo = "—";
    [ObservableProperty] private string _errorInfo = "—";
    [ObservableProperty] private string _otherInfo = "—";
    [ObservableProperty] private string _eventRowsText = "—";
    [ObservableProperty] private string _attachmentRowsText = "—";
    [ObservableProperty] private string _perfRowsText = "—";

    /// <summary>Конструктор только для XAML-дизайнера.</summary>
    public StoreManagementViewModel()
    {
        _dispatcher = null!;
        _windowProvider = null!;
    }

    public StoreManagementViewModel(EventStoreDispatcher dispatcher, IWindowProvider windowProvider)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));
    }

    /// <summary>Первичная загрузка статистики и списка файлов (вызывать до показа диалога).</summary>
    public Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        // Разбивка размера могла устареть после удаления/очистки/импорта — просим перезагрузить.
        IsBreakdownLoaded = false;

        IsBusy = true;
        StatusMessage = Loc.Tr("Store.Manage.Loading");
        try
        {
            var stats = await _dispatcher.RunAsync(store => store.GetStatistics());
            var files = await _dispatcher.RunAsync(store => store.ListFiles());

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
        StatusMessage = string.Format(Loc.Tr("Store.Manage.Deleting"), hashes.Count);
        try
        {
            await _dispatcher.RunAsync(store =>
            {
                foreach (var hash in hashes)
                    store.DeleteFile(hash);
            });

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
        StatusMessage = Loc.Tr("Store.Manage.Clearing");
        try
        {
            await _dispatcher.RunAsync(store => store.Clear());

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

    /// <summary>Экспортирует выбранные файлы (или все, если ничего не выбрано) в отдельный файл-хранилище.</summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Files.Count == 0)
        {
            StatusMessage = Loc.Tr("Store.Manage.NothingToExport");
            return;
        }

        var topLevel = _windowProvider.GetCurrent();
        if (topLevel?.StorageProvider is null)
            return;

        var picked = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc.Tr("Store.Manage.ExportTitle"),
            SuggestedFileName = "events-export.db",
            DefaultExtension = "db",
            FileTypeChoices = [new FilePickerFileType(Loc.Tr("Store.Manage.StoreFileType")) { Patterns = ["*.db"] }]
        });
        if (picked is null)
            return;

        var selected = Files.Where(f => f.IsSelected).Select(f => f.File).ToList();
        var toExport = selected.Count > 0 ? selected : Files.Select(f => f.File).ToList();
        var path = picked.Path.LocalPath;

        IsBusy = true;
        StatusMessage = Loc.Tr("Store.Manage.Exporting");
        try
        {
            await _dispatcher.RunAsync(store => store.ExportTo(path, toExport));

            StatusMessage = string.Format(Loc.Tr("Store.Manage.Exported"), toExport.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Store management: export failed");
            StatusMessage = string.Format(Loc.Tr("Store.Manage.Error"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Импортирует файлы из другого файла-хранилища (файлы с существующим хэшем пропускаются).</summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        var topLevel = _windowProvider.GetCurrent();
        if (topLevel?.StorageProvider is null)
            return;

        var picked = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.Tr("Store.Manage.ImportTitle"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Loc.Tr("Store.Manage.StoreFileType")) { Patterns = ["*.db"] }]
        });
        if (picked.Count == 0)
            return;

        var path = picked[0].Path.LocalPath;
        var imported = 0;

        IsBusy = true;
        StatusMessage = Loc.Tr("Store.Manage.Importing");
        try
        {
            imported = await _dispatcher.RunAsync(store => store.ImportFrom(path));

            StatusMessage = string.Format(Loc.Tr("Store.Manage.Imported"), imported);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Store management: import failed");
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

    /// <summary>Грузит разбивку размера по запросу (полный скан таблиц — недёшево).</summary>
    [RelayCommand]
    private async Task LoadSizeBreakdownAsync()
    {
        IsBreakdownLoading = true;
        try
        {
            var b = await _dispatcher.RunAsync(store => store.GetSizeBreakdown());

            SqlTextInfo = $"{ByteSizeFormatter.FormatBytes(b.SqlTextBytes)} • {b.SqlTextRows:N0}";
            ParamInfo = $"{ByteSizeFormatter.FormatBytes(b.ParameterBytes)} • {b.ParameterRows:N0}";
            ErrorInfo = $"{ByteSizeFormatter.FormatBytes(b.ErrorMessageBytes)} • {b.ErrorLineRows:N0}";
            OtherInfo = ByteSizeFormatter.FormatBytes(b.OtherBytes);
            EventRowsText = b.EventRows.ToString("N0");
            AttachmentRowsText = b.AttachmentRows.ToString("N0");
            PerfRowsText = b.PerfItemRows.ToString("N0");

            IsBreakdownLoaded = true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Store management: size breakdown failed");
            StatusMessage = string.Format(Loc.Tr("Store.Manage.Error"), ex.Message);
        }
        finally
        {
            IsBreakdownLoading = false;
        }
    }

    /// <summary>
    /// Закрывает окно, вернув выбранные файлы для загрузки в рабочую сессию (чтение их событий из
    /// хранилища и добавление карточек выполняет владелец диалога). Для накопительного архива это
    /// способ подгрузить нужный срез без повторного парсинга.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoadSelected))]
    private void LoadSelected()
    {
        var selected = Files.Where(f => f.IsSelected).Select(f => f.File).ToList();
        if (selected.Count == 0)
            return;

        CloseRequested?.Invoke(this, selected);
    }

    private bool CanLoadSelected() => SelectedCount > 0 && !IsBusy;

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
        LoadSelectedCommand.NotifyCanExecuteChanged();
    }

    private bool CanDeleteSelected() => SelectedCount > 0 && !IsBusy;

    private bool CanClearAll() => Files.Count > 0 && !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        ClearAllCommand.NotifyCanExecuteChanged();
        LoadSelectedCommand.NotifyCanExecuteChanged();
    }
}
