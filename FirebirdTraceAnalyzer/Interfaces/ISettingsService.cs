using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Represents a service for managing application settings, including application-wide settings,
/// user interface settings, and window geometry. Provides methods for saving, exporting, and reading settings from files.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Settings application-wide, which are not saved to the user file (appsettings.json).
    /// </summary>
    AppSettings App { get; }

    /// <summary>
    /// User interface settings, which are saved to the user file (appsettings.json).
    /// </summary>
    UiSectionSettings Ui { get; }

    /// <summary>
    /// Geometry of the main window (live instance, which is saved in Save).
    /// </summary>
    WindowSettings Window { get; }

    /// <summary>
    /// Returns the path to the folder for saving downloaded files, taking into account the default value.
    /// </summary>
    /// <returns>The path to the remote download directory.</returns>
    string GetRemoteDownloadDirectory();

    /// <summary>
    /// Returns the path to the folder for saving reports, taking into account the default value.
    /// </summary>
    /// <returns>The path to the report's directory.</returns>
    string GetReportsDirectory();

    /// <summary>
    /// Returns the path to the folder for saving event store files, taking into account the default value.
    /// </summary>
    /// <returns>The path to the event store directory.</returns>
    string GetEventStoreDirectory();

    /// <summary>
    /// Saves the current settings to the user file (appsettings.json).
    /// </summary>
    void Save();

    /// <summary>
    /// Returns a copy of the default settings (from appsettings.json) — for the «Reset» button.
    /// </summary>
    /// <returns>The default settings.</returns>
    UserSettings GetDefaults();

    /// <summary>
    /// Serializes the provided settings to the specified file.
    /// </summary>
    /// <param name="path">The path to the file where settings will be saved.</param>
    /// <param name="settings">The settings to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportAsync(string path, UserSettings settings);

    /// <summary>
    /// Reads and validates settings from a file (without applying them to the application).
    /// </summary>
    /// <param name="path">The path to the file from which to read settings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<UserSettings> ReadFromFileAsync(string path);
}
