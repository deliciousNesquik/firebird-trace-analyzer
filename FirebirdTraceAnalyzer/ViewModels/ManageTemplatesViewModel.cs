using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Interfaces.Dialogs;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Window;
using FirebirdTraceAnalyzer.Models.Reports;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel встроенного окна управления кастомными шаблонами отчётов: список пользовательских
/// шаблонов с действиями просмотреть (открыть JSON в проводнике) / редактировать / экспортировать /
/// удалить, плюс импорт и создание. Показывается как in-window overlay (IDialogViewModel).
/// View/Delete/Export/Import выполняются здесь; Edit/Create делегируются главному окну через
/// события (нужны сессия событий и окно редактора).
/// </summary>
public partial class ManageTemplatesViewModel : ViewModelBase, IDialogViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IReportTemplateService _templateService;
    private readonly IFileDialogService _fileDialogService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "Ready";

    public ObservableCollection<TemplateItem> Templates { get; } = new();

    /// <summary>Диалог просит закрыться.</summary>
    public event EventHandler<object?>? CloseRequested;

    /// <summary>Запрос на редактирование шаблона (id) — обрабатывает главное окно.</summary>
    public event EventHandler<string>? EditRequested;

    /// <summary>Запрос на создание нового шаблона — обрабатывает главное окно.</summary>
    public event EventHandler? CreateRequested;

    public ManageTemplatesViewModel(
        IReportTemplateService templateService,
        IFileDialogService fileDialogService)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
    }

    public ManageTemplatesViewModel()
    {
        _templateService = null!;
        _fileDialogService = null!;
    }

    /// <summary>Загружает список пользовательских шаблонов с диска.</summary>
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            Templates.Clear();

            var customTemplates = await _templateService.GetCustomTemplatesAsync();

            foreach (var template in customTemplates)
            {
                var path = await _templateService.GetCustomTemplatePathAsync(template.Id);
                Templates.Add(new TemplateItem { Template = template, FilePath = path ?? string.Empty });
            }

            StatusMessage = Templates.Count == 0
                ? "No custom templates yet"
                : $"{Templates.Count} template(s)";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading custom templates");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, null);

    /// <summary>Просмотр: открывает JSON-файл шаблона в файловом менеджере.</summary>
    [RelayCommand]
    private async Task ViewAsync(TemplateItem? item)
    {
        if (item is null)
            return;

        if (string.IsNullOrWhiteSpace(item.FilePath))
        {
            StatusMessage = "Template file not found on disk";
            return;
        }

        await _fileDialogService.RevealInFileManagerAsync(item.FilePath);
    }

    [RelayCommand]
    private async Task DeleteAsync(TemplateItem? item)
    {
        if (item is null)
            return;

        try
        {
            await _templateService.DeleteTemplateAsync(item.Template.Id);
            Templates.Remove(item);
            StatusMessage = $"Deleted: {item.Name}";
            Logger.Info("Deleted custom template: {Name}", item.Name);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting template {Name}", item.Name);
            StatusMessage = $"Error deleting: {ex.Message}";
        }
    }

    /// <summary>Экспорт шаблона в выбранный JSON-файл (чтобы поделиться).</summary>
    [RelayCommand]
    private async Task ExportAsync(TemplateItem? item)
    {
        if (item is null)
            return;

        try
        {
            var path = await _fileDialogService.PickJsonToSaveAsync($"{item.Name}.json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            await _templateService.ExportTemplateAsync(item.Template, path);
            StatusMessage = $"Exported: {item.Name}";
            Logger.Info("Exported template {Name} to {Path}", item.Name, path);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting template {Name}", item.Name);
            StatusMessage = $"Export error: {ex.Message}";
        }
    }

    /// <summary>Импорт шаблона из JSON-файла (создаёт новый шаблон).</summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        try
        {
            var path = await _fileDialogService.PickJsonToOpenAsync();
            if (string.IsNullOrWhiteSpace(path))
                return;

            var imported = await _templateService.ImportTemplateAsync(path);
            StatusMessage = $"Imported: {imported.Name}";
            Logger.Info("Imported template: {Name}", imported.Name);

            await LoadAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error importing template");
            StatusMessage = $"Import error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Edit(TemplateItem? item)
    {
        if (item is not null)
            EditRequested?.Invoke(this, item.Template.Id);
    }

    [RelayCommand]
    private void Create() => CreateRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>Элемент списка: кастомный шаблон + путь к его файлу на диске.</summary>
public partial class TemplateItem : ObservableObject
{
    public required ReportTemplate Template { get; init; }
    public required string FilePath { get; init; }

    public string Name => Template.Name;
    public string Description => Template.Description;
    public string Format => Template.DefaultFormat.ToString();
    public bool HasDescription => !string.IsNullOrWhiteSpace(Template.Description);
}
