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
    
    /// <summary>
    /// Контрастный режим оформления приложения. Наследуется от Dark, чтобы хром стандартных
    /// контролов FluentTheme (ListBox, ComboBox, TextBox, ScrollBar, Expander) рендерился тёмным
    /// и не «светил» поверх чёрной кастомной палитры.
    /// </summary>
    public static ThemeVariant Contrast { get; } = new("Contrast", ThemeVariant.Dark);
    
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
            AppTheme.Contrast => Contrast,
            _ => ThemeVariant.Default
        };

        Logger.Info("Theme applied: {Theme}", theme);
    }
}
