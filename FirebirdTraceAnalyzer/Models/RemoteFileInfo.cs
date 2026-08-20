using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Information about a remote file.
/// </summary>
public partial class RemoteFileInfo: ViewModelBase
{
    /// <summary>File name.</summary>
    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;

    /// <summary>Full path on the remote server.</summary>
    [ObservableProperty]
    public partial string FullPath { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    [ObservableProperty]
    public partial long Size { get; set; }

    /// <summary>Last modification date.</summary>
    [ObservableProperty]
    public partial DateTime LastModified { get; set; }

    /// <summary>Access permissions.</summary>
    [ObservableProperty]
    public partial Permissions Permissions { get; set; } = new(false, false, false, false, false, false, false, false, false);

    /// <summary>File owner.</summary>
    [ObservableProperty]
    public partial string Owner { get; set; } = string.Empty;

    /// <summary>Whether the file is selected for download.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Size in a human-readable format.</summary>
    public string FormattedSize => ByteSizeFormatter.FormatBytes(Size);

    /// <summary>Date in a human-readable format.</summary>
    public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm:ss");
}