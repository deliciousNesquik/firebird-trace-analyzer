using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceParser.Infrastructure.Caching;

/// <summary>
/// Контекст одного разбора: пулы интернирования строк, подключений и сессий.
/// Состояние живёт в пределах одного ParseFile*/ParseStreamAsync, а не в статике —
/// поэтому парсер реентерабелен и параллелизуем, а ручной Reset() не нужен.
/// Доступ к контексту однопоточный в пределах одного разбора, поэтому обычный Dictionary.
/// </summary>
public sealed class ParsingContext
{
    /// <summary>
    /// Порог интернинга. Длинные строки (тексты SQL, крупные значения параметров) почти уникальны —
    /// их дедупликация не окупается, зато раздувает словарь и тратит CPU на хеширование. Такие строки
    /// возвращаем как есть. Короткие повторяющиеся токены (имена, роли, charset, dtype) интернируются.
    /// </summary>
    private const int MaxInternLength = 128;

    private readonly Dictionary<string, string> _strings = new();
    // Ключ (база, id), а не голый id: у Classic счётчики ATT per-process, и один номер
    // встречается у разных баз в одном разборе (issue #31). Контракт совпадает с
    // DurationPostProcessor, который ключует свою карту так же.
    private readonly Dictionary<(string DatabasePath, long AttachmentId), AttachmentInfo> _attachments = new();
    private readonly Dictionary<int, TraceSessionInfo> _sessions = new();

    /// <summary>Дедуплицирует строку в пределах текущего разбора.</summary>
    public string Intern(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Length > MaxInternLength)
            return value;

        if (_strings.TryGetValue(value, out var existing))
            return existing;

        _strings[value] = value;
        return value;
    }

    /// <summary>Возвращает ранее созданный AttachmentInfo по (база, id) в пределах разбора.</summary>
    public bool TryGetAttachment(string databasePath, long attachmentId, out AttachmentInfo? info)
        => _attachments.TryGetValue((databasePath, attachmentId), out info);

    /// <summary>Добавляет AttachmentInfo в пул и возвращает его.</summary>
    public AttachmentInfo AddAttachment(AttachmentInfo info)
    {
        _attachments[(info.DatabasePath, info.AttachmentId)] = info;
        return info;
    }

    /// <summary>Дедуплицирует TraceSessionInfo по id (в пределах разбора).</summary>
    public TraceSessionInfo InternSession(int sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
            return existing;

        var created = new TraceSessionInfo { SessionId = sessionId };
        _sessions[sessionId] = created;
        return created;
    }
}
