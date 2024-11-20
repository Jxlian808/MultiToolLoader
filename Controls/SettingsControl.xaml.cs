using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using MultiToolLoader.Services;
using System.Linq;
using MultiToolLoader.Models;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;

namespace MultiToolLoader.Controls
{
    public partial class SettingsControl : UserControl, INotifyPropertyChanged
    {
        private bool _isDarkMode;
        private string _apiKey = string.Empty;
        private AIModel _defaultModel = null!;
        private double _temperature = 0.7;
        private int _maxTokens = 1024;
        private string _selectedFontSize = "Normal";
        private ObservableCollection<string> _backupFiles = new();
        private readonly ISettingsService _settingsService;
        private readonly IChatService _chatService;
        private readonly ILoggingService _loggingService;

        public ObservableCollection<AIModel> AvailableModels { get; }
        public ObservableCollection<UsageStatistics> UsageStatistics { get; } = new();
        public ObservableCollection<string> FontSizes { get; } = new() { "Klein", "Normal", "Groß" };

        public SettingsControl()
        {
            InitializeComponent();
            _settingsService = App.SettingsService;
            _loggingService = App.LoggingService;
            _chatService = new ChatService(_settingsService, _loggingService);

            AvailableModels = new ObservableCollection<AIModel>(_chatService.AvailableModels);

            InitializeCommands();
            LoadSettings();
            LoadBackupFiles();
            InitializeUsageStatistics();
            DataContext = this;

            _settingsService.SettingsChanged += (s, settings) => LoadSettings();
            _chatService.ModelUsageUpdated += (s, e) => UpdateModelStatistics(e.ModelId, e.Statistics);
        }

        private void InitializeCommands()
        {
            SaveApiKeyCommand = new RelayCommand(async () => await SaveApiKeyAsync());
            CreateBackupCommand = new RelayCommand(async () => await CreateBackupAsync());
            RestoreBackupCommand = new RelayCommand(async () => await RestoreBackupAsync());
            ResetSettingsCommand = new RelayCommand(async () => await ResetSettingsAsync());
            ExportSettingsCommand = new RelayCommand(async () => await ExportSettingsAsync());
            ImportSettingsCommand = new RelayCommand(async () => await ImportSettingsAsync());
        }

        private void InitializeUsageStatistics()
        {
            foreach (var model in AvailableModels)
            {
                UsageStatistics.Add(new UsageStatistics
                {
                    ModelName = model.Name,
                    ModelId = model.Id
                });
            }
        }

        private void UpdateModelStatistics(string modelId, ChatStatistics stats)
        {
            var statistic = UsageStatistics.FirstOrDefault(s => s.ModelId == modelId);
            if (statistic != null)
            {
                statistic.TotalRequests = stats.TotalRequests;
                statistic.TotalTokens = stats.TotalTokens;
                statistic.TotalCost = stats.EstimatedCost;
                OnPropertyChanged(nameof(UsageStatistics));
            }
        }

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    OnPropertyChanged();
                    SaveSettingsAsync().ConfigureAwait(false);
                }
            }
        }

        public string SelectedFontSize
        {
            get => _selectedFontSize;
            set
            {
                if (_selectedFontSize != value)
                {
                    _selectedFontSize = value;
                    OnPropertyChanged();
                    UpdateGlobalFontSize();
                    SaveSettingsAsync().ConfigureAwait(false);
                }
            }
        }

        public double Temperature
        {
            get => _temperature;
            set
            {
                if (_temperature != value)
                {
                    _temperature = value;
                    OnPropertyChanged();
                    SaveSettingsAsync().ConfigureAwait(false);
                }
            }
        }

        public int MaxTokens
        {
            get => _maxTokens;
            set
            {
                if (_maxTokens != value)
                {
                    _maxTokens = value;
                    OnPropertyChanged();
                    SaveSettingsAsync().ConfigureAwait(false);
                }
            }
        }

        public AIModel DefaultModel
        {
            get => _defaultModel;
            set
            {
                if (_defaultModel != value)
                {
                    _defaultModel = value;
                    OnPropertyChanged();
                    SaveSettingsAsync().ConfigureAwait(false);
                }
            }
        }

        public ObservableCollection<string> BackupFiles
        {
            get => _backupFiles;
            set
            {
                _backupFiles = value;
                OnPropertyChanged();
            }
        }

        public ICommand SaveApiKeyCommand { get; private set; }
        public ICommand CreateBackupCommand { get; private set; }
        public ICommand RestoreBackupCommand { get; private set; }
        public ICommand ResetSettingsCommand { get; private set; }
        public ICommand ExportSettingsCommand { get; private set; }
        public ICommand ImportSettingsCommand { get; private set; }

        private async Task LoadSettings()
        {
            var settings = _settingsService.CurrentSettings;
            _isDarkMode = settings.IsDarkMode;
            ApiKeyBox.Password = settings.ApiKey;
            _defaultModel = AvailableModels.FirstOrDefault(m => m.Id == settings.DefaultModelId) ?? AvailableModels.First();
            _temperature = settings.Temperature;
            _maxTokens = settings.MaxTokens;
            _selectedFontSize = settings.UserPreferences.GetValueOrDefault("FontSize", "Normal").ToString();

            OnPropertyChanged(nameof(IsDarkMode));
            OnPropertyChanged(nameof(DefaultModel));
            OnPropertyChanged(nameof(Temperature));
            OnPropertyChanged(nameof(MaxTokens));
            OnPropertyChanged(nameof(SelectedFontSize));
        }

        private void LoadBackupFiles()
        {
            BackupFiles.Clear();
            foreach (var file in _settingsService.GetBackupFiles())
            {
                BackupFiles.Add(Path.GetFileName(file));
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                var settings = new Settings
                {
                    IsDarkMode = _isDarkMode,
                    ApiKey = ApiKeyBox.Password,
                    DefaultModelId = _defaultModel?.Id ?? "mixtral",
                    Temperature = _temperature,
                    MaxTokens = _maxTokens,
                    UserPreferences = new Dictionary<string, object>
                    {
                        { "FontSize", _selectedFontSize }
                    }
                };

                await _settingsService.SaveSettingsAsync(settings);
                _loggingService.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error saving settings", ex);
                ShowError("Fehler beim Speichern der Einstellungen", ex.Message);
            }
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            (SaveApiKeyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task SaveApiKeyAsync()
        {
            if (string.IsNullOrWhiteSpace(ApiKeyBox.Password))
            {
                ShowError("Ungültiger API Key", "Bitte geben Sie einen gültigen API Key ein.");
                return;
            }

            try
            {
                var settings = _settingsService.CurrentSettings;
                settings.ApiKey = ApiKeyBox.Password;
                await _settingsService.SaveSettingsAsync(settings);
                ShowSuccess("API Key gespeichert", "Der API Key wurde erfolgreich gespeichert.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error saving API key", ex);
                ShowError("Fehler", "Der API Key konnte nicht gespeichert werden.");
            }
        }

        private async Task CreateBackupAsync()
        {
            try
            {
                await _settingsService.BackupSettingsAsync();
                LoadBackupFiles();
                ShowSuccess("Backup erstellt", "Die Einstellungen wurden erfolgreich gesichert.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error creating backup", ex);
                ShowError("Backup Fehler", "Beim Erstellen des Backups ist ein Fehler aufgetreten.");
            }
        }

        private async Task RestoreBackupAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Backup files (*.json)|*.json",
                InitialDirectory = Path.GetDirectoryName(_settingsService.GetBackupFiles().FirstOrDefault() ?? "")
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _settingsService.RestoreSettingsAsync(dialog.FileName);
                    await LoadSettings();
                    ShowSuccess("Backup wiederhergestellt", "Die Einstellungen wurden erfolgreich wiederhergestellt.");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error restoring backup", ex);
                    ShowError("Wiederherstellungsfehler", "Beim Wiederherstellen des Backups ist ein Fehler aufgetreten.");
                }
            }
        }

        private async Task ResetSettingsAsync()
        {
            if (MessageBox.Show(
                "Möchten Sie wirklich alle Einstellungen zurücksetzen? Dies kann nicht rückgängig gemacht werden.",
                "Einstellungen zurücksetzen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    var defaultSettings = new Settings
                    {
                        IsDarkMode = true,
                        ApiKey = string.Empty,
                        DefaultModelId = "mixtral",
                        Temperature = 0.7,
                        MaxTokens = 1024
                    };

                    await _settingsService.SaveSettingsAsync(defaultSettings);
                    await LoadSettings();
                    ShowSuccess("Zurückgesetzt", "Die Einstellungen wurden erfolgreich zurückgesetzt.");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error resetting settings", ex);
                    ShowError("Fehler", "Beim Zurücksetzen der Einstellungen ist ein Fehler aufgetreten.");
                }
            }
        }

        private async Task ExportSettingsAsync()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = "json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = await _settingsService.ExportSettingsAsync();
                    await File.WriteAllTextAsync(dialog.FileName, json);
                    ShowSuccess("Export erfolgreich", "Die Einstellungen wurden erfolgreich exportiert.");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error exporting settings", ex);
                    ShowError("Exportfehler", "Beim Exportieren der Einstellungen ist ein Fehler aufgetreten.");
                }
            }
        }

        private async Task ImportSettingsAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(dialog.FileName);
                    await _settingsService.ImportSettingsAsync(json);
                    await LoadSettings();
                    ShowSuccess("Import erfolgreich", "Die Einstellungen wurden erfolgreich importiert.");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error importing settings", ex);
                    ShowError("Importfehler", "Beim Importieren der Einstellungen ist ein Fehler aufgetreten.");
                }
            }
        }

        private void UpdateGlobalFontSize()
        {
            var size = _selectedFontSize switch
            {
                "Klein" => 12d,
                "Normal" => 14d,
                "Groß" => 16d,
                _ => 14d
            };

            Application.Current.Resources["DefaultFontSize"] = size;
        }

        private void ShowError(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccess(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class UsageStatistics : INotifyPropertyChanged
    {
        private string _modelName = string.Empty;
        private string _modelId = string.Empty;
        private int _totalRequests;
        private int _totalTokens;
        private decimal _totalCost;

        public string ModelName
        {
            get => _modelName;
            set
            {
                _modelName = value;
                OnPropertyChanged();
            }
        }

        public string ModelId
        {
            get => _modelId;
            set
            {
                _modelId = value;
                OnPropertyChanged();
            }
        }

        public int TotalRequests
        {
            get => _totalRequests;
            set
            {
                _totalRequests = value;
                OnPropertyChanged();
            }
        }

        public int TotalTokens
        {
            get => _totalTokens;
            set
            {
                _totalTokens = value;
                OnPropertyChanged();
            }
        }

        public decimal TotalCost
        {
            get => _totalCost;
            set
            {
                _totalCost = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}