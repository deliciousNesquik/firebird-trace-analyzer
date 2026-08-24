namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Application appearance mode.
/// </summary>
public enum AppTheme
{
    /// <summary>Follow the system setting (light/dark).</summary>
    Auto,

    /// <summary>Always use the light theme.</summary>
    Light,

    /// <summary>Always use the dark theme.</summary>
    Dark,

    /// <summary>Always use the high-contrast theme.</summary>
    Contrast
}

/// <summary>
/// Mode for persisting parsed events on disk.
/// </summary>
public enum StorageMode
{
    /// <summary>Do not persist events to disk.</summary>
    Off,

    /// <summary>Session mode: each new parse clears the store and writes the current session's files
    /// (crash recovery after a close/hang).</summary>
    Session,

    /// <summary>Accumulate mode: each parse appends to the store (long-lived archive).</summary>
    Accumulate
}

/// <summary>
/// Core application settings.
/// </summary>
public class AppSettings
{
    /// <summary>Whether classic (non-regex) search is used.</summary>
    public bool IsClassicSearch { get; set; }

    /// <summary>Appearance mode: Auto (follow system) / Light / Dark.</summary>
    public AppTheme Theme { get; set; } = AppTheme.Auto;

    /// <summary>
    /// UI language code (e.g. "en", "ru"). A string rather than an enum: adding a language means a new
    /// translation file Assets/i18n/{code}.json plus a manifest entry, with no code changes.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Folder where files downloaded from the server are saved (when delete-after-processing is off).
    /// Empty means the default folder (%AppData%/FirebirdTraceAnalyzer/RemoteDownloads).
    /// </summary>
    public string RemoteDownloadPath { get; set; } = string.Empty;

    /// <summary>
    /// Folder where generated reports are saved. Empty means the default folder
    /// (%AppData%/FirebirdTraceAnalyzer/Reports/History).
    /// </summary>
    public string ReportsPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the application log file. Empty means the default path (next to the app, logs/application.log).
    /// </summary>
    public string AppLogPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the parser log file. Empty means the default path (logs/parser.log).
    /// </summary>
    public string ParserLogPath { get; set; } = string.Empty;

    /// <summary>Disk storage mode for events. Defaults to session mode (crash recovery).</summary>
    public StorageMode StorageMode { get; set; } = StorageMode.Session;

    /// <summary>
    /// Folder of the event store file (events.db). Empty means the default folder
    /// (%AppData%/FirebirdTraceAnalyzer/EventStore).
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Unlocks diagnostic tool statistics parser. Off by default — these
    /// items are hidden from ordinary users.
    /// </summary>
    public bool StatisticsMode { get; set; }
    
    /// <summary>
    /// Unlocks diagnostic tool inspector window. Off by default — these
    /// items are hidden from ordinary users.
    /// </summary>
    public bool InspectorMode { get; set; }

    /// <summary>
    /// Deferred store maintenance: set after a partial file deletion so the next launch runs orphan
    /// cleanup + VACUUM in the background (we do not VACUUM on every deletion).
    /// </summary>
    public bool StorageMaintenancePending { get; set; }
}

/// <summary>
/// Main-window geometry (last size/position). Saved when the window closes.
/// Fields are nullable: null means the value has not been saved yet and the XAML sizes are used.
/// </summary>
public sealed class WindowSettings
{
    /// <summary>Last window width, or null when never saved.</summary>
    public double? Width { get; set; }

    /// <summary>Last window height, or null when never saved.</summary>
    public double? Height { get; set; }

    /// <summary>Last window X position, or null when never saved.</summary>
    public int? X { get; set; }

    /// <summary>Last window Y position, or null when never saved.</summary>
    public int? Y { get; set; }

    /// <summary>Whether the window was maximized.</summary>
    public bool Maximized { get; set; }
}

/// <summary>
/// Visibility settings for the UI sections.
/// </summary>
public class UiSectionSettings
{
    /// <summary>Whether the Files section is visible.</summary>
    public bool Files { get; set; }

    /// <summary>Whether the Search section is visible.</summary>
    public bool Search { get; set; }

    /// <summary>Whether the Events section is visible.</summary>
    public bool Events { get; set; }

    /// <summary>Whether the Statistics section is visible.</summary>
    public bool Statistics { get; set; }

    /// <summary>Whether the Logs section is visible.</summary>
    public bool Logs { get; set; }
}

/// <summary>
/// Root model of user settings persisted to disk
/// (in %AppData%/FirebirdTraceAnalyzer/settings.json). Defaults come from the appsettings.json shipped
/// with the application, and user changes are written to this file.
/// </summary>
public sealed class UserSettings
{
    /// <summary>Application-level settings.</summary>
    public AppSettings App { get; set; } = new();

    /// <summary>UI section visibility settings.</summary>
    public UiSectionSettings Ui { get; set; } = new();

    /// <summary>Main-window geometry settings.</summary>
    public WindowSettings Window { get; set; } = new();
}
