using System.Text;
using System.Text.Json.Serialization;
using FirebirdTraceAnalyzer.Enums;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// SSH connection settings.
/// </summary>
public sealed record SshConnectionSettings
{
    /// <summary>Server address (IP or hostname).</summary>
    public string Hostname { get; init; } = string.Empty;

    /// <summary>SSH port (defaults to 22).</summary>
    public int Port { get; init; } = 22;

    /// <summary>User name.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Authentication method.</summary>
    public AuthenticationMethod AuthMethod { get; init; } = AuthenticationMethod.Password;

    /// <summary>Password (only for AuthMethod.Password). Secret: never serialized into profiles.</summary>
    [JsonIgnore]
    public string? Password { get; init; }

    /// <summary>Path to the private key (only for AuthMethod.PrivateKey).</summary>
    public string? PrivateKeyPath { get; init; }

    /// <summary>Passphrase for the key (optional). Secret: never serialized into profiles.</summary>
    [JsonIgnore]
    public string? KeyPassphrase { get; init; }

    /// <summary>Remote directory containing the trace files.</summary>
    public string RemoteDirectory { get; init; } = "/var/log/firebird";

    /// <summary>Whether to delete files on the server after processing.</summary>
    public bool DeleteAfterProcessingFromServer { get; init; }

    /// <summary>Whether to delete files on the local machine after processing.</summary>
    public bool DeleteAfterProcessingOnLocaleMachine { get; init; }

    /// <summary>Connection timeout in seconds.</summary>
    public int ConnectionTimeout { get; init; } = 30;

    /// <summary>
    /// Prints the record without secrets: a record's auto-generated ToString() would emit ALL
    /// properties, so the password and passphrase would leak into logs when the record is interpolated
    /// or logged. This masks them.
    /// </summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Hostname = ").Append(Hostname);
        builder.Append(", Port = ").Append(Port);
        builder.Append(", Username = ").Append(Username);
        builder.Append(", AuthMethod = ").Append(AuthMethod);
        builder.Append(", Password = ").Append(Password is null ? "null" : "***");
        builder.Append(", PrivateKeyPath = ").Append(PrivateKeyPath);
        builder.Append(", KeyPassphrase = ").Append(KeyPassphrase is null ? "null" : "***");
        builder.Append(", RemoteDirectory = ").Append(RemoteDirectory);
        builder.Append(", DeleteAfterProcessingFromServer = ").Append(DeleteAfterProcessingFromServer);
        builder.Append(", DeleteAfterProcessingOnLocaleMachine = ").Append(DeleteAfterProcessingOnLocaleMachine);
        builder.Append(", ConnectionTimeout = ").Append(ConnectionTimeout);
        return true;
    }

    /// <summary>Validates the settings.</summary>
    /// <param name="errorMessage">The first validation error, or <c>null</c> when the settings are valid.</param>
    /// <returns><c>true</c> when the settings are valid; otherwise <c>false</c>.</returns>
    public bool IsValid(out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(Hostname))
        {
            errorMessage = "Hostname is required";
            return false;
        }

        if (Port is < 1 or > 65535)
        {
            errorMessage = "Port must be between 1 and 65535";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            errorMessage = "Username is required";
            return false;
        }

        if (AuthMethod == AuthenticationMethod.Password && string.IsNullOrWhiteSpace(Password))
        {
            errorMessage = "Password is required";
            return false;
        }

        if (AuthMethod == AuthenticationMethod.PrivateKey && string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            errorMessage = "Private key path is required";
            return false;
        }
        
        // проверка на существование ключа
        if (AuthMethod == AuthenticationMethod.PrivateKey && !File.Exists(PrivateKeyPath))
        {
            errorMessage = "Private key path is not exists";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RemoteDirectory))
        {
            errorMessage = "Remote directory is required";
            return false;
        }

        errorMessage = null;
        return true;
    }
}