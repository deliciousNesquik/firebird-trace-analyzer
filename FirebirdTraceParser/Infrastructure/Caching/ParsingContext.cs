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
    private readonly Dictionary<string, string> _strings = new();
    private readonly Dictionary<long, AttachmentInfo> _attachments = new();
    private readonly Dictionary<int, TraceSessionInfo> _sessions = new();

    /// <summary>Дедуплицирует строку в пределах текущего разбора.</summary>
    public string Intern(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (_strings.TryGetValue(value, out var existing))
            return existing;

        _strings[value] = value;
        return value;
    }

    /// <summary>Возвращает ранее созданный AttachmentInfo по id (в пределах разбора).</summary>
    public bool TryGetAttachment(long attachmentId, out AttachmentInfo? info)
        => _attachments.TryGetValue(attachmentId, out info);

    /// <summary>Добавляет AttachmentInfo в пул и возвращает его.</summary>
    public AttachmentInfo AddAttachment(AttachmentInfo info)
    {
        _attachments[info.AttachmentId] = info;
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
