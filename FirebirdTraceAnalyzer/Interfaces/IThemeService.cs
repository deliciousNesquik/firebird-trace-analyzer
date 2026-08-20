using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Defines a service for managing application themes.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Apply the specified theme to the application.
    /// </summary>
    /// <param name="theme">The theme to apply. <see cref="AppTheme"/></param>
    void Apply(AppTheme theme);
}
