namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
/// Represents a store for SSH host keys (analogue <c>known_hosts</c>),
/// allowing verification of host keys against stored fingerprints.
/// Implements <c>TOFU</c> (trust on first use): the first host key encountered is stored,
/// and the connection must be rejected if the fingerprint subsequently mismatches (protection against <b>MITM</b>).
/// </summary>
public interface IHostKeyStore
{
    /// <summary>
    /// Verifies the host key against the stored fingerprint.
    /// </summary>
    /// <param name="host">The host for which to verify the key.</param>
    /// <param name="port">The port for which to verify the key.</param>
    /// <param name="keyName">The name of the key type (e.g., <c>ssh-ed25519</c>).</param>
    /// <param name="fingerprintSha256">The SHA-256 fingerprint of the key (base64).</param>
    /// <returns>
    /// <c>true</c> if the key is trusted (either new or matches stored);
    /// <c>false</c> if the key is not trusted (different from stored).
    /// </returns>
    bool Verify(string host, int port, string keyName, string fingerprintSha256);
}
