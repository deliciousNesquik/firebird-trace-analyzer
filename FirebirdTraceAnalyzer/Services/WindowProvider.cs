using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Window;

namespace FirebirdTraceAnalyzer.Services;

public class WindowProvider: IWindowProvider
{
    public TopLevel? GetCurrent()
    {
        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Возвращаем активное окно, а не только главное
            return desktop.Windows.FirstOrDefault(w => w.IsActive)
                   ?? desktop.MainWindow;
        }

        return null;
    }
}