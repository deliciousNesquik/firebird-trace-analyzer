using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Сервис пользовательских настроек приложения. Источник истины для настроек в рантайме:
/// при старте берёт значения по умолчанию из appsettings.json и накладывает сверху
/// сохранённый пользовательский файл, а изменения сохраняет в %AppData%.
/// </summary>
public interface ISettingsService
{
    /// <summary>Основные настройки приложения (живой экземпляр, который сохраняется в Save).</summary>
    AppSettings App { get; }

    /// <summary>Настройки видимости секций UI (живой экземпляр, который сохраняется в Save).</summary>
    UiSectionSettings Ui { get; }

    /// <summary>Папка для скачивания удалённых файлов с учётом значения по умолчанию.</summary>
    string GetRemoteDownloadDirectory();

    /// <summary>Сохраняет текущие настройки в пользовательский файл.</summary>
    void Save();
}
