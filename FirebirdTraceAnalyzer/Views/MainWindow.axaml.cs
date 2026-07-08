using System;
using Avalonia;
using Avalonia.Controls;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FirebirdTraceAnalyzer.Views;

public partial class MainWindow : Window
{
    private readonly ISettingsService? _settingsService;

    public MainWindow()
    {
        InitializeComponent();

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
