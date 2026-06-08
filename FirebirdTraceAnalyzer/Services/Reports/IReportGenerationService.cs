using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Services.Reports;

/// <summary>
/// Сервис генерации отчётов
/// </summary>
public interface IReportGenerationService
{
    /// <summary>
    /// Генерирует отчёт на основе шаблона и текущих данных
    /// </summary>
    /// <param name="template">Шаблон отчёта</param>
    /// <param name="metadata">Метаданные для отчёта</param>
    /// <param name="format">Формат экспорта</param>
    /// <param name="outputPath">Путь для сохранения (опционально, если null - создаётся автоматически)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сгенерированный отчёт с путём к файлу</returns>
    Task<GeneratedReport> GenerateReportAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        ReportFormat format,
        string? outputPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Подготавливает события для отчёта (применяет фильтры и сортировку шаблона)
    /// </summary>
    /// <param name="visibleEvents">Текущие видимые события (уже отфильтрованные пользователем)</param>
    /// <param name="template">Шаблон отчёта</param>
    /// <param name="currentSortField">Текущее поле сортировки (если есть)</param>
    /// <param name="currentSortDescending">Текущее направление сортировки</param>
    /// <returns>События, готовые для включения в отчёт</returns>
    IReadOnlyList<EventBase> PrepareEventsForReport(
        IEnumerable<EventBase> visibleEvents,
        ReportTemplate template,
        string? currentSortField,
        bool currentSortDescending);
}