namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Immutable information about a loaded trace file.
/// </summary>
/// <param name="FileName">File name.</param>
/// <param name="FilePath">Full path to the file.</param>
/// <param name="FileSize">File size in bytes.</param>
/// <param name="StartTrace">Timestamp of the first event in the file.</param>
/// <param name="EndTrace">Timestamp of the last event in the file.</param>
/// <param name="EventCount">Number of events parsed from the file.</param>
/// <param name="FileHash">Content hash used to identify the file.</param>
public sealed record TraceFileInfoModel(
    string FileName,
    string FilePath,
    long FileSize,
    DateTime StartTrace,
    DateTime EndTrace,
    long EventCount,
    string FileHash);
