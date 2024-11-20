using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using MultiToolLoader.Models;
using MultiToolLoader.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;

namespace MultiToolLoader.Tests
{
    [TestClass]
    public class ChatServiceTests : TestBase
    {
        private ChatService _chatService = null!;
        private Settings _defaultSettings;

        [TestInitialize]
        public override async Task TestInitialize()
        {
            await base.TestInitialize();

            _defaultSettings = new Settings
            {
                ApiKey = "test-key",
                DefaultModelId = "mixtral",
                Temperature = 0.7,
                MaxTokens = 1024
            };

            SettingsServiceMock.Setup(x => x.CurrentSettings).Returns(_defaultSettings);

            _chatService = new ChatService(SettingsServiceMock.Object, LoggingServiceMock.Object);
        }

        [TestMethod]
        public void Constructor_InitializesWithDefaultModel()
        {
            Assert.AreEqual("Mixtral-8x7B", _chatService.CurrentModel);
        }

        [TestMethod]
        public void SetModel_ValidModel_ChangesCurrentModel()
        {
            _chatService.SetModel("llama");
            Assert.AreEqual("Llama-2-70b", _chatService.CurrentModel);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SetModel_InvalidModel_ThrowsException()
        {
            _chatService.SetModel("invalid-model");
        }

        [TestMethod]
        public async Task GetResponseAsync_ValidInput_ReturnsResponse()
        {
            var message = "Test message";
            var response = await _chatService.GetResponseAsync(message);

            Assert.IsNotNull(response);
            LoggingServiceMock.Verify(x => x.LogInformation(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task GetResponseAsync_ApiError_HandlesErrorGracefully()
        {
            var message = "Test message";

            // Simulate API error
            _defaultSettings.ApiKey = "invalid-key";

            var response = await _chatService.GetResponseAsync(message);

            Assert.IsTrue(response.Contains("Fehler"));
            LoggingServiceMock.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task SaveChatHistoryAsync_ValidInput_SavesHistory()
        {
            var conversationId = "test-conv";
            var messages = new List<ChatMessage>
            {
                new() { Content = "Test", IsUser = true, Timestamp = DateTime.Now }
            };

            await _chatService.SaveChatHistoryAsync(conversationId, messages);

            SettingsServiceMock.Verify(x => x.SaveSettingsAsync(It.IsAny<Settings>()), Times.Once);
        }

        [TestMethod]
        public async Task GetChatHistoryAsync_ExistingConversation_ReturnsHistory()
        {
            var conversationId = "test-conv";
            var messages = new List<ChatMessage>
            {
                new() { Content = "Test", IsUser = true, Timestamp = DateTime.Now }
            };

            _defaultSettings.ChatHistory[conversationId] = messages;

            var result = await _chatService.GetChatHistoryAsync(conversationId);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Test", result[0].Content);
        }

        [TestMethod]
        public async Task GetCustomPromptsAsync_ReturnsPrompts()
        {
            _defaultSettings.CustomPrompts["test"] = "Test prompt";

            var prompts = await _chatService.GetCustomPromptsAsync();

            Assert.IsTrue(((IEnumerable<string>)prompts).Contains("test"));
        }
    }
}