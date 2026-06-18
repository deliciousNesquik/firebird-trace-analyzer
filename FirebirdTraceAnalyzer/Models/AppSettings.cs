namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Режим оформления приложения.
/// </summary>
public enum AppTheme
{
    /// <summary>Следовать системным настройкам (light/dark).</summary>
    Auto,

    /// <summary>Всегда светлая тема.</summary>
    Light,

    /// <summary>Всегда тёмная тема.</summary>
    Dark
}

/// <summary>
/// Основные настройки приложения
/// </summary>
public class AppSettings
{
    public bool IsClassicSearch { get; set; }

    /// <summary>Режим оформления: Auto (по системе) / Light / Dark.</summary>
    public AppTheme Theme { get; set; } = AppTheme.Auto;

    /// <summary>
    /// Папка, в которую сохраняются скачанные с сервера файлы (когда удаление после обработки
    /// выключено). Пусто — используется папка по умолчанию (%AppData%/FirebirdTraceAnalyzer/RemoteDownloads).
    /// </summary>
    public string RemoteDownloadPath { get; set; } = string.Empty;

    /// <summary>
    /// Папка, в которую сохраняются сгенерированные отчёты. Пусто — папка по умолчанию
    /// (%AppData%/FirebirdTraceAnalyzer/Reports/History).
    /// </summary>
    public string ReportsPath { get; set; } = string.Empty;
}

/// <summary>
/// Геометрия главного окна (последние размеры/позиция). Сохраняется при закрытии окна.
/// Поля nullable: null — значение ещё не сохранялось, используются размеры из XAML.
/// </summary>
public sealed class WindowSettings
{
    public double? Width { get; set; }
    public double? Height { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public bool Maximized { get; set; }
}

/// <summary>
/// Настройки видимости секций UI
/// </summary>
public class UiSectionSettings
{
    public bool Files { get; set; }
    public bool Search { get; set; }
    public bool Events { get; set; }
    public bool Statistics { get; set; }
    public bool Logs { get; set; }
}

/// <summary>
/// Корневая модель пользовательских настроек, которая сохраняется на диск
/// (в %AppData%/FirebirdTraceAnalyzer/settings.json). Значения по умолчанию берутся из
/// поставляемого с приложением appsettings.json, а изменения пользователя пишутся в этот файл.
/// </summary>
public sealed class UserSettings
{
    public AppSettings App { get; set; } = new();
    public UiSectionSettings Ui { get; set; } = new();
    public WindowSettings Window { get; set; } = new();
}