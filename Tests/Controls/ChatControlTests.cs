using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Threading.Tasks;
using MultiToolLoader.Controls;

namespace MultiToolLoader.Tests.Controls
{
    [TestClass]
    public class ChatControlTests : UITestBase
    {
        private ChatControl? _chatControl;

        [TestInitialize]
        public new async Task TestInitialize()
        {
            await base.TestInitialize();

            RunOnUIThread(() =>
            {
                _chatControl = new ChatControl();
                var window = new Window
                {
                    Content = _chatControl
                };
                window.Show();
            });
        }

        [TestMethod]
        public void Initialize_LoadsDefaultModel()
        {
            RunOnUIThread(() =>
            {
                Assert.IsNotNull(_chatControl);
                Assert.IsNotNull(_chatControl.SelectedModel);
                Assert.AreEqual("mixtral", _chatControl.SelectedModel.Id);
            });
        }

        [TestMethod]
        public void SendMessage_EmptyInput_ButtonDisabled()
        {
            RunOnUIThread(() =>
            {
                Assert.IsNotNull(_chatControl);
                _chatControl.MessageInput = "";
                Assert.IsFalse(_chatControl.SendMessageCommand.CanExecute(null));
            });
        }

        [TestMethod]
        public void SendMessage_ValidInput_AddsToMessages()
        {
            RunOnUIThread(async () =>
            {
                Assert.IsNotNull(_chatControl);

                _chatControl.MessageInput = "Test message";
                await Task.Delay(100); // Wait for UI update

                Assert.AreEqual(0, _chatControl.Messages.Count);

                _chatControl.SendMessageCommand.Execute(null);
                await Task.Delay(100); // Wait for message processing

                Assert.AreEqual(2, _chatControl.Messages.Count); // User message + AI response
                Assert.AreEqual("Test message", _chatControl.Messages[0].Content);
            });
        }

        [TestMethod]
        public void ModelSelection_ChangesCurrentModel()
        {
            RunOnUIThread(() =>
            {
                Assert.IsNotNull(_chatControl);
                var newModel = _chatControl.AvailableModels[1]; // Select second model

                _chatControl.SelectedModel = newModel;

                Assert.AreEqual(newModel.Id, _chatControl.SelectedModel.Id);
            });
        }

        [TestMethod]
        public void ClearChat_RemovesAllMessages()
        {
            RunOnUIThread(async () =>
            {
                Assert.IsNotNull(_chatControl);

                // Add some messages
                _chatControl.MessageInput = "Test 1";
                _chatControl.SendMessageCommand.Execute(null);
                await Task.Delay(100);

                _chatControl.MessageInput = "Test 2";
                _chatControl.SendMessageCommand.Execute(null);
                await Task.Delay(100);

                Assert.IsTrue(_chatControl.Messages.Count > 0);

                // Clear chat
                _chatControl.ClearCommand.Execute(null);

                Assert.AreEqual(0, _chatControl.Messages.Count);
            });
        }
    }
}