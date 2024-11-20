using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using MultiToolLoader.Services;
using System.Windows.Threading;
using System.Linq;
using MultiToolLoader.Models;
using System.Collections.Generic;
using System.Windows;
using System.Text.Json;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MultiToolLoader.Controls
{
    public partial class ChatControl : UserControl, INotifyPropertyChanged
    {
        private string _messageInput = string.Empty;
        private bool _isLoading;
        private readonly IChatService _chatService;
        private AIModel _selectedModel = null!;
        private string _currentModelInfo = string.Empty;
        private readonly ISettingsService _settingsService;
        private readonly ILoggingService _loggingService;
        private string _conversationId;
        private bool _showPrompts;
        private int _remainingTokens;
        private ChatStatistics _usageStats = new();
        private readonly DispatcherTimer _tokenUpdateTimer;

        public ObservableCollection<ChatMessage> Messages { get; } = new();
        public ObservableCollection<AIModel> AvailableModels { get; }
        public ObservableCollection<CustomPrompt> CustomPrompts { get; } = new();

        public ChatControl()
        {
            InitializeComponent();
            _settingsService = App.SettingsService;
            _loggingService = App.LoggingService;
            _chatService = new ChatService(_settingsService, _loggingService);
            _conversationId = Guid.NewGuid().ToString();

            AvailableModels = new ObservableCollection<AIModel>(_chatService.AvailableModels);

            var defaultModelId = _settingsService.CurrentSettings.DefaultModelId;
            SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == defaultModelId) ?? AvailableModels.First();

            InitializeCommands();
            LoadCustomPrompts();
            InitializeEventHandlers();

            _tokenUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _tokenUpdateTimer.Tick += (s, e) => UpdateRemainingTokens();
            _tokenUpdateTimer.Start();

            DataContext = this;
        }

        private void InitializeCommands()
        {
            SendMessageCommand = new RelayCommand(async () => await SendMessageAsync(), CanSendMessage);
            ClearCommand = new RelayCommand(ClearChat);
            ExportChatCommand = new RelayCommand(async () => await ExportChatAsync());
            SaveCustomPromptCommand = new RelayCommand(async () => await SaveCustomPromptAsync());
            CopyMessageCommand = new RelayCommand<ChatMessage>(CopyMessage);
            RegenerateResponseCommand = new RelayCommand<ChatMessage>(async (msg) => await RegenerateResponseAsync(msg));
            UsePromptCommand = new RelayCommand<CustomPrompt>(UsePrompt);
            DeletePromptCommand = new RelayCommand<CustomPrompt>(async (prompt) => await DeletePromptAsync(prompt));
        }

        private void InitializeEventHandlers()
        {
            Messages.CollectionChanged += MessagesCollectionChanged;
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MessageInput))
                {
                    UpdateRemainingTokens();
                }
            };

            _chatService.ModelUsageUpdated += (s, e) =>
            {
                if (e.ModelId == SelectedModel.Id)
                {
                    UsageStats = e.Statistics;
                }
            };

            _settingsService.SettingsChanged += async (s, settings) =>
            {
                var newModel = AvailableModels.FirstOrDefault(m => m.Id == settings.DefaultModelId);
                if (newModel != null && newModel != SelectedModel)
                {
                    SelectedModel = newModel;
                }
                await LoadCustomPrompts();
            };
        }

        private void MessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    MessageList.ScrollIntoView(MessageList.Items[^1]);
                }));
            }
        }

        public AIModel SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (_selectedModel != value)
                {
                    _selectedModel = value;
                    _chatService.SetModel(value.Id);
                    UpdateModelInfo();
                    OnPropertyChanged();
                }
            }
        }

        public ChatStatistics UsageStats
        {
            get => _usageStats;
            set
            {
                _usageStats = value;
                OnPropertyChanged();
            }
        }

        public string CurrentModelInfo
        {
            get => _currentModelInfo;
            set
            {
                _currentModelInfo = value;
                OnPropertyChanged();
            }
        }

        public string MessageInput
        {
            get => _messageInput;
            set
            {
                _messageInput = value;
                OnPropertyChanged();
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool ShowPrompts
        {
            get => _showPrompts;
            set
            {
                _showPrompts = value;
                OnPropertyChanged();
            }
        }

        public int RemainingTokens
        {
            get => _remainingTokens;
            set
            {
                _remainingTokens = value;
                OnPropertyChanged();
            }
        }

        public ICommand SendMessageCommand { get; private set; }
        public ICommand ClearCommand { get; private set; }
        public ICommand ExportChatCommand { get; private set; }
        public ICommand SaveCustomPromptCommand { get; private set; }
        public ICommand CopyMessageCommand { get; private set; }
        public ICommand RegenerateResponseCommand { get; private set; }
        public ICommand UsePromptCommand { get; private set; }
        public ICommand DeletePromptCommand { get; private set; }

        private void UpdateModelInfo()
        {
            var model = SelectedModel;
            var stats = UsageStats;
            CurrentModelInfo = $"{model.Name} - Max Tokens: {model.MaxTokens}, " +
                             $"Erfolgsrate: {(1 - model.ErrorRate):P}";
        }

        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(MessageInput) &&
                   !IsLoading &&
                   RemainingTokens > 0;
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(MessageInput)) return;

            var userMessage = new ChatMessage
            {
                Content = MessageInput,
                IsUser = true,
                Timestamp = DateTime.Now,
                TokenCount = EstimateTokenCount(MessageInput)
            };
            Messages.Add(userMessage);

            var inputText = MessageInput;
            MessageInput = string.Empty;
            IsLoading = true;

            try
            {
                var response = await _chatService.GetResponseAsync(inputText, _conversationId);
                var responseMessage = new ChatMessage
                {
                    Content = response,
                    IsUser = false,
                    Timestamp = DateTime.Now,
                    TokenCount = EstimateTokenCount(response)
                };
                Messages.Add(responseMessage);

                await _chatService.SaveChatHistoryAsync(_conversationId, Messages.ToList());
                _loggingService.LogInformation($"Message sent and response received. Tokens: {responseMessage.TokenCount}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error sending message", ex);
                Messages.Add(new ChatMessage
                {
                    Content = $"Entschuldigung, es gab einen Fehler: {ex.Message}",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateRemainingTokens()
        {
            var estimatedTokens = EstimateTokenCount(MessageInput);
            RemainingTokens = Math.Max(0, SelectedModel.MaxTokens - estimatedTokens);
        }

        private int EstimateTokenCount(string text)
        {
            return text.Length / 4; // Vereinfachte Schätzung
        }

        private async Task LoadCustomPrompts()
        {
            var prompts = await _chatService.GetCustomPromptsAsync();
            CustomPrompts.Clear();
            foreach (var promptName in prompts)
            {
                CustomPrompts.Add(new CustomPrompt { Name = promptName });
            }
        }

        private async Task SaveCustomPromptAsync()
        {
            var dialog = new CustomPromptDialog();
            if (dialog.ShowDialog() == true)
            {
                await _chatService.SaveCustomPromptAsync(dialog.PromptName, dialog.PromptText);
                await LoadCustomPrompts();
            }
        }

        private void CopyMessage(ChatMessage message)
        {
            try
            {
                Clipboard.SetText(message.Content);
                _loggingService.LogInformation("Message copied to clipboard");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error copying message", ex);
            }
        }

        private async Task RegenerateResponseAsync(ChatMessage message)
        {
            if (message.IsUser) return;

            var previousMessage = Messages
                .TakeWhile(m => m != message)
                .LastOrDefault(m => m.IsUser);

            if (previousMessage != null)
            {
                Messages.Remove(message);
                IsLoading = true;

                try
                {
                    var newResponse = await _chatService.GetResponseAsync(previousMessage.Content, _conversationId);
                    Messages.Add(new ChatMessage
                    {
                        Content = newResponse,
                        IsUser = false,
                        Timestamp = DateTime.Now,
                        TokenCount = EstimateTokenCount(newResponse)
                    });
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error regenerating response", ex);
                    Messages.Add(new ChatMessage
                    {
                        Content = $"Fehler bei der Neugenerierung: {ex.Message}",
                        IsUser = false,
                        Timestamp = DateTime.Now
                    });
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private void UsePrompt(CustomPrompt prompt)
        {
            MessageInput = prompt.Prompt;
        }

        private async Task DeletePromptAsync(CustomPrompt prompt)
        {
            if (MessageBox.Show(
                $"Möchten Sie den Prompt '{prompt.Name}' wirklich löschen?",
                "Prompt löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _chatService.DeleteCustomPromptAsync(prompt.Name);
                await LoadCustomPrompts();
            }
        }

        private void ClearChat()
        {
            Messages.Clear();
            _conversationId = Guid.NewGuid().ToString();
        }

        private async Task ExportChatAsync()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var export = new ChatExport
                    {
                        Messages = Messages.ToList(),
                        ModelInfo = SelectedModel,
                        ExportDate = DateTime.Now,
                        Statistics = UsageStats
                    };

                    var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    await File.WriteAllTextAsync(dialog.FileName, json);
                    _loggingService.LogInformation($"Chat exported to {dialog.FileName}");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error exporting chat", ex);
                    MessageBox.Show(
                        "Fehler beim Exportieren des Chats: " + ex.Message,
                        "Exportfehler",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                if (SendMessageCommand.CanExecute(null))
                {
                    SendMessageCommand.Execute(null);
                }
                e.Handled = true;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CustomPrompt
    {
        public string Name { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
    }

    public class ChatExport
    {
        public List<ChatMessage> Messages { get; set; } = new();
        public AIModel ModelInfo { get; set; } = null!;
        public DateTime ExportDate { get; set; }
        public ChatStatistics Statistics { get; set; } = null!;
    }
}