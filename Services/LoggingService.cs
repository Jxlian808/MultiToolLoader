using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MultiToolLoader.Services
{
    public interface ILoggingService
    {
        void LogInformation(string message, [CallerMemberName] string caller = "");
        void LogWarning(string message, [CallerMemberName] string caller = "");
        void LogError(string message, Exception? ex = null, [CallerMemberName] string caller = "");
        Task<string> GetLogsAsync();
        Task ClearLogsAsync();
    }

    public class LoggingService : ILoggingService
    {
        private readonly string _logPath;
        private readonly ConcurrentQueue<LogEntry> _memoryLogs;
        private readonly int _maxMemoryEntries = 1000;

        public LoggingService()
        {
            _logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MultiToolLoader",
                "logs");
            _memoryLogs = new ConcurrentQueue<LogEntry>();

            Directory.CreateDirectory(_logPath);
            StartLogRotation();
        }

        private string GetLogFilePath() =>
            Path.Combine(_logPath, $"log_{DateTime.Now:yyyy-MM-dd}.json");

        private void StartLogRotation()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                    await RotateLogsAsync();
                }
            });
        }

        private async Task RotateLogsAsync()
        {
            var files = Directory.GetFiles(_logPath, "log_*.json");
            Array.Sort(files);

            while (files.Length > 7)
            {
                File.Delete(files[0]);
                files = Directory.GetFiles(_logPath, "log_*.json");
            }

            if (_memoryLogs.Count > _maxMemoryEntries)
            {
                var toRemove = _memoryLogs.Count - _maxMemoryEntries;
                for (int i = 0; i < toRemove; i++)
                {
                    _memoryLogs.TryDequeue(out _);
                }
            }

            await FlushLogsAsync();
        }

        private async Task FlushLogsAsync()
        {
            var entries = _memoryLogs.ToArray();
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            await File.AppendAllTextAsync(GetLogFilePath(), json);
        }

        public void LogInformation(string message, [CallerMemberName] string caller = "")
        {
            AddLogEntry("INFO", message, null, caller);
        }

        public void LogWarning(string message, [CallerMemberName] string caller = "")
        {
            AddLogEntry("WARNING", message, null, caller);
        }

        public void LogError(string message, Exception? ex = null, [CallerMemberName] string caller = "")
        {
            AddLogEntry("ERROR", message, ex?.ToString(), caller);
        }

        private void AddLogEntry(string level, string message, string? exception, string caller)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Exception = exception,
                Caller = caller
            };

            _memoryLogs.Enqueue(entry);

            if (_memoryLogs.Count >= _maxMemoryEntries)
            {
                Task.Run(FlushLogsAsync);
            }
        }

        public async Task<string> GetLogsAsync()
        {
            await FlushLogsAsync();
            var allLogs = new ConcurrentQueue<LogEntry>();

            foreach (var file in Directory.GetFiles(_logPath, "log_*.json"))
            {
                var content = await File.ReadAllTextAsync(file);
                var entries = JsonSerializer.Deserialize<LogEntry[]>(content);
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        allLogs.Enqueue(entry);
                    }
                }
            }

            foreach (var memoryEntry in _memoryLogs)
            {
                allLogs.Enqueue(memoryEntry);
            }

            return JsonSerializer.Serialize(allLogs.OrderByDescending(x => x.Timestamp),
                new JsonSerializerOptions { WriteIndented = true });
        }

        public async Task ClearLogsAsync()
        {
            _memoryLogs.Clear();
            foreach (var file in Directory.GetFiles(_logPath, "log_*.json"))
            {
                File.Delete(file);
            }
            await Task.CompletedTask;
        }

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Level { get; set; } = "";
            public string Message { get; set; } = "";
            public string? Exception { get; set; }
            public string Caller { get; set; } = "";
        }
    }
}