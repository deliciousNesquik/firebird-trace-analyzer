using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Platform.Storage;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Window;
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

    public async Task<string?> PickJsonToSaveAsync(string suggestedName)
    {
        var topLevel = _windowProvider.GetCurrent();

        if (topLevel == null || !topLevel.StorageProvider.CanSave)
        {
            Logger.Warn("StorageProvider does not support saving files.");
            return null;
        }

        try
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export report template",
                SuggestedFileName = suggestedName,
                DefaultExtension = "json",
                FileTypeChoices =
                [
                    new FilePickerFileType("Report template") { Patterns = ["*.json"] }
                ]
            });

            return file?.TryGetLocalPath();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening save file dialog.");
            return null;
        }
    }

    public async Task<string?> PickJsonToOpenAsync()
    {
        var topLevel = _windowProvider.GetCurrent();

        if (topLevel == null || !topLevel.StorageProvider.CanOpen)
        {
            Logger.Warn("StorageProvider does not support opening files.");
            return null;
        }

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import report template",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Report template") { Patterns = ["*.json"] },
                    FilePickerFileTypes.All
                ]
            });

            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening file selection dialog.");
            return null;
        }
    }

    public Task<bool> RevealInFileManagerAsync(string filePath)
    {
        try
        {
            var isDirectory = Directory.Exists(filePath);

            if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !isDirectory))
            {
                Logger.Warn($"Path does not exist or is invalid: {filePath}");
                return Task.FromResult(false);
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows: для файла — выделить в проводнике; для папки — просто открыть её.
                    if (isDirectory)
                        Process.Start("explorer.exe", $"\"{filePath}\"");
                    else
                        Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // macOS: -R (reveal) выделяет файл; для папки открываем её саму.
                    if (isDirectory)
                        Process.Start("open", $"\"{filePath}\"");
                    else
                        Process.Start("open", $"-R \"{filePath}\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Linux: xdg-open открывает директорию (для файла — его папку).
                    var directory = isDirectory ? filePath : Path.GetDirectoryName(filePath);
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