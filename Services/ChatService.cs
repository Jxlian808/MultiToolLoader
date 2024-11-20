using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using MultiToolLoader.Models;

namespace MultiToolLoader.Services
{
    public interface IChatService
    {
        Task<string> GetResponseAsync(string message, string? conversationId = null);
        void SetModel(string modelId);
        string CurrentModel { get; }
        IEnumerable<AIModel> AvailableModels { get; }
        Task<List<ChatMessage>> GetChatHistoryAsync(string conversationId);
        Task SaveChatHistoryAsync(string conversationId, List<ChatMessage> messages);
        Task ClearChatHistoryAsync(string conversationId);
        Task<Dictionary<string, ChatStatistics>> GetChatStatisticsAsync();
        event EventHandler<ModelUsageEventArgs>? ModelUsageUpdated;
        Task<IEnumerable<string>> GetCustomPromptsAsync();
        Task SaveCustomPromptAsync(string name, string prompt);
        Task DeleteCustomPromptAsync(string name);
    }

    public class ChatService : IChatService
    {
        private readonly HttpClient _httpClient;
        private AIModel _currentModel = null!;
        private readonly List<AIModel> _availableModels;
        private readonly ISettingsService _settingsService;
        private readonly ILoggingService _loggingService;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _rateLimiters;
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _usageTracking;
        private readonly ConcurrentDictionary<string, ChatStatistics> _chatStatistics;
        private readonly int _maxRequestsPerMinute = 10;

        public event EventHandler<ModelUsageEventArgs>? ModelUsageUpdated;

        public IEnumerable<AIModel> AvailableModels => _availableModels;
        public string CurrentModel => _currentModel.Name;

        public ChatService(ISettingsService settingsService, ILoggingService loggingService)
        {
            _settingsService = settingsService;
            _loggingService = loggingService;
            _rateLimiters = new ConcurrentDictionary<string, SemaphoreSlim>();
            _usageTracking = new ConcurrentDictionary<string, Queue<DateTime>>();
            _chatStatistics = new ConcurrentDictionary<string, ChatStatistics>();

            var handler = new HttpClientHandler
            {
                UseProxy = false,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _httpClient = new HttpClient(handler);
            UpdateApiKey(_settingsService.CurrentSettings.ApiKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            _availableModels = new List<AIModel>
            {
                new AIModel
                {
                    Id = "mixtral",
                    Name = "Mixtral-8x7B",
                    ApiEndpoint = "https://api-inference.huggingface.co/models/mistralai/Mixtral-8x7B-Instruct-v0.1",
                    MaxTokens = 1024,
                    Temperature = 0.7,
                    SystemPrompt = "Du bist ein hilfreicher Assistent. Antworte präzise und klar.",
                    RequestsPerMinute = 10,
                    CostPerRequest = 0.0001m
                },
                new AIModel
                {
                    Id = "llama",
                    Name = "Llama-2-70b",
                    ApiEndpoint = "https://api-inference.huggingface.co/models/meta-llama/Llama-2-70b-chat-hf",
                    MaxTokens = 2048,
                    Temperature = 0.8,
                    SystemPrompt = "Du bist ein fortgeschrittener KI-Assistent, der komplexe Aufgaben lösen kann.",
                    RequestsPerMinute = 8,
                    CostPerRequest = 0.0002m
                },
                new AIModel
                {
                    Id = "codellama",
                    Name = "CodeLlama-34b",
                    ApiEndpoint = "https://api-inference.huggingface.co/models/codellama/CodeLlama-34b-Instruct-hf",
                    MaxTokens = 2048,
                    Temperature = 0.5,
                    SystemPrompt = "Du bist ein Programmier-Experte. Liefere präzisen, gut dokumentierten Code.",
                    RequestsPerMinute = 12,
                    CostPerRequest = 0.00015m
                }
            };

            var defaultModelId = _settingsService.CurrentSettings.DefaultModelId;
            _currentModel = _availableModels.Find(m => m.Id == defaultModelId) ?? _availableModels[0];

            InitializeRateLimiters();

            _settingsService.SettingsChanged += (s, settings) =>
            {
                UpdateApiKey(settings.ApiKey);
                var newModel = _availableModels.Find(m => m.Id == settings.DefaultModelId);
                if (newModel != null && newModel.Id != _currentModel.Id)
                {
                    SetModel(newModel.Id);
                }
            };
        }

        private void InitializeRateLimiters()
        {
            foreach (var model in _availableModels)
            {
                _rateLimiters.TryAdd(model.Id, new SemaphoreSlim(1, 1));
                _usageTracking.TryAdd(model.Id, new Queue<DateTime>());
                _chatStatistics.TryAdd(model.Id, new ChatStatistics());
            }
        }

        private void UpdateApiKey(string apiKey)
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public void SetModel(string modelId)
        {
            var model = _availableModels.Find(m => m.Id == modelId);
            if (model != null)
            {
                _currentModel = model;
                _loggingService.LogInformation($"Model changed to {model.Name}");
            }
            else
            {
                _loggingService.LogError($"Model {modelId} not found");
                throw new ArgumentException($"Model {modelId} not found");
            }
        }

        public async Task<string> GetResponseAsync(string message, string? conversationId = null)
        {
            try
            {
                await EnforceRateLimitAsync(_currentModel.Id);
                await TrackUsageAsync(_currentModel.Id);

                var systemPrompt = await GetCustomPromptAsync(_currentModel.Id) ?? _currentModel.SystemPrompt;
                var prompt = $"[INST] {systemPrompt}\n\n{message} [/INST]";

                var requestObject = new
                {
                    inputs = prompt,
                    parameters = new
                    {
                        max_new_tokens = _currentModel.MaxTokens,
                        temperature = _currentModel.Temperature,
                        top_p = 0.95,
                        return_full_text = false,
                        do_sample = true,
                        num_return_sequences = 1
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestObject);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                int maxRetries = 3;
                int currentRetry = 0;
                string? response = null;

                while (currentRetry < maxRetries && response == null)
                {
                    try
                    {
                        response = await ExecuteRequestAsync(content);
                    }
                    catch (HttpRequestException ex)
                    {
                        currentRetry++;
                        if (currentRetry == maxRetries) throw;
                        await Task.Delay(2000 * currentRetry);
                        _loggingService.LogWarning($"Retry {currentRetry} after error: {ex.Message}");
                    }
                }

                UpdateStatistics(_currentModel.Id, message.Length, response?.Length ?? 0);

                if (!string.IsNullOrEmpty(conversationId))
                {
                    await SaveMessageToHistoryAsync(conversationId, message, response ?? "Error: No response");
                }

                return response ?? "Error: No response received";
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error in GetResponseAsync", ex);
                throw;
            }
        }

        private async Task<string?> ExecuteRequestAsync(StringContent content)
        {
            var response = await _httpClient.PostAsync(_currentModel.ApiEndpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                if (responseContent.Contains("loading"))
                {
                    await Task.Delay(2000);
                    return null;
                }

                var result = JsonSerializer.Deserialize<JsonElement[]>(responseContent);
                if (result != null && result.Length > 0)
                {
                    return result[0].GetProperty("generated_text").GetString()?
                        .Replace("[/INST]", "")
                        .Replace("[INST]", "")
                        .Trim();
                }
            }

            throw new HttpRequestException($"API Error: {response.StatusCode} - {responseContent}");
        }

        private async Task EnforceRateLimitAsync(string modelId)
        {
            var limiter = _rateLimiters.GetOrAdd(modelId, new SemaphoreSlim(1, 1));
            await limiter.WaitAsync();

            try
            {
                var usageQueue = _usageTracking.GetOrAdd(modelId, new Queue<DateTime>());
                var now = DateTime.UtcNow;

                while (usageQueue.Count > 0 && (now - usageQueue.Peek()).TotalMinutes >= 1)
                {
                    usageQueue.Dequeue();
                }

                if (usageQueue.Count >= _maxRequestsPerMinute)
                {
                    var waitTime = 60 - (now - usageQueue.Peek()).TotalSeconds;
                    if (waitTime > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(waitTime));
                    }
                }

                usageQueue.Enqueue(now);
            }
            finally
            {
                limiter.Release();
            }
        }

        private async Task TrackUsageAsync(string modelId)
        {
            var stats = _chatStatistics.GetOrAdd(modelId, new ChatStatistics());
            stats.TotalRequests++;
            ModelUsageUpdated?.Invoke(this, new ModelUsageEventArgs(modelId, stats));
        }

        private void UpdateStatistics(string modelId, int inputLength, int outputLength)
        {
            var stats = _chatStatistics.GetOrAdd(modelId, new ChatStatistics());
            stats.TotalTokens += inputLength + outputLength;
            stats.LastUsed = DateTime.UtcNow;
        }

        private async Task<string?> GetCustomPromptAsync(string modelId)
        {
            var settings = _settingsService.CurrentSettings;
            return settings.CustomPrompts.GetValueOrDefault(modelId);
        }

        public async Task<List<ChatMessage>> GetChatHistoryAsync(string conversationId)
        {
            var settings = _settingsService.CurrentSettings;
            return settings.ChatHistory.GetValueOrDefault(conversationId) ?? new List<ChatMessage>();
        }

        public async Task SaveChatHistoryAsync(string conversationId, List<ChatMessage> messages)
        {
            var settings = _settingsService.CurrentSettings;
            settings.ChatHistory[conversationId] = messages;
            await _settingsService.SaveSettingsAsync(settings);
        }

        public async Task ClearChatHistoryAsync(string conversationId)
        {
            var settings = _settingsService.CurrentSettings;
            settings.ChatHistory.Remove(conversationId);
            await _settingsService.SaveSettingsAsync(settings);
        }

        private async Task SaveMessageToHistoryAsync(string conversationId, string userMessage, string aiResponse)
        {
            var settings = _settingsService.CurrentSettings;
            if (!settings.ChatHistory.ContainsKey(conversationId))
            {
                settings.ChatHistory[conversationId] = new List<ChatMessage>();
            }

            var history = settings.ChatHistory[conversationId];
            history.Add(new ChatMessage
            {
                Content = userMessage,
                IsUser = true,
                Timestamp = DateTime.UtcNow
            });
            history.Add(new ChatMessage
            {
                Content = aiResponse,
                IsUser = false,
                Timestamp = DateTime.UtcNow
            });

            await _settingsService.SaveSettingsAsync(settings);
        }

        public async Task<Dictionary<string, ChatStatistics>> GetChatStatisticsAsync()
        {
            return new Dictionary<string, ChatStatistics>(_chatStatistics);
        }

        public async Task<IEnumerable<string>> GetCustomPromptsAsync()
        {
            return _settingsService.CurrentSettings.CustomPrompts.Keys;
        }

        public async Task SaveCustomPromptAsync(string name, string prompt)
        {
            var settings = _settingsService.CurrentSettings;
            settings.CustomPrompts[name] = prompt;
            await _settingsService.SaveSettingsAsync(settings);
        }

        public async Task DeleteCustomPromptAsync(string name)
        {
            var settings = _settingsService.CurrentSettings;
            settings.CustomPrompts.Remove(name);
            await _settingsService.SaveSettingsAsync(settings);
        }
    }

    public class ChatStatistics
    {
        public int TotalRequests { get; set; }
        public int TotalTokens { get; set; }
        public DateTime LastUsed { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    public class ModelUsageEventArgs : EventArgs
    {
        public string ModelId { get; }
        public ChatStatistics Statistics { get; }

        public ModelUsageEventArgs(string modelId, ChatStatistics statistics)
        {
            ModelId = modelId;
            Statistics = statistics;
        }
    }
}