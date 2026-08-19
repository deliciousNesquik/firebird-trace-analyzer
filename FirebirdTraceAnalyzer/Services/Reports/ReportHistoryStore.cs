using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Reports;

/// <summary>
/// Файловая история отчётов. Каталог берётся из настроек (с дефолтом), резолвится при каждом
/// обращении — учитывает смену пути без перезапуска.
/// </summary>
public sealed class ReportHistoryStore : IReportHistoryStore
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ISettingsService? _settingsService;

    public ReportHistoryStore(ISettingsService? settingsService = null)
    {
        _settingsService = settingsService;
    }

    public string ResolveDirectory()
    {
        var directory = _settingsService?.GetReportsDirectory()
                        ?? Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "FirebirdTraceAnalyzer", "Reports", "History");

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Logger.Info("Created reports history directory: {Path}", directory);
        }

        return directory;
    }

    public IReadOnlyList<ReportFileEntry> List()
    {
        var directory = ResolveDirectory();

        var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(IsReportFile)
            .OrderByDescending(File.GetCreationTime);

        var result = new List<ReportFileEntry>();
        foreach (var file in files)
        {
            try
            {
                var info = new FileInfo(file);
                result.Add(new ReportFileEntry(
                    info.Name, info.FullName, info.Length, info.CreationTime, GetFormatFromExtension(info.Extension)));
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error reading report file: {File}", file);
            }
        }

        return result;
    }

    public void Delete(string filePath) => File.Delete(filePath);

    private static bool IsReportFile(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() is ".pdf" or ".docx" or ".xlsx" or ".csv";

    private static string GetFormatFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "Pdf",
        ".docx" => "Docx",
        ".xlsx" => "Xlsx",
        ".csv" => "Csv",
        _ => "Unknown"
    };
}
