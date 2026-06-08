using FirebirdTraceAnalyzer.Enums.Reports;

namespace FirebirdTraceAnalyzer.Models.Reports;

/// <summary>
/// Результат генерации отчёта
/// </summary>
public sealed class GeneratedReport
{
    /// <summary>Использованный шаблон</summary>
    public required ReportTemplate Template { get; init; }
    
    /// <summary>Метаданные отчёта</summary>
    public required ReportMetadata Metadata { get; init; }
    
    /// <summary>Формат экспорта</summary>
    public required ReportFormat Format { get; init; }
    
    /// <summary>Путь к сгенерированному файлу</summary>
    public required string FilePath { get; init; }
    
    /// <summary>Размер файла в байтах</summary>
    public long FileSize { get; init; }
    
    /// <summary>Дата генерации</summary>
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
}