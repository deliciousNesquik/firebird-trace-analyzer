using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Применяет режим оформления к приложению (Application.RequestedThemeVariant).
/// </summary>
public interface IThemeService
{
    /// <summary>Применяет выбранную тему. Вызывать на UI-потоке.</summary>
    void Apply(AppTheme theme);
}
