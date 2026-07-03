namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Профиль подключения для сохранения настроек
/// </summary>
public sealed record SshConnectionProfile
{
    /// <summary>Имя профиля</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Настройки подключения (без пароля)</summary>
    public SshConnectionSettings Settings { get; init; } = new();
    
    /// <summary>Дата создания профиля</summary>
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    
    /// <summary>Последнее использование</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Создаёт копию профиля с обновлённой датой использования</summary>
    public SshConnectionProfile WithLastUsed() => this with { LastUsedAt = DateTime.Now };
}