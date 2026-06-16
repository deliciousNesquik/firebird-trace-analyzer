namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
/// Сервис для безопасного хранения учётных данных
/// </summary>
public interface ICredentialStorageService
{
    /// <summary>Сохранить пароль</summary>
    Task SavePasswordAsync(string server, string username, string password);
    
    /// <summary>Получить пароль</summary>
    Task<string?> GetPasswordAsync(string server, string username);
    
    /// <summary>Удалить пароль</summary>
    Task DeletePasswordAsync(string server, string username);
    
    /// <summary>Проверить наличие сохранённого пароля</summary>
    Task<bool> HasPasswordAsync(string server, string username);
}