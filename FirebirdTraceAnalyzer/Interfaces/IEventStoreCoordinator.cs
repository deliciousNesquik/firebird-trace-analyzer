using FirebirdTraceAnalyzer.Models;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Coordinator interface for managing the event store, which handles the persistence and retrieval
/// of events associated with trace files.
/// </summary>
public interface IEventStoreCoordinator
{
    /// <summary>
    /// Returns true if the event store is enabled (Session or Accumulate mode). In Off mode, it returns false.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns true if the event store contains events for the specified file hash. In Off mode, it returns false.
    /// </summary>
    /// <param name="fileHash">The hash of the file to check.</param>
    /// <returns>True if the event store contains events for the specified file hash; otherwise, false.</returns>
    Task<bool> ContainsAsync(string fileHash);
    
    /// <summary>
    /// Queues the file event record for background processing and returns immediately (the disk is not on the critical path).
    /// It operates on a snapshot of the list, eliminating race conditions associated with clearing the working set on the UI thread.
    /// </summary>
    /// <param name="file">The file info model.</param>
    /// <param name="events">The list of events to persist.</param>
    void Persist(TraceFileInfoModel file, IReadOnlyList<EventBase> events);
    
    /// <summary>
    /// Reads the events for the specified file hash from the event store. If the file hash is not found, it returns an empty list.
    /// </summary>
    /// <param name="fileHash">The hash of the file for which to read events.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of events for the specified file hash; otherwise, an empty list.</returns>
    Task<IReadOnlyList<EventBase>> ReadFileAsync(string fileHash, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Manifest of files in the storage (for session recovery). Empty list in case of failure or if the storage is disabled.
    /// </summary>
    /// <returns>The list of files in the storage; otherwise, an empty list.</returns>
    Task<IReadOnlyList<TraceFileInfoModel>> ListFilesAsync();

    /// <summary>
    /// The Session mode acts as a "session mirror": it removes files from storage when they are closed or deleted.
    /// In Accumulate mode, it is a no-op; it places the deletion task into the same FIFO queue and flags the item
    /// for deferred processing.
    /// </summary>
    /// <param name="fileHashes">The hashes of the files to remove.</param>
    void RemoveIfSession(IReadOnlyCollection<string> fileHashes);

    /// <summary>Session mode: completely clears the storage (closing all files = empty session).</summary>
    void ClearIfSession();
    
    /// <summary>
    /// Performs maintenance tasks on the event store, such as removing old files or optimizing the database.
    /// In Accumulate mode, it is a no-op; it places the maintenance task into the same FIFO queue and flags it for deferred processing.
    /// </summary>
    /// <returns>Result of the maintenance operation.</returns>
    Task RunPendingMaintenanceAsync();
}
