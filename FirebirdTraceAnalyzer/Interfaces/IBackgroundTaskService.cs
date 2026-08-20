using System.Collections.ObjectModel;
using System.ComponentModel;
using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Represents a service that manages background tasks and provides a read-only collection of active tasks.
/// Reusable: any background operation can be marked with a single <see cref="Begin"/>.
/// </summary>
public interface IBackgroundTaskService : INotifyPropertyChanged
{
    /// <summary>
    /// Get the list of background tasks. The list is read-only and can be observed for changes.
    /// </summary>
    ReadOnlyObservableCollection<BackgroundTaskItem> Items { get; }

    /// <summary>
    /// Returns true if there are any active background tasks. This can be used to show or hide the background task panel.
    /// </summary>
    bool HasActive { get; }
    
    /// <summary>
    /// Marks the start of a background operation. Repeated calls with the same <paramref name="key"/>
    /// are consolidated into a single entry with a counter (e.g., for queues, a batch of file records).
    /// <see cref="IDisposable.Dispose"/> marks completion; the entry is removed when the counter reaches zero.
    /// Thread-safe: can be called from any thread; UI updates are marshaled to the UI thread.
    /// </summary>
    /// <param name="key">The key for the background task.</param>
    /// <param name="title">The title of the background task.</param>
    /// <param name="detail">The detail information for the background task.</param>
    /// <returns>An IDisposable that can be used to end the task when disposed.</returns>
    IDisposable Begin(string key, string title, string? detail = null);
}
