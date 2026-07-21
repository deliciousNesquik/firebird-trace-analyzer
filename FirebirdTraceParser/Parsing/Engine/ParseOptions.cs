using System.Text;

namespace FirebirdTraceParser.Parsing.Engine;

/// <summary>
/// Опции парсинга trace логов.
/// </summary>
public sealed record ParseOptions
{
    /// <summary>Кодировка файла (по умолчанию UTF-8)</summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;
    
    /// <summary>Режим валидации блоков</summary>
    public ValidationMode ValidationMode { get; init; } = ValidationMode.Strict;
    
    /// <summary>Размер батча для потоковой обработки</summary>
    public int BatchSize { get; init; } = 256;
    
    /// <summary>Тайм-аут для regex операций</summary>
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromSeconds(1);
    
    /// <summary>Включить парсинг таблиц производительности</summary>
    public bool ParsePerformanceTables { get; init; } = true;

    /// <summary>
    /// Максимум строк в теле одного блока. Блок закрывается только следующим заголовком, поэтому
    /// файл без границ блоков (битый/вредоносный) иначе буферизуется целиком в память → OOM.
    /// При превышении блок усекается с предупреждением. Лимит намеренно щедрый — легитимные блоки
    /// (большой SQL, широкая perf-таблица) его не достигают.
    /// </summary>
    public int MaxBlockLines { get; init; } = 200_000;

    public static ParseOptions Default => new();
}