using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.Interfaces.Window;

public interface IWindowProvider
{
    TopLevel? GetCurrent();
}