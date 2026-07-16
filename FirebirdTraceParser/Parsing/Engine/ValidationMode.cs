namespace FirebirdTraceParser.Parsing.Engine;

/// <summary>
/// Режим валидации при парсинге.
/// </summary>
public enum ValidationMode
{
    /// <summary>Строгий режим: сбой разбора блока трактуется как ошибка (<c>Error</c>) —
    /// <c>ParsingResult.HasErrors</c> становится <c>true</c>.</summary>
    Strict,

    /// <summary>Мягкий режим: сбой разбора блока трактуется как предупреждение (<c>Warning</c>) —
    /// разбор продолжается, <c>HasErrors</c> не поднимается (проблемы видны в <c>SkippedBlocks</c>).</summary>
    Relaxed
}