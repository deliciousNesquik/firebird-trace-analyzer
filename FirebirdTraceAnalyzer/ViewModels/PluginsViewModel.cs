using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Services.Plugins;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel встроенного окна управления плагинами: список установленных плагинов с метаданными
/// (Id, автор, версия, что предоставляет, статус), включение/выключение (применяется после
/// перезапуска) и открытие папки. Показывается как in-window overlay.
/// </summary>
public partial class PluginsViewModel : ViewModelBase, IDialogViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly PluginManagerService _pluginManager;
    private readonly IFileDialogService _fileDialogService;

    /// <summary>Стало ли какое-то изменение (вкл/выкл), требующее перезапуска.</summary>
    [ObservableProperty] private bool _restartNeeded;

    public ObservableCollection<PluginRow> Plugins { get; } = new();

    public event EventHandler<object?>? CloseRequested;

    public PluginsViewModel(PluginManagerService pluginManager, IFileDialogService fileDialogService)
    {
        _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
    }

    public PluginsViewModel()
    {
        _pluginManager = null!;
        _fileDialogService = null!;
    }

    /// <summary>Заполняет список из текущего снимка загрузчика (загрузка была на старте).</summary>
    public void LoadPlugins()
    {
        foreach (var row in Plugins)
            row.PropertyChanged -= OnRowChanged;
        Plugins.Clear();

        foreach (var info in _pluginManager.GetPlugins())
        {
            var row = new PluginRow(info);
            row.PropertyChanged += OnRowChanged;
            Plugins.Add(row);
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PluginRow.IsEnabled) || sender is not PluginRow row)
            return;

        // Персистим выбор; фактически применится при следующем запуске.
        _pluginManager.SetEnabled(row.Id, row.IsEnabled);
        RestartNeeded = true;
        Logger.Info("Plugin '{Id}' set enabled={Enabled} (applies after restart)", row.Id, row.IsEnabled);
    }

    /// <summary>Открывает папку с DLL плагина (выделяет файл).</summary>
    [RelayCommand]
    private async Task RevealAsync(PluginRow? row)
    {
        if (row is not null)
            await _fileDialogService.RevealInFileManagerAsync(row.FilePath);
    }

    /// <summary>Открывает каталог плагинов.</summary>
    [RelayCommand]
    private async Task OpenPluginsFolderAsync()
        => await _fileDialogService.RevealInFileManagerAsync(_pluginManager.PluginsDirectory);

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, null);
}

/// <summary>Строка списка плагинов: обёртка над <see cref="PluginInfo"/> с редактируемым «включён».</summary>
public partial class PluginRow : ObservableObject
{
    private readonly PluginInfo _info;

    public PluginRow(PluginInfo info)
    {
        _info = info;
        _isEnabled = info.Status != PluginStatus.Disabled;
    }

    [ObservableProperty] private bool _isEnabled;

    public string Id => _info.Id;
    public string Name => _info.Name;
    public string Author => _info.Author;
    public string Version => _info.Version;
    public string FilePath => _info.FilePath;
    public string DirectoryName => _info.DirectoryName;

    public string KindText => _info.Kind switch
    {
        PluginKind.Sort => "Sort",
        PluginKind.Filter => "Filter",
        PluginKind.Sort | PluginKind.Filter => "Sort + Filter",
        _ => "—"
    };

    public string StatusText => _info.Status switch
    {
        PluginStatus.Active => "Active",
        PluginStatus.Disabled => "Disabled",
        PluginStatus.Shadowed => "Shadowed (older version)",
        PluginStatus.LoadError => "Load error",
        _ => "—"
    };

    public bool IsLoadError => _info.Status == PluginStatus.LoadError;
    public bool IsShadowed => _info.Status == PluginStatus.Shadowed;
    public string? LoadError => _info.LoadError;
    public bool HasLoadError => !string.IsNullOrWhiteSpace(_info.LoadError);
}
