using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using MultiToolLoader.Services;

namespace MultiToolLoader.Tests
{
    [TestClass]
    public class LoggingServiceTests : TestBase
    {
        private LoggingService _loggingService = null!;
        private string _testLogPath = null!;

        [TestInitialize]
        public override async Task TestInitialize()
        {
            await base.TestInitialize();

            _testLogPath = Path.Combine(Path.GetTempPath(), "MultiToolLoader_Tests", "logs");
            if (Directory.Exists(_testLogPath))
            {
                Directory.Delete(_testLogPath, true);
            }
            Directory.CreateDirectory(_testLogPath);

            _loggingService = new LoggingService(_testLogPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (Directory.Exists(_testLogPath))
            {
                Directory.Delete(_testLogPath, true);
            }
        }

        [TestMethod]
        public void LogInformation_CreatesLogEntry()
        {
            // Arrange
            var message = "Test information message";
            var caller = "TestMethod";

            // Act
            _loggingService.LogInformation(message, caller);

            // Assert
            var logs = _loggingService.GetLogsAsync().Result;
            var logEntries = JsonSerializer.Deserialize<LogEntry[]>(logs);

            Assert.IsNotNull(logEntries);
            Assert.IsTrue(logEntries!.Any(l =>
                l.Level == "INFO" &&
                l.Message == message &&
                l.Caller == caller));
        }

        [TestMethod]
        public void LogWarning_CreatesWarningEntry()
        {
            // Arrange
            var message = "Test warning message";

            // Act
            _loggingService.LogWarning(message);

            // Assert
            var logs = _loggingService.GetLogsAsync().Result;
            var logEntries = JsonSerializer.Deserialize<LogEntry[]>(logs);

            Assert.IsNotNull(logEntries);
            Assert.IsTrue(logEntries!.Any(l =>
                l.Level == "WARNING" &&
                l.Message == message));
        }

        [TestMethod]
        public void LogError_CreatesErrorEntryWithException()
        {
            // Arrange
            var message = "Test error message";
            var exception = new Exception("Test exception");

            // Act
            _loggingService.LogError(message, exception);

            // Assert
            var logs = _loggingService.GetLogsAsync().Result;
            var logEntries = JsonSerializer.Deserialize<LogEntry[]>(logs);

            Assert.IsNotNull(logEntries);
            Assert.IsTrue(logEntries!.Any(l =>
                l.Level == "ERROR" &&
                l.Message == message &&
                l.Exception!.Contains("Test exception")));
        }

        [TestMethod]
        public async Task GetLogs_ReturnsAllLogs()
        {
            // Arrange
            _loggingService.LogInformation("Info 1");
            _loggingService.LogWarning("Warning 1");
            _loggingService.LogError("Error 1");

            // Act
            var logs = await _loggingService.GetLogsAsync();
            var logEntries = JsonSerializer.Deserialize<LogEntry[]>(logs);

            // Assert
            Assert.IsNotNull(logEntries);
            Assert.AreEqual(3, logEntries!.Length);
            Assert.IsTrue(logEntries.Any(l => l.Level == "INFO"));
            Assert.IsTrue(logEntries.Any(l => l.Level == "WARNING"));
            Assert.IsTrue(logEntries.Any(l => l.Level == "ERROR"));
        }

        [TestMethod]
        public async Task ClearLogs_RemovesAllLogs()
        {
            // Arrange
            _loggingService.LogInformation("Test message");

            // Act
            await _loggingService.ClearLogsAsync();
            var logs = await _loggingService.GetLogsAsync();
            var logEntries = JsonSerializer.Deserialize<LogEntry[]>(logs);

            // Assert
            Assert.IsNotNull(logEntries);
            Assert.AreEqual(0, logEntries!.Length);
        }

        [TestMethod]
        public void LogRotation_KeepsMaximumNumberOfFiles()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                _loggingService.LogInformation($"Test message {i}");
            }

            // Force log rotation
            for (int i = 0; i < 8; i++)
            {
                var fileName = $"log_{DateTime.Now.AddDays(-i):yyyy-MM-dd}.json";
                File.WriteAllText(Path.Combine(_testLogPath, fileName), "{}");
            }

            // Act
            _loggingService.LogInformation("Trigger rotation");
            System.Threading.Thread.Sleep(100); // Give time for async rotation

            // Assert
            var files = Directory.GetFiles(_testLogPath, "log_*.json");
            Assert.IsTrue(files.Length <= 7);
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