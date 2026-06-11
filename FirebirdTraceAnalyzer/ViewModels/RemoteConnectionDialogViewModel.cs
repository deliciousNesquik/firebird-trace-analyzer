using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FirebirdTraceAnalyzer.Enums;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;
using NLog;

namespace FirebirdTraceAnalyzer.ViewModels;

/// <summary>
/// ViewModel диалога подключения к удалённому серверу
/// </summary>
public partial class RemoteConnectionDialogViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly IWindowProvider _windowProvider;
    private readonly ISshConnectionService _sshService;
    private readonly ICredentialStorageService? _credentialStorage;

    #region Observable Properties - Connection Settings

    [ObservableProperty]
    private string _hostname = string.Empty;

    [ObservableProperty]
    private int _port = 22;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private AuthenticationMethod _authenticationMethod = AuthenticationMethod.Password;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _showPassword;

    [ObservableProperty]
    private string _privateKeyPath = string.Empty;

    [ObservableProperty]
    private string _keyPassphrase = string.Empty;

    [ObservableProperty]
    private bool _showKeyPassphrase;

    [ObservableProperty]
    private string _remoteDirectory = "/var/log/firebird";

    [ObservableProperty]
    private bool _deleteAfterProcessing;

    #endregion

    #region Observable Properties - UI State

    [ObservableProperty]
    private bool _isPasswordAuthSelected = true;

    [ObservableProperty]
    private bool _isPrivateKeyAuthSelected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _statusMessage = "Ready to connect";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _rememberPassword;

    #endregion

    #region Observable Properties - Profiles

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private SshConnectionProfile? _selectedProfile;

    public ObservableCollection<SshConnectionProfile> SavedProfiles { get; } = new();

    #endregion

    /// <summary>Событие успешного подключения (возвращает настройки)</summary>
    public event EventHandler<SshConnectionSettings>? ConnectionEstablished;

    public RemoteConnectionDialogViewModel(
        IWindowProvider windowProvider,
        ISshConnectionService sshService,
        ICredentialStorageService? credentialStorage = null)
    {
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));
        _sshService = sshService ?? throw new ArgumentNullException(nameof(sshService));
        _credentialStorage = credentialStorage;

        LoadSavedProfiles();
    }
    
    /// <summary>
    /// Конструктор только для XAML-дизайнера. В рантайме ViewModel создаётся через DI
    /// (см. <c>App.Services.GetRequiredService</c>), поэтому здесь не делаем файловый ввод-вывод
    /// и не используем зависимости.
    /// </summary>
    public RemoteConnectionDialogViewModel()
    {
        _windowProvider = null!;
        _sshService = null!;
        _credentialStorage = null;
    }

    #region Commands

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsConnecting = true;
        StatusMessage = "Validating settings...";

        try
        {
            var settings = CreateConnectionSettings();

            if (!settings.IsValid(out var validationError))
            {
                ErrorMessage = validationError;
                StatusMessage = "Validation failed";
                return;
            }

            StatusMessage = $"Connecting to {Hostname}:{Port}...";
            Logger.Info("Attempting connection to {Hostname}:{Port}", Hostname, Port);

            // Подключаемся
            await _sshService.ConnectAsync(settings, cancellationToken);

            // Проверяем существование директории
            StatusMessage = "Checking remote directory...";
            
            if (!await _sshService.DirectoryExistsAsync(RemoteDirectory, cancellationToken))
            {
                ErrorMessage = $"Directory not found: {RemoteDirectory}";
                StatusMessage = "Directory not found";
                _sshService.Disconnect();
                return;
            }

            if (!await _sshService.CanReadAsync(RemoteDirectory, cancellationToken))
            {
                ErrorMessage = $"No read permissions for: {RemoteDirectory}";
                StatusMessage = "Access denied";
                _sshService.Disconnect();
                return;
            }

            // Сохраняем пароль если нужно
            if (RememberPassword && AuthenticationMethod == AuthenticationMethod.Password 
                && _credentialStorage != null)
            {
                await _credentialStorage.SavePasswordAsync(Hostname, Username, Password);
                Logger.Info("Password saved for {Username}@{Hostname}", Username, Hostname);
            }

            StatusMessage = "Connected successfully!";
            Logger.Info("Connection established successfully");

            // Уведомляем об успешном подключении
            ConnectionEstablished?.Invoke(this, settings);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Connection cancelled";
            Logger.Info("Connection cancelled by user");
            _sshService.Disconnect();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Connection failed";
            Logger.Error(ex, "Connection failed");
            _sshService.Disconnect();
        }
        finally
        {
            IsConnecting = false;
            ConnectCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanConnect()
    {
        return !IsConnecting 
               && !string.IsNullOrWhiteSpace(Hostname)
               && !string.IsNullOrWhiteSpace(Username)
               && Port > 0 && Port <= 65535;
    }
    
    [RelayCommand]
    private void Cancel()
    {
        Logger.Info("Connection dialog cancelled");
        ConnectionEstablished?.Invoke(this, null!);
    }
    
    [RelayCommand]
    private void ChangeAuthMethod(AuthenticationMethod method)
    {
        AuthenticationMethod = method;
        Logger.Debug("Authentication method changed to: {Method}", method);
    }

    [RelayCommand]
    private async Task BrowsePrivateKeyAsync()
    {
        var topLevel = _windowProvider.GetCurrent();
        if (topLevel?.StorageProvider == null) return;

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Select Private Key",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("SSH Keys")
                        {
                            Patterns = new[] { "*", "id_rsa", "id_ed25519", "*.pem" }
                        },
                        FilePickerFileTypes.All
                    }
                });

            if (files.Count > 0)
            {
                PrivateKeyPath = files[0].Path.LocalPath;
                Logger.Debug("Selected private key: {Path}", PrivateKeyPath);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error selecting private key");
            ErrorMessage = $"Error selecting file: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            ErrorMessage = "Profile name is required";
            return;
        }

        try
        {
            var settings = CreateConnectionSettings() with { Password = null }; // Не сохраняем пароль
            
            var profile = new SshConnectionProfile
            {
                Name = ProfileName,
                Settings = settings,
                CreatedAt = DateTime.Now
            };

            // Удаляем старый профиль с таким именем
            var existing = SavedProfiles.FirstOrDefault(p => p.Name == ProfileName);
            if (existing != null)
                SavedProfiles.Remove(existing);

            SavedProfiles.Add(profile);
            
            await SaveProfilesToFileAsync();

            StatusMessage = $"Profile '{ProfileName}' saved";
            Logger.Info("Profile saved: {Name}", ProfileName);
            
            ProfileName = string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error saving profile");
            ErrorMessage = $"Error saving profile: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteProfileAsync(SshConnectionProfile? profile)
    {
        if (profile == null) return;

        try
        {
            SavedProfiles.Remove(profile);
            await SaveProfilesToFileAsync();
            
            StatusMessage = $"Profile '{profile.Name}' deleted";
            Logger.Info("Profile deleted: {Name}", profile.Name);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting profile");
            ErrorMessage = $"Error deleting profile: {ex.Message}";
        }
    }

    [RelayCommand]
    private void TestConnection()
    {
        // Простая валидация без подключения
        var settings = CreateConnectionSettings();
        
        if (settings.IsValid(out var error))
        {
            StatusMessage = "Settings are valid";
            ErrorMessage = null;
        }
        else
        {
            ErrorMessage = error;
            StatusMessage = "Settings validation failed";
        }
    }

    #endregion

    #region Property Changed Handlers

    partial void OnAuthenticationMethodChanged(AuthenticationMethod value)
    {
        IsPasswordAuthSelected = value == AuthenticationMethod.Password;
        IsPrivateKeyAuthSelected = value == AuthenticationMethod.PrivateKey;
        
        ConnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnHostnameChanged(string value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
        ErrorMessage = null;
    }

    partial void OnUsernameChanged(string value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
        ErrorMessage = null;
    }

    partial void OnPortChanged(int value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProfileChanged(SshConnectionProfile? value)
    {
        if (value == null) return;

        LoadProfileSettings(value);
        StatusMessage = $"Profile '{value.Name}' loaded";
    }

    #endregion

    #region Helper Methods

    private SshConnectionSettings CreateConnectionSettings()
    {
        return new SshConnectionSettings
        {
            Hostname = Hostname.Trim(),
            Port = Port,
            Username = Username.Trim(),
            AuthMethod = AuthenticationMethod,
            Password = AuthenticationMethod == AuthenticationMethod.Password ? Password : null,
            PrivateKeyPath = AuthenticationMethod == AuthenticationMethod.PrivateKey ? PrivateKeyPath : null,
            KeyPassphrase = AuthenticationMethod == AuthenticationMethod.PrivateKey && !string.IsNullOrWhiteSpace(KeyPassphrase) 
                ? KeyPassphrase 
                : null,
            RemoteDirectory = RemoteDirectory.Trim(),
            DeleteAfterProcessing = DeleteAfterProcessing,
            ConnectionTimeout = 30
        };
    }

    private void LoadProfileSettings(SshConnectionProfile profile)
    {
        var settings = profile.Settings;
        
        Hostname = settings.Hostname;
        Port = settings.Port;
        Username = settings.Username;
        AuthenticationMethod = settings.AuthMethod;
        PrivateKeyPath = settings.PrivateKeyPath ?? string.Empty;
        KeyPassphrase = settings.KeyPassphrase ?? string.Empty;
        RemoteDirectory = settings.RemoteDirectory;
        DeleteAfterProcessing = settings.DeleteAfterProcessing;

        // Пытаемся загрузить сохранённый пароль (асинхронно, с маршалингом записи свойств на UI-поток)
        if (_credentialStorage != null && settings.AuthMethod == AuthenticationMethod.Password)
        {
            _ = LoadSavedPasswordAsync(settings.Hostname, settings.Username);
        }
    }

    private async Task LoadSavedPasswordAsync(string hostname, string username)
    {
        if (_credentialStorage == null)
            return;

        try
        {
            var savedPassword = await _credentialStorage.GetPasswordAsync(hostname, username);

            if (string.IsNullOrEmpty(savedPassword))
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Password = savedPassword;
                RememberPassword = true;
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading saved password for {Username}@{Hostname}", username, hostname);
        }
    }

    private void LoadSavedProfiles()
    {
        try
        {
            var profilesPath = GetProfilesFilePath();
            
            if (!File.Exists(profilesPath))
            {
                Logger.Debug("No saved profiles found");
                return;
            }

            var json = File.ReadAllText(profilesPath);
            var profiles = System.Text.Json.JsonSerializer.Deserialize<List<SshConnectionProfile>>(json);

            if (profiles != null)
            {
                SavedProfiles.Clear();
                foreach (var profile in profiles.OrderByDescending(p => p.LastUsedAt ?? p.CreatedAt))
                {
                    SavedProfiles.Add(profile);
                }
                
                Logger.Info("Loaded {Count} profiles", profiles.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading profiles");
        }
    }

    private async Task SaveProfilesToFileAsync()
    {
        try
        {
            var profilesPath = GetProfilesFilePath();
            var directory = Path.GetDirectoryName(profilesPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(
                SavedProfiles.ToList(),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(profilesPath, json);
            
            Logger.Debug("Profiles saved to {Path}", profilesPath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error saving profiles");
            throw;
        }
    }

    private static string GetProfilesFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "FirebirdTraceAnalyzer");
        return Path.Combine(appFolder, "ssh_profiles.json");
    }

    #endregion
}