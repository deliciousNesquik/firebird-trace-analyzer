using System.Windows.Input;
using Avalonia;
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

    /// <summary>Пропущенные блоки парсинга (Severity ≥ Warning); null = неизвестно (кэш из хранилища).</summary>
    public static readonly StyledProperty<int?> SkippedBlocksProperty =
        AvaloniaProperty.Register<FileCard, int?>(nameof(SkippedBlocks));

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

    /// <summary>Пропущенные блоки парсинга; null/0 — бейдж не показывается.</summary>
    public int? SkippedBlocks
    {
        get => GetValue(SkippedBlocksProperty);
        set => SetValue(SkippedBlocksProperty, value);
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
