using System.Text;
using System.Text.Json.Serialization;
using FirebirdTraceAnalyzer.Enums;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Настройки SSH подключения
/// </summary>
public sealed record SshConnectionSettings
{
    /// <summary>Адрес сервера (IP или hostname)</summary>
    public string Hostname { get; init; } = string.Empty;
    
    /// <summary>SSH порт (по умолчанию 22)</summary>
    public int Port { get; init; } = 22;
    
    /// <summary>Имя пользователя</summary>
    public string Username { get; init; } = string.Empty;
    
    /// <summary>Метод аутентификации</summary>
    public AuthenticationMethod AuthMethod { get; init; } = AuthenticationMethod.Password;
    
    /// <summary>Пароль (только для AuthMethod.Password). Секрет: никогда не сериализуется в профили.</summary>
    [JsonIgnore]
    public string? Password { get; init; }

    /// <summary>Путь к приватному ключу (только для AuthMethod.PrivateKey)</summary>
    public string? PrivateKeyPath { get; init; }

    /// <summary>Парольная фраза для ключа (опционально). Секрет: никогда не сериализуется в профили.</summary>
    [JsonIgnore]
    public string? KeyPassphrase { get; init; }
    
    /// <summary>Удалённая директория с трассировочными файлами</summary>
    public string RemoteDirectory { get; init; } = "/var/log/firebird";
    
    /// <summary>Удалять файлы на сервере после обработки</summary>
    public bool DeleteAfterProcessingFromServer { get; init; }
    
    /// <summary>Удалять файлы на локальной машине после обработки</summary>
    public bool DeleteAfterProcessingOnLocaleMachine { get; init; }
    
    /// <summary>Таймаут подключения (секунды)</summary>
    public int ConnectionTimeout { get; init; } = 30;

    /// <summary>
    /// Печать record без секретов: авто-ToString() у record выводит ВСЕ свойства, поэтому пароль и
    /// парольная фраза утекли бы в логи при интерполяции/логировании записи. Маскируем их.
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

    /// <summary>Валидация настроек</summary>
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