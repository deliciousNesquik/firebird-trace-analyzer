using CommunityToolkit.Mvvm.ComponentModel;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// A single kind of background task in the indicator (e.g. "Writing to store"). <see cref="Count"/>
/// is how many operations of this kind are currently in flight (for queues: grows on enqueue, drops
/// on completion; the item disappears at zero).
/// </summary>
public sealed partial class BackgroundTaskItem : ObservableObject
{
    /// <summary>Stable key identifying the task kind.</summary>
    public required string Key { get; init; }

    /// <summary>Display title of the task kind.</summary>
    public required string Title { get; init; }

    /// <summary>Optional detail line shown under the title.</summary>
    [ObservableProperty] private string? _detail;

    /// <summary>Number of in-flight operations of this kind.</summary>
    [ObservableProperty] private int _count;
}
