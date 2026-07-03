using Avalonia.Platform.Storage;

namespace FirebirdTraceAnalyzer.Interfaces.Window;

public interface IFileDialogService
{
    /// <summary>
    /// Открывает диалог выбора файлов и возвращает список выбранных файлов.
    /// Если пользователь отменяет выбор, возвращается пустой список.
    /// </summary>
    /// <returns>IStorageFile - список выбранных файлов</returns>
    Task<IReadOnlyList<IStorageFile>> PickTraceFilesAsync();

    /// <summary>
    /// Открывает расположение указанного файла в проводнике операционной системы 
    /// и фокусирует/выделяет данный файл (если поддерживается ОС).
    /// </summary>
    /// <param name="filePath">Абсолютный путь к целевому файлу на диске.</param>
    /// <returns>
    /// Возвращает <c>true</c>, если системный процесс открытия запущен успешно; 
    /// в противном случае — <c>false</c>.
    /// </returns>
    Task<bool> RevealInFileManagerAsync(string filePath);

    /// <summary>
    /// Диалог сохранения JSON-файла (для экспорта шаблона отчёта).
    /// Возвращает локальный путь выбранного файла или <c>null</c>, если отменено.
    /// </summary>
    Task<string?> PickJsonToSaveAsync(string suggestedName);

    /// <summary>
    /// Диалог выбора JSON-файла для открытия (для импорта шаблона отчёта).
    /// Возвращает локальный путь выбранного файла или <c>null</c>, если отменено.
    /// </summary>
    Task<string?> PickJsonToOpenAsync();
}