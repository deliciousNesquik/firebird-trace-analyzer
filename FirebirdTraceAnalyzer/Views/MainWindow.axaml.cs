using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace FirebirdTraceAnalyzer.Views;

public partial class MainWindow : Window
{
    private readonly ISettingsService? _settingsService;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public MainWindow()
    {
        InitializeComponent();

        // Drag-and-drop файлов: цель — корневой Panel (DragDrop.AllowDrop в XAML), обработчики висят
        // на окне и ловят события всплытием.
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        _settingsService = App.Services?.GetService<ISettingsService>();

        if (_settingsService != null)
        {
            ApplyWindowGeometry(_settingsService.Window);
            // Сохраняем геометрию только при закрытии, а не на каждое изменение размера.
            Closing += OnWindowClosing;
        }

        // После показа окна — однократные стартовые подсказки: восстановление прошлой сессии из
        // хранилища (режим Session), затем выбор при неразрешённых коллизиях плагинов.
        Opened += OnOpenedStartupPrompts;
    }

    private async void OnOpenedStartupPrompts(object? sender, EventArgs e)
    {
        Opened -= OnOpenedStartupPrompts;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.PromptSessionRecoveryAsync();
        await vm.PromptUnresolvedCollisionsAsync();

        // Отложенное обслуживание хранилища (чистка сирот + VACUUM после частичных удалений).
        await vm.RunPendingStorageMaintenanceAsync();
    }

    // --- Drag-and-drop файлов -------------------------------------------------------------------

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Avalonia 12: полезная нагрузка — IDataTransfer; наличие файлов проверяем через DataFormat.File.
        var hasFiles = e.DataTransfer is { } dt && dt.Contains(DataFormat.File);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        if (DataContext is MainWindowViewModel vm)
            vm.IsDragOver = hasFiles;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.IsDragOver = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.IsDragOver = false;

        try
        {
            // TryGetFiles() отдаёт и файлы, и папки — берём только файлы (OfType<IStorageFile>).
            var files = e.DataTransfer?.TryGetFiles()?.OfType<IStorageFile>().ToList();
            if (files is not { Count: > 0 })
                return;

            await vm.LoadDroppedFilesAsync(files);
        }
        catch (Exception ex)
        {
            // Обёртка async void: сбой обработки drop не должен ронять приложение
            // (внутренняя загрузка и так ловит ошибки; здесь — страховка на извлечение файлов).
            Logger.Error(ex, "Drag-and-drop file load failed");
        }
    }

    private void ApplyWindowGeometry(WindowSettings ws)
    {
        if (ws.Width is > 0 && ws.Height is > 0)
        {
            Width = ws.Width.Value;
            Height = ws.Height.Value;
        }

        if (ws.X.HasValue && ws.Y.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(ws.X.Value, ws.Y.Value);
        }

        if (ws.Maximized)
            WindowState = WindowState.Maximized;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_settingsService == null)
            return;

        var ws = _settingsService.Window;
        ws.Maximized = WindowState == WindowState.Maximized;

        // Размеры/позицию запоминаем только в обычном состоянии: в развёрнутом окне
        // Width/Height/Position — это габариты на весь экран, их хранить не нужно
        // (прежние «нормальные» значения остаются, чтобы было куда сворачиваться).
        if (WindowState == WindowState.Normal)
        {
            if (!double.IsNaN(Width) && !double.IsNaN(Height))
            {
                ws.Width = Width;
                ws.Height = Height;
            }

            ws.X = Position.X;
            ws.Y = Position.Y;
        }

        _settingsService.Save();
    }
}
