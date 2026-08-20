using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// A row in the session-restore list: a file found in the event store and available to load back into
/// the working set. Wraps the <see cref="TraceFileInfoModel"/> manifest, adding a selection flag and
/// human-readable formatting for the dialog.
/// </summary>
public partial class RestorableFileInfo : ViewModelBase
{
    /// <summary>Initializes the row from a store manifest entry.</summary>
    /// <param name="file">The source store manifest entry.</param>
    /// <param name="selected">Initial selection state (true for restore, explicit for deletion).</param>
    public RestorableFileInfo(TraceFileInfoModel file, bool selected = true)
    {
        File = file;
        IsSelected = selected;
    }

    /// <summary>The source store manifest entry.</summary>
    public TraceFileInfoModel File { get; }

    /// <summary>Whether the file is selected (true by default for restore; set explicitly for deletion).</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>File name.</summary>
    public string FileName => File.FileName;

    /// <summary>Number of events stored for the file.</summary>
    public long EventCount => File.EventCount;

    /// <summary>File size in a human-readable format.</summary>
    public string FormattedSize => ByteSizeFormatter.FormatBytes(File.FileSize);

    /// <summary>Trace time range as a display string (start — end).</summary>
    public string TimeRange => $"{File.StartTrace:yyyy-MM-dd HH:mm:ss} — {File.EndTrace:HH:mm:ss}";
}
