using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Interfaces.Window;

/// <summary>
/// Defines a provider for obtaining the current top-level window in an Avalonia application.
/// </summary>
public interface IWindowProvider
{
    /// <summary>
    /// Gets the current top-level window. <see cref="TopLevel"/>
    /// </summary>
    /// <returns>The current top-level window or null if none is available.</returns>
    TopLevel? GetCurrent();
}