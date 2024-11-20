using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MultiToolLoader.Services
{
    public interface ISettingsService
    {
        Settings CurrentSettings { get; }
        event EventHandler<Settings>? SettingsChanged;
        Task SaveSettingsAsync(Settings settings);
        Task LoadSettingsAsync();
        Task<string> ExportSettingsAsync();
        Task ImportSettingsAsync(string jsonSettings);
        Task BackupSettingsAsync();
        Task RestoreSettingsAsync(string backupPath);
        IEnumerable<string> GetBackupFiles();
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsPath;
        private readonly string _backupPath;
        private Settings _currentSettings = new();
        private readonly ILoggingService _loggingService;
        private readonly byte[] _encryptionKey;

        public Settings CurrentSettings => _currentSettings;
        public event EventHandler<Settings>? SettingsChanged;

        public SettingsService(ILoggingService loggingService)
        {
            _loggingService = loggingService;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appData, "MultiToolLoader");

            _settingsPath = Path.Combine(appFolder, "settings.json");
            _backupPath = Path.Combine(appFolder, "backups");

            Directory.CreateDirectory(appFolder);
            Directory.CreateDirectory(_backupPath);

            _encryptionKey = GetOrCreateEncryptionKey(appFolder);
        }

        private byte[] GetOrCreateEncryptionKey(string folder)
        {
            var keyPath = Path.Combine(folder, "key.dat");
            if (File.Exists(keyPath))
            {
                return File.ReadAllBytes(keyPath);
            }

            var key = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            File.WriteAllBytes(keyPath, key);
            return key;
        }

        private string EncryptString(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var resultBytes = new byte[aes.IV.Length + cipherBytes.Length];
            aes.IV.CopyTo(resultBytes, 0);
            cipherBytes.CopyTo(resultBytes, aes.IV.Length);

            return Convert.ToBase64String(resultBytes);
        }

        private string DecryptString(string cipherText)
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.Key = _encryptionKey;

            var iv = new byte[aes.IV.Length];
            var cipher = new byte[cipherBytes.Length - iv.Length];
            Array.Copy(cipherBytes, iv, iv.Length);
            Array.Copy(cipherBytes, iv.Length, cipher, 0, cipher.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public async Task SaveSettingsAsync(Settings settings)
        {
            try
            {
                var encryptedApiKey = EncryptString(settings.ApiKey);
                var settingsToSave = new Settings
                {
                    IsDarkMode = settings.IsDarkMode,
                    ApiKey = encryptedApiKey,
                    DefaultModelId = settings.DefaultModelId,
                    CustomPrompts = settings.CustomPrompts,
                    MaxTokens = settings.MaxTokens,
                    Temperature = settings.Temperature,
                    ChatHistory = settings.ChatHistory,
                    UserPreferences = settings.UserPreferences
                };

                var json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_settingsPath, json);
                _currentSettings = settings;
                SettingsChanged?.Invoke(this, settings);
                await BackupSettingsAsync();
                _loggingService.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error saving settings", ex);
                throw;
            }
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = await File.ReadAllTextAsync(_settingsPath);
                    var loadedSettings = JsonSerializer.Deserialize<Settings>(json);
                    if (loadedSettings != null)
                    {
                        loadedSettings.ApiKey = DecryptString(loadedSettings.ApiKey);
                        _currentSettings = loadedSettings;
                    }
                }
                else
                {
                    _currentSettings = new Settings
                    {
                        IsDarkMode = true,
                        ApiKey = "default_key",
                        DefaultModelId = "mixtral",
                        MaxTokens = 1024,
                        Temperature = 0.7,
                        CustomPrompts = new Dictionary<string, string>(),
                        ChatHistory = new Dictionary<string, List<ChatMessage>>(),
                        UserPreferences = new Dictionary<string, object>()
                    };
                    await SaveSettingsAsync(_currentSettings);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error loading settings", ex);
                throw;
            }
        }

        public async Task<string> ExportSettingsAsync()
        {
            var settings = new Settings
            {
                IsDarkMode = _currentSettings.IsDarkMode,
                DefaultModelId = _currentSettings.DefaultModelId,
                CustomPrompts = _currentSettings.CustomPrompts,
                MaxTokens = _currentSettings.MaxTokens,
                Temperature = _currentSettings.Temperature,
                UserPreferences = _currentSettings.UserPreferences
            };

            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        }

        public async Task ImportSettingsAsync(string jsonSettings)
        {
            try
            {
                var importedSettings = JsonSerializer.Deserialize<Settings>(jsonSettings);
                if (importedSettings != null)
                {
                    importedSettings.ApiKey = _currentSettings.ApiKey;
                    await SaveSettingsAsync(importedSettings);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error importing settings", ex);
                throw;
            }
        }

        public async Task BackupSettingsAsync()
        {
            try
            {
                var backupFileName = $"settings_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var backupFilePath = Path.Combine(_backupPath, backupFileName);
                var json = await File.ReadAllTextAsync(_settingsPath);
                await File.WriteAllTextAsync(backupFilePath, json);

                var backupFiles = Directory.GetFiles(_backupPath, "settings_backup_*.json");
                if (backupFiles.Length > 5)
                {
                    Array.Sort(backupFiles);
                    File.Delete(backupFiles[0]);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error creating settings backup", ex);
                throw;
            }
        }

        public async Task RestoreSettingsAsync(string backupPath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(backupPath);
                await File.WriteAllTextAsync(_settingsPath, json);
                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error restoring settings from backup", ex);
                throw;
            }
        }

        public IEnumerable<string> GetBackupFiles()
        {
            return Directory.GetFiles(_backupPath, "settings_backup_*.json");
        }
    }

    public class Settings
    {
        public bool IsDarkMode { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string DefaultModelId { get; set; } = string.Empty;
        public int MaxTokens { get; set; } = 1024;
        public double Temperature { get; set; } = 0.7;
        public Dictionary<string, string> CustomPrompts { get; set; } = new();
        public Dictionary<string, List<ChatMessage>> ChatHistory { get; set; } = new();
        public Dictionary<string, object> UserPreferences { get; set; } = new();
    }

    public class ChatMessage
    {
        public string Content { get; set; } = string.Empty;
        public bool IsUser { get; set; }
        public DateTime Timestamp { get; set; }
    }
}