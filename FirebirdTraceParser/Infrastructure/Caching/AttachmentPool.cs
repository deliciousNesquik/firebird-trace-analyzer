using System.Collections.Concurrent;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceParser.Infrastructure.Caching;

public static class AttachmentPool
{
    private static readonly ConcurrentDictionary<long, AttachmentInfo> Pool = new();

    // Пытаемся получить готовый объект по ID
    public static bool TryGet(long attachmentId, out AttachmentInfo? info)
    {
        return Pool.TryGetValue(attachmentId, out info);
    }

    // Добавляем новый объект в пул и возвращаем его
    public static AttachmentInfo Add(AttachmentInfo info)
    {
        Pool[info.AttachmentId] = info;
        return info;
    }

    public static void Reset() => Pool.Clear();
}