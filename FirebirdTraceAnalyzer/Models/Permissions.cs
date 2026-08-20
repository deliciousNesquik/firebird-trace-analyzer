namespace FirebirdTraceAnalyzer.Models;

/// <summary>Unix-style file permission bits for owner, group and others (read/write/execute).</summary>
/// <param name="ownerCanRead">Whether the owner can read.</param>
/// <param name="ownerCanWrite">Whether the owner can write.</param>
/// <param name="ownerCanExecute">Whether the owner can execute.</param>
/// <param name="groupCanRead">Whether the group can read.</param>
/// <param name="groupCanWrite">Whether the group can write.</param>
/// <param name="groupCanExecute">Whether the group can execute.</param>
/// <param name="othersCanRead">Whether others can read.</param>
/// <param name="othersCanWrite">Whether others can write.</param>
/// <param name="othersCanExecute">Whether others can execute.</param>
public sealed class Permissions(bool ownerCanRead, bool ownerCanWrite, bool ownerCanExecute, bool groupCanRead, bool groupCanWrite, bool groupCanExecute, bool othersCanRead, bool othersCanWrite, bool othersCanExecute)
{
    /// <summary>Whether the owner can execute.</summary>
    public bool OwnerCanExecute { get; set; } = ownerCanExecute;

    /// <summary>Whether the owner can read.</summary>
    public bool OwnerCanRead { get; set; }  = ownerCanRead;

    /// <summary>Whether the owner can write.</summary>
    public bool OwnerCanWrite { get; set; } = ownerCanWrite;

    /// <summary>Whether the group can execute.</summary>
    public bool GroupCanExecute { get; set; } = groupCanExecute;

    /// <summary>Whether the group can read.</summary>
    public bool GroupCanRead { get; set; } = groupCanRead;

    /// <summary>Whether the group can write.</summary>
    public bool GroupCanWrite { get; set; } = groupCanWrite;

    /// <summary>Whether others can execute.</summary>
    public bool OthersCanExecute { get; set; } = othersCanExecute;

    /// <summary>Whether others can read.</summary>
    public bool OthersCanRead { get; set; } = othersCanRead;

    /// <summary>Whether others can write.</summary>
    public bool OthersCanWrite { get; set; } = othersCanWrite;
}