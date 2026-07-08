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

    /// <summary>Геометрия главного окна (живой экземпляр, который сохраняется в Save).</summary>
    WindowSettings Window { get; }

    /// <summary>Папка для скачивания удалённых файлов с учётом значения по умолчанию.</summary>
    string GetRemoteDownloadDirectory();

    /// <summary>Папка для сохранения отчётов с учётом значения по умолчанию.</summary>
    string GetReportsDirectory();

    /// <summary>Папка файла хранилища событий (events.db) с учётом значения по умолчанию.</summary>
    string GetEventStoreDirectory();

    /// <summary>Сохраняет текущие настройки в пользовательский файл.</summary>
    void Save();

    /// <summary>Возвращает копию заводских настроек (из appsettings.json) — для кнопки «Сброс».</summary>
    UserSettings GetDefaults();

    /// <summary>Сериализует переданные настройки в указанный файл.</summary>
    Task ExportAsync(string path, UserSettings settings);

    /// <summary>Читает и валидирует настройки из файла (без применения к приложению).</summary>
    Task<UserSettings> ReadFromFileAsync(string path);
}
