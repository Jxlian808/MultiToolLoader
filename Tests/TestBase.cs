using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using MultiToolLoader.Services;
using System.Threading.Tasks;

namespace MultiToolLoader.Tests
{
    [TestClass]
    public abstract class TestBase
    {
        protected Mock<ILoggingService> LoggingServiceMock;
        protected Mock<ISettingsService> SettingsServiceMock;
        protected Mock<IChatService> ChatServiceMock;
        protected Mock<IErrorHandlingService> ErrorHandlingServiceMock;

        protected TestBase()
        {
            LoggingServiceMock = new Mock<ILoggingService>();
            SettingsServiceMock = new Mock<ISettingsService>();
            ChatServiceMock = new Mock<IChatService>();
            ErrorHandlingServiceMock = new Mock<IErrorHandlingService>();
        }

        [TestInitialize]
        public virtual Task TestInitialize()
        {
            LoggingServiceMock.Reset();
            SettingsServiceMock.Reset();
            ChatServiceMock.Reset();
            ErrorHandlingServiceMock.Reset();

            return Task.CompletedTask;
        }
    }
}