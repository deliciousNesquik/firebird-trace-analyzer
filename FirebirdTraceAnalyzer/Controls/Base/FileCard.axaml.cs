using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace FirebirdTraceAnalyzer.Controls;

public class FileCard : TemplatedControl
{
    public static readonly StyledProperty<string> FileNameProperty =
        AvaloniaProperty.Register<FileCard, string>(nameof(FileName), "");

    public static readonly StyledProperty<DateTime> StartTraceProperty =
        AvaloniaProperty.Register<FileCard, DateTime>(nameof(StartTrace), DateTime.Now);

    public static readonly StyledProperty<DateTime> EndTraceProperty =
        AvaloniaProperty.Register<FileCard, DateTime>(nameof(EndTrace), DateTime.Now);

    public static readonly StyledProperty<long> FileSizeProperty =
        AvaloniaProperty.Register<FileCard, long>(nameof(FileSize), 0);

    public static readonly StyledProperty<long> EventCountProperty =
        AvaloniaProperty.Register<FileCard, long>(nameof(EventCount), 0);

    public static readonly StyledProperty<ICommand?> RemoveFileCommandProperty =
        AvaloniaProperty.Register<FileCard, ICommand?>(nameof(RemoveFileCommand));
    
    public static readonly StyledProperty<ICommand?> OpenFileCommandProperty =
        AvaloniaProperty.Register<FileCard, ICommand?>(nameof(OpenFileCommand));

    public string FileName
    {
        get => GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    public DateTime StartTrace
    {
        get => GetValue(StartTraceProperty);
        set => SetValue(StartTraceProperty, value);
    }

    public DateTime EndTrace
    {
        get => GetValue(EndTraceProperty);
        set => SetValue(EndTraceProperty, value);
    }

    public long FileSize
    {
        get => GetValue(FileSizeProperty);
        set => SetValue(FileSizeProperty, value);
    }

    public long EventCount
    {
        get => GetValue(EventCountProperty);
        set => SetValue(EventCountProperty, value);
    }

    public ICommand? RemoveFileCommand
    {
        get => GetValue(RemoveFileCommandProperty);
        set => SetValue(RemoveFileCommandProperty, value);
    }
    
    public ICommand? OpenFileCommand
    {
        get => GetValue(OpenFileCommandProperty);
        set => SetValue(OpenFileCommandProperty, value);
    }
}
