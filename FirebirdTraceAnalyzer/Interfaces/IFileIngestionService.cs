using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Result of parsing a trace file: list of events, start/end timestamps, and parsing duration in milliseconds.
/// </summary>
/// <param name="Events">The list of parsed events.</param>
/// <param name="StartTrace">The start timestamp of the trace.</param>
/// <param name="EndTrace">The end timestamp of the trace.</param>
/// <param name="ParseMs">The parsing duration in milliseconds.</param>
public sealed record ParsedFile(IReadOnlyList<EventBase> Events, DateTime StartTrace, DateTime EndTrace, long ParseMs);

/// <summary>
/// Defines methods for file ingestion services, including computing file hashes and parsing trace files asynchronously.
/// </summary>
public interface IFileIngestionService
{
    /// <summary>
    /// Computes the hash of a file asynchronously. This operation is CPU-bound and will be offloaded from the calling thread.
    /// </summary>
    /// <param name="filePath">The path to the file for which to compute the hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The hash of the file.</returns>
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a trace file asynchronously. This operation is CPU-bound and will be offloaded from the calling thread.
    /// </summary>
    /// <param name="filePath">The path to the trace file to parse.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the parsing operation.</returns>
    Task<ParsedFile> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
