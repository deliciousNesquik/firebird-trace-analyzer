using FirebirdTraceAnalyzer.Models.Reports;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Интерфейс экспортера отчётов
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Экспортирует отчёт в указанный формат
    /// </summary>
    /// <param name="template">Шаблон отчёта</param>
    /// <param name="metadata">Метаданные отчёта</param>
    /// <param name="outputPath">Путь для сохранения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task ExportAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        string outputPath,
        CancellationToken cancellationToken = default);
}