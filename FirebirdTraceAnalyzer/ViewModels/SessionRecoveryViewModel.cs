using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// Диалог восстановления сессии при старте: показывает файлы, найденные в хранилище событий,
/// и даёт выбрать, какие вернуть в рабочий набор (чтение с диска, без повторного парсинга).
/// Результат — список выбранных манифестов <see cref="TraceFileInfoModel"/> или <c>null</c> (пропуск).
/// </summary>
public partial class SessionRecoveryViewModel : ViewModelBase, IDialogViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>Диалог просит закрыться. Аргумент — выбранные файлы или <c>null</c> (пропуск).</summary>
    public event EventHandler<object?>? CloseRequested;

    public ObservableCollection<RestorableFileInfo> Files { get; } = [];

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private long _totalEvents;

    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>Заполняет диалог списком файлов из манифеста хранилища.</summary>
    public void Initialize(IEnumerable<TraceFileInfoModel> files)
    {
        foreach (var file in files)
        {
            var item = new RestorableFileInfo(file);
            item.PropertyChanged += OnItemChanged;
            Files.Add(item);
        }

        UpdateStatistics();
        Summary = string.Format(Loc.Tr("Recovery.FoundSummary"), Files.Count);
        Logger.Info("Session recovery dialog: {Count} restorable file(s)", Files.Count);
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RestorableFileInfo.IsSelected))
            UpdateStatistics();
    }

    private void UpdateStatistics()
    {
        var selected = Files.Where(f => f.IsSelected).ToList();
        SelectedCount = selected.Count;
        TotalEvents = selected.Sum(f => f.EventCount);
        RestoreCommand.NotifyCanExecuteChanged();
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

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private void Restore()
    {
        var selected = Files.Where(f => f.IsSelected).Select(f => f.File).ToList();
        Logger.Info("Session recovery: restoring {Count} file(s)", selected.Count);
        CloseRequested?.Invoke(this, selected);
    }

    private bool CanRestore() => SelectedCount > 0;

    [RelayCommand]
    private void Skip() => CloseRequested?.Invoke(this, null);
}
