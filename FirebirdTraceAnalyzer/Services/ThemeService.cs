using Avalonia;
using Avalonia.Styling;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Применяет режим оформления к приложению. Auto → ThemeVariant.Default (следует за ОС),
/// Light/Dark — принудительно. Брауши тем заданы в App.axaml (ThemeDictionaries) и переключаются
/// динамически при смене RequestedThemeVariant.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null)
        {
            Logger.Warn("Cannot apply theme: Application.Current is null");
            return;
        }

        app.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        Logger.Info("Theme applied: {Theme}", theme);
    }
}
