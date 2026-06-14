using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Platform.Storage;
using FirebirdTraceAnalyzer.Interfaces;
using NLog;

namespace FirebirdTraceAnalyzer.Services;

public class FileDialogService : IFileDialogService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IWindowProvider _windowProvider;

    public FileDialogService(IWindowProvider windowProvider)
    {
        _windowProvider = windowProvider
                          ?? throw new ArgumentNullException(nameof(windowProvider));
    }

    public async Task<IReadOnlyList<IStorageFile>> PickTraceFilesAsync()
    {
        var topLevel = _windowProvider.GetCurrent();

        if (topLevel == null)
        {
            Logger.Warn("Active window not found.");
            return [];
        }

        if (!topLevel.StorageProvider.CanOpen)
        {
            Logger.Warn("StorageProvider does not support opening files.");
            return [];
        }

        try
        {
            return await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Select trace log files",
                    AllowMultiple = true,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Trace Logs")
                        {
                            Patterns = ["*.log", "*.txt"]
                        },
                        FilePickerFileTypes.All
                    ]
                });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening file selection dialog.");
            return [];
        }
    }

    public Task<bool> RevealInFileManagerAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Logger.Warn($"File does not exist or path is invalid: {filePath}");
                return Task.FromResult(false);
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows: открывает проводник и выделяет конкретный файл
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // macOS: аргумент -R (reveal) открывает Finder и выделяет файл
                    Process.Start("open", $"-R \"{filePath}\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Linux: xdg-open обычно умеет открывать только директорию
                    var directory = Path.GetDirectoryName(filePath);
                    if (directory != null)
                    {
                        Process.Start("xdg-open", $"\"{directory}\"");
                    }
                }
                else
                {
                    Logger.Warn("Unsupported OS platform for opening file storage.");
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to open file in storage: {filePath}");
                return Task.FromResult(false);
            }
        }
        catch (Exception exception)
        {
            return Task.FromException<bool>(exception);
        }
    }
}