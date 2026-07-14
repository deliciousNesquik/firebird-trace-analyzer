using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Services;
using FirebirdTraceAnalyzer.ViewModels;
using FirebirdTraceAnalyzer.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FirebirdTraceAnalyzer;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services HERE
        Services = Program.ConfigureServices();

        // Применяем сохранённую тему и язык до создания окна (Auto = следовать ОС).
        var settings = Services.GetRequiredService<ISettingsService>();
        Services.GetRequiredService<IThemeService>().Apply(settings.App.Theme);
        Services.GetRequiredService<ILocalizationService>().SetLanguage(settings.App.Language);

        // Применяем пользовательские пути логов (если заданы) — File-таргеты подхватят на следующей записи
        LogConfiguration.Apply(settings.App.AppLogPath, settings.App.ParserLogPath);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Получаем MainWindowViewModel из DI
            var mainViewModel = Services.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // Cleanup при закрытии приложения
            desktop.Exit += OnApplicationExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        // Освобождаем ресурсы DI контейнера
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}