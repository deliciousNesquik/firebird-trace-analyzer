namespace FirebirdTraceAnalyzer.Interfaces.Reports;

/// <summary>Метаданные одного файла отчёта в истории.</summary>
public sealed record ReportFileEntry(string FileName, string FilePath, long FileSize, DateTime CreatedAt, string Format);

/// <summary>
/// Доступ к истории сгенерированных отчётов на диске. Инкапсулирует файловый ввод-вывод, чтобы
/// ViewModel не работала с File/Directory напрямую (SoC/тестируемость).
/// </summary>
public interface IReportHistoryStore
{
    /// <summary>Каталог истории отчётов (создаётся при отсутствии).</summary>
    string ResolveDirectory();

    /// <summary>Список файлов отчётов (pdf/docx/xlsx/csv), новые сверху.</summary>
    IReadOnlyList<ReportFileEntry> List();

    /// <summary>Удаляет файл отчёта.</summary>
    void Delete(string filePath);
}
