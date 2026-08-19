namespace FirebirdTraceAnalyzer.Enums;

/// <summary>
/// Defines the authentication methods for connecting to a remote machine.
/// </summary>
public enum AuthenticationMethod
{
    /// <summary>
    /// Authentication by password
    /// </summary>
    Password,
    
    /// <summary>
    /// Authentication by private key
    /// </summary>
    PrivateKey
}