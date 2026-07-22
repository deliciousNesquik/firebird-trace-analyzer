using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>Результат разбора одного файла: события и границы диапазона времени + время разбора (мс).</summary>
public sealed record ParsedFile(IReadOnlyList<EventBase> Events, DateTime StartTrace, DateTime EndTrace, long ParseMs);

/// <summary>
/// Приём трейс-файлов: вычисление хэша и потоковый разбор. Выносит эту логику из MainWindowViewModel;
/// применение результата к UI-коллекциям/статистике/хранилищу остаётся у вызывающего.
/// </summary>
public interface IFileIngestionService
{
    /// <summary>SHA-256 файла (для дедупа/кэша переоткрытия), потоково, не загружая файл в память.</summary>
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Потоковый разбор файла в список событий (CPU-работа уводится с вызывающего потока). Возвращает
    /// события в порядке разбора и границы времени (первое/последнее событие).
    /// </summary>
    Task<ParsedFile> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
