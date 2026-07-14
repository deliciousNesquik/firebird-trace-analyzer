namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Неизменяемая модель информации о загруженном trace-файле.
/// </summary>
public sealed record TraceFileInfoModel(
    string FileName,
    string FilePath,
    long FileSize,
    DateTime StartTrace,
    DateTime EndTrace,
    long EventCount,
    string FileHash);
