namespace FirebirdTraceAnalyzer.Interfaces.Reports;

/// <summary>
/// Represents a report file entry in the report history.
/// </summary>
/// <param name="FileName">The name of the report file.</param>
/// <param name="FilePath">The path to the report file.</param>
/// <param name="FileSize">The size of the report file.</param>
/// <param name="CreatedAt">The date and time when the report file was created.</param>
/// <param name="Format">The format of the report file.</param>
public sealed record ReportFileEntry(string FileName, string FilePath, long FileSize, DateTime CreatedAt, string Format);

/// <summary>
/// Interface for managing the storage of report history files.
/// Implementations of this interface are responsible
/// for providing access to the directory where report history files are stored,
/// listing the available report files, and deleting specific report files from the history.
/// </summary>
public interface IReportHistoryStore
{
    /// <summary>
    /// Returns the directory where the report history is stored. The directory is created if it does not exist.
    /// </summary>
    /// <returns>Path to the report history directory.</returns>
    string ResolveDirectory();

    /// <summary>
    /// Returns a list of report files in the history directory, sorted by creation date (newest first).
    /// </summary>
    /// <returns>A list of report file entries.</returns>
    IReadOnlyList<ReportFileEntry> List();

    /// <summary>
    /// Deletes a report file from the history directory.
    /// </summary>
    /// <param name="filePath">The path to the report file to delete.</param>
    void Delete(string filePath);
}
