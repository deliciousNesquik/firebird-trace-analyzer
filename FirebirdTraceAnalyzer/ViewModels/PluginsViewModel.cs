using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
    private readonly IDialogService _dialogService;

    /// <summary>Стало ли какое-то изменение (вкл/выкл/установка/удаление), требующее перезапуска.</summary>
    [ObservableProperty] private bool _restartNeeded;

    /// <summary>Показывать ли раздел коллизий вместо обычного списка плагинов.</summary>
    [ObservableProperty] private bool _showCollisions;

    /// <summary>Есть ли вообще коллизии (несколько плагинов с одним Id).</summary>
    [ObservableProperty] private bool _hasCollisions;

    public ObservableCollection<PluginRow> Plugins { get; } = new();

    /// <summary>Группы коллизий: по одной на каждый конфликтующий Id (пары/наборы плагинов).</summary>
    public ObservableCollection<CollisionGroupRow> CollisionGroups { get; } = new();

    public event EventHandler<object?>? CloseRequested;

    public PluginsViewModel(
        PluginManagerService pluginManager,
        IFileDialogService fileDialogService,
        IDialogService dialogService)
    {
        _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public PluginsViewModel()
    {
        _pluginManager = null!;
        _fileDialogService = null!;
        _dialogService = null!;
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

        BuildCollisionGroups();
    }

    /// <summary>Строит группы коллизий из снимка загрузчика и предвыбор в каждой группе.</summary>
    private void BuildCollisionGroups()
    {
        foreach (var group in CollisionGroups)
            foreach (var choice in group.Choices)
                choice.PropertyChanged -= OnCollisionChoiceChanged;
        CollisionGroups.Clear();

        foreach (var group in _pluginManager.GetCollisionGroups())
        {
            var id = group[0].Id;

            // Новые версии — выше; так первым идёт авто-победитель «старшая версия».
            var ordered = group
                .OrderByDescending(p => p.ParsedVersion ?? new Version(0, 0))
                .ToList();

            // Предвыбор ставим ТОЛЬКО если коллизия реально разрешена — включён ровно один экземпляр
            // (остальные выключены пользователем). Если включены все (выбор ещё не сделан), не
            // отмечаем ничего: иначе радио выглядело бы как готовое решение, хотя ничего не записано.
            // Берём актуальное состояние из менеджера (а не стартовый Status) — чтобы после выбора
            // в этой же сессии пересборка групп отражала выбор.
            var enabledCount = ordered.Count(p => _pluginManager.IsEnabled(p.FilePath, p.Id));
            var choices = ordered
                .Select(info => new CollisionChoiceRow(
                    info,
                    enabledCount == 1 && _pluginManager.IsEnabled(info.FilePath, info.Id)))
                .ToList();

            var groupRow = new CollisionGroupRow(id, choices);
            foreach (var choice in choices)
                choice.PropertyChanged += OnCollisionChoiceChanged;

            CollisionGroups.Add(groupRow);
        }

        HasCollisions = CollisionGroups.Count > 0;
        if (!HasCollisions)
            ShowCollisions = false;
    }

    private void OnCollisionChoiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Реагируем только на выбор (переход в true): оставить выбранную DLL, остальные в группе —
        // выключить. Это ровно тот же механизм «включено/выключено», что и в основном списке.
        if (e.PropertyName != nameof(CollisionChoiceRow.IsSelected) ||
            sender is not CollisionChoiceRow choice || !choice.IsSelected)
            return;

        var group = CollisionGroups.FirstOrDefault(g => g.Choices.Contains(choice));
        if (group is null)
            return;

        foreach (var c in group.Choices)
        {
            var enabled = ReferenceEquals(c, choice);
            // Ключ — экземпляр (путь+Id): выключаем только этот класс, а не всю DLL. Так у одной DLL
            // можно оставить, напр., фильтр и выключить её сортировку.
            _pluginManager.SetEnabled(c.FilePath, c.Id, enabled);

            // Синхронизируем чекбокс в основном списке (совпадение по паре путь+Id).
            var mainRow = Plugins.FirstOrDefault(p =>
                string.Equals(p.FilePath, c.FilePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Id, c.Id, StringComparison.OrdinalIgnoreCase));
            if (mainRow is not null && mainRow.IsEnabled != enabled)
            {
                mainRow.PropertyChanged -= OnRowChanged; // избегаем повторного SetEnabled
                mainRow.IsEnabled = enabled;
                mainRow.PropertyChanged += OnRowChanged;
            }
        }

        RestartNeeded = true;
        Logger.Info("Collision for Id '{Id}' resolved to '{File}' (others disabled, applies after restart)",
            choice.Id, choice.FilePath);
    }

    /// <summary>Переключает отображение раздела коллизий.</summary>
    [RelayCommand]
    private void ToggleCollisions() => ShowCollisions = !ShowCollisions;

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PluginRow.IsEnabled) || sender is not PluginRow row)
            return;

        // Персистим выбор по экземпляру (путь+Id); фактически применится при следующем запуске.
        _pluginManager.SetEnabled(row.FilePath, row.Id, row.IsEnabled);
        RestartNeeded = true;
        Logger.Info("Plugin '{Id}' ({File}) set enabled={Enabled} (applies after restart)",
            row.Id, row.FilePath, row.IsEnabled);
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

    /// <summary>
    /// Устанавливает плагин: выбор пакета (.dll или .zip) → DLL копируется в новую подпапку,
    /// ZIP распаковывается в подпапку по имени архива. Прочие форматы отклоняются.
    /// </summary>
    [RelayCommand]
    private async Task InstallAsync()
    {
        var path = await _fileDialogService.PickPluginPackageAsync();
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (_pluginManager.InstallPlugin(path))
        {
            RestartNeeded = true;
            Logger.Info("Installed plugin from '{Path}' (applies after restart)", path);
            await _dialogService.ShowDialogAsync<object>(new ConfirmDialogViewModel(
                "Plugin installed",
                $"'{Path.GetFileName(path)}' has been installed. It will become available after restarting the application.",
                confirmText: "OK",
                cancelText: "Close"));
        }
        else
        {
            await _dialogService.ShowDialogAsync<object>(new ConfirmDialogViewModel(
                "Install failed",
                "Could not install the selected file. Only a plugin .dll or a .zip archive containing a plugin .dll are accepted. See the log for details.",
                confirmText: "OK",
                cancelText: "Close"));
        }
    }

    /// <summary>
    /// Удаляет пакет плагина целиком (всю подпапку). Перед удалением показывает подтверждение
    /// со списком ВСЕХ плагинов из этой папки (в одном файле может быть несколько классов).
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync(PluginRow? row)
    {
        if (row is null)
            return;

        // Все плагины из той же подпапки — они удалятся вместе (созависимые объекты).
        var bundled = Plugins
            .Where(p => string.Equals(p.FolderPath, row.FolderPath, StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.Name}  (Id: {p.Id}, v{p.Version})")
            .ToList();

        var confirmed = await _dialogService.ShowDialogAsync<bool>(new ConfirmDialogViewModel(
            "Delete plugin package?",
            $"The whole package folder '{row.DirectoryName}' will be deleted. " +
            "The following plugins are bundled in it and will be removed together:",
            details: bundled,
            confirmText: "Delete",
            cancelText: "Cancel",
            isDanger: true));

        if (!confirmed)
            return;

        var (deletedNow, pending) = _pluginManager.DeletePackage(row.FolderPath);

        if (deletedNow || pending)
        {
            RestartNeeded = true;

            // Убираем из списка строки того же пакета (снимок загрузчика обновится на старте).
            var toRemove = Plugins
                .Where(p => string.Equals(p.FolderPath, row.FolderPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var r in toRemove)
            {
                r.PropertyChanged -= OnRowChanged;
                Plugins.Remove(r);
            }

            // Удалённый пакет мог участвовать в коллизии — пересобираем группы.
            BuildCollisionGroups();
        }

        if (pending)
        {
            await _dialogService.ShowDialogAsync<object>(new ConfirmDialogViewModel(
                "Deletion scheduled",
                "The plugin is currently loaded and its files are locked. " +
                "It will be removed automatically the next time the application starts.",
                confirmText: "OK",
                cancelText: "Close"));
        }
        else if (!deletedNow)
        {
            await _dialogService.ShowDialogAsync<object>(new ConfirmDialogViewModel(
                "Delete failed",
                "Could not delete the plugin package. See the log for details.",
                confirmText: "OK",
                cancelText: "Close"));
        }
    }

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
    public string FolderPath => Path.GetDirectoryName(_info.FilePath) ?? _info.FilePath;

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

/// <summary>Группа коллизии: один конфликтующий Id и конкурирующие за него плагины (пара/набор).</summary>
public sealed class CollisionGroupRow
{
    public CollisionGroupRow(string id, IReadOnlyList<CollisionChoiceRow> choices)
    {
        Id = id;
        Choices = choices;
    }

    public string Id { get; }
    public IReadOnlyList<CollisionChoiceRow> Choices { get; }

    /// <summary>Имя группы для взаимоисключающих radio-кнопок (уникально по Id).</summary>
    public string GroupName => $"collision::{Id}";
}

/// <summary>Один вариант в группе коллизии: конкретная DLL, которую можно выбрать активной.</summary>
public partial class CollisionChoiceRow : ObservableObject
{
    private readonly PluginInfo _info;

    public CollisionChoiceRow(PluginInfo info, bool isSelected)
    {
        _info = info;
        _isSelected = isSelected;
    }

    /// <summary>Выбран ли этот вариант как активный для своего Id.</summary>
    [ObservableProperty] private bool _isSelected;

    public string Id => _info.Id;
    public string Name => _info.Name;
    public string Author => _info.Author;
    public string Version => _info.Version;
    public string FilePath => _info.FilePath;
    public string DirectoryName => _info.DirectoryName;
}
