using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using MultiToolLoader.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MultiToolLoader.Tests
{
    [TestClass]
    public class ErrorHandlingServiceTests : TestBase
    {
        private ErrorHandlingService _errorHandlingService = null!;
        private bool _errorEventRaised;
        private ErrorInfo? _lastEventError;

        [TestInitialize]
        public override async Task TestInitialize()
        {
            await base.TestInitialize();
            _errorHandlingService = new ErrorHandlingService(LoggingServiceMock.Object);
            _errorEventRaised = false;
            _lastEventError = null;

            _errorHandlingService.OnErrorOccurred += (s, e) =>
            {
                _errorEventRaised = true;
                _lastEventError = e;
            };
        }

        [TestMethod]
        public async Task HandleErrorAsync_Exception_LogsAndStoresError()
        {
            // Arrange
            var exception = new Exception("Test error");

            // Act
            await _errorHandlingService.HandleErrorAsync(exception, "TestContext");

            // Assert
            LoggingServiceMock.Verify(x => x.LogError(It.IsAny<string>()), Times.Once);
            Assert.IsTrue(_errorEventRaised);
            Assert.AreEqual("TestContext", _errorHandlingService.GetLastError().Context);
            Assert.IsTrue(_errorHandlingService.GetErrorHistory().Any());
        }

        [TestMethod]
        public async Task HandleErrorAsync_HttpException_ProvidesUserFriendlyMessage()
        {
            // Arrange
            var exception = new HttpRequestException("Connection failed");

            // Act
            await _errorHandlingService.HandleErrorAsync(exception, "NetworkContext");

            // Assert
            var lastError = _errorHandlingService.GetLastError();
            Assert.IsTrue(lastError.Message.Contains("Verbindung"));
            Assert.AreEqual("NET", lastError.ErrorCode.Split('_')[0]);
        }

        [TestMethod]
        public async Task HandleErrorAsync_CriticalError_LogsWithHigherSeverity()
        {
            // Arrange
            var exception = new Exception("Critical error");

            // Act
            await _errorHandlingService.HandleErrorAsync(exception, "CriticalContext", ErrorSeverity.Critical);

            // Assert
            var lastError = _errorHandlingService.GetLastError();
            Assert.AreEqual(ErrorSeverity.Critical, lastError.Severity);
            LoggingServiceMock.Verify(x => x.LogError(
                It.Is<string>(msg => msg.StartsWith("CRITICAL")),
                It.IsAny<Exception>()),
                Times.Once);
        }

        [TestMethod]
        public async Task HandleErrorAsync_Warning_DoesNotTriggerUIMessage()
        {
            // Arrange
            var message = "Warning message";

            // Act
            await _errorHandlingService.HandleErrorAsync(message, "WarningContext", ErrorSeverity.Warning);

            // Assert
            LoggingServiceMock.Verify(x => x.LogWarning(It.IsAny<string>()), Times.Once);
            var lastError = _errorHandlingService.GetLastError();
            Assert.AreEqual(ErrorSeverity.Warning, lastError.Severity);
        }

        [TestMethod]
        public void ErrorHistory_RespectsMaxItems()
        {
            // Arrange
            const int maxItems = 100;

            // Act
            for (int i = 0; i < maxItems + 50; i++)
            {
                _errorHandlingService.HandleErrorAsync(
                    $"Test message {i}",
                    "TestContext").Wait();
            }

            // Assert
            Assert.AreEqual(maxItems, _errorHandlingService.GetErrorHistory().Count());
        }

        [TestMethod]
        public async Task ClearErrorHistory_RemovesAllErrors()
        {
            // Arrange
            await _errorHandlingService.HandleErrorAsync("Test error 1", "Context1");
            await _errorHandlingService.HandleErrorAsync("Test error 2", "Context2");

            // Act
            _errorHandlingService.ClearErrorHistory();

            // Assert
            Assert.IsFalse(_errorHandlingService.GetErrorHistory().Any());
        }

        [TestMethod]
        public async Task ErrorInfo_ToString_FormatsCorrectly()
        {
            // Arrange
            await _errorHandlingService.HandleErrorAsync("Test message", "TestContext");

            // Act
            var errorString = _errorHandlingService.GetLastError().ToString();

            // Assert
            Assert.IsTrue(errorString.Contains("TestContext"));
            Assert.IsTrue(errorString.Contains("Test message"));
            Assert.IsTrue(errorString.Contains("Code:"));
        }
    }
}