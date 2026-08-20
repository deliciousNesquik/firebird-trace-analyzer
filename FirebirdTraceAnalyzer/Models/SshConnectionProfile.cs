namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// A saved SSH connection profile.
/// </summary>
public sealed record SshConnectionProfile
{
    /// <summary>Display name of the profile.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Connection settings (without the password).</summary>
    public SshConnectionSettings Settings { get; init; } = new();

    /// <summary>When the profile was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.Now;

    /// <summary>When the profile was last used, if ever.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Returns a copy of the profile with its last-used timestamp set to now.</summary>
    /// <returns>A copy with an updated <see cref="LastUsedAt"/>.</returns>
    public SshConnectionProfile WithLastUsed() => this with { LastUsedAt = DateTime.Now };
}