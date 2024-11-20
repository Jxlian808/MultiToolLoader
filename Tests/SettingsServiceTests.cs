using Microsoft.VisualStudio.TestTools.UnitTesting;
using MultiToolLoader.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace MultiToolLoader.Tests
{
    [TestClass]
    public class SettingsServiceTests : TestBase
    {
        private SettingsService _settingsService = null!;
        private string _testSettingsPath = null!;

        [TestInitialize]
        public override async Task TestInitialize()
        {
            await base.TestInitialize();

            _testSettingsPath = Path.Combine(Path.GetTempPath(), "test_settings.json");
            _settingsService = new SettingsService(LoggingServiceMock.Object);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (File.Exists(_testSettingsPath))
            {
                File.Delete(_testSettingsPath);
            }
        }

        [TestMethod]
        public async Task SaveSettingsAsync_ValidSettings_SavesSuccessfully()
        {
            var settings = new Settings
            {
                IsDarkMode = true,
                ApiKey = "test-key",
                DefaultModelId = "mixtral"
            };

            await _settingsService.SaveSettingsAsync(settings);

            Assert.AreEqual(settings.IsDarkMode, _settingsService.CurrentSettings.IsDarkMode);
            Assert.AreEqual(settings.DefaultModelId, _settingsService.CurrentSettings.DefaultModelId);
        }

        [TestMethod]
        public async Task LoadSettingsAsync_NoExistingFile_CreatesDefaultSettings()
        {
            if (File.Exists(_testSettingsPath))
            {
                File.Delete(_testSettingsPath);
            }

            await _settingsService.LoadSettingsAsync();

            Assert.IsTrue(_settingsService.CurrentSettings.IsDarkMode);
            Assert.AreEqual("mixtral", _settingsService.CurrentSettings.DefaultModelId);
        }

        [TestMethod]
        public async Task BackupSettingsAsync_CreatesBackupFile()
        {
            await _settingsService.BackupSettingsAsync();

            var backupFiles = _settingsService.GetBackupFiles();
            Assert.IsTrue(backupFiles.Any());
        }

        [TestMethod]
        public async Task RestoreSettingsAsync_ValidBackup_RestoresSettings()
        {
            // Create initial settings
            var initialSettings = new Settings
            {
                IsDarkMode = true,
                ApiKey = "test-key",
                DefaultModelId = "mixtral"
            };
            await _settingsService.SaveSettingsAsync(initialSettings);
            await _settingsService.BackupSettingsAsync();

            // Change settings
            var newSettings = new Settings
            {
                IsDarkMode = false,
                ApiKey = "new-key",
                DefaultModelId = "llama"
            };
            await _settingsService.SaveSettingsAsync(newSettings);

            // Restore from backup
            var backupFile = _settingsService.GetBackupFiles().First();
            await _settingsService.RestoreSettingsAsync(backupFile);

            Assert.AreEqual(initialSettings.IsDarkMode, _settingsService.CurrentSettings.IsDarkMode);
            Assert.AreEqual(initialSettings.DefaultModelId, _settingsService.CurrentSettings.DefaultModelId);
        }

        [TestMethod]
        public async Task ExportImportSettingsAsync_PreservesSettings()
        {
            var originalSettings = new Settings
            {
                IsDarkMode = true,
                ApiKey = "test-key",
                DefaultModelId = "mixtral",
                CustomPrompts = new() { { "test", "prompt" } }
            };
            await _settingsService.SaveSettingsAsync(originalSettings);

            var exportedJson = await _settingsService.ExportSettingsAsync();
            await _settingsService.ImportSettingsAsync(exportedJson);

            Assert.AreEqual(originalSettings.IsDarkMode, _settingsService.CurrentSettings.IsDarkMode);
            Assert.AreEqual(originalSettings.DefaultModelId, _settingsService.CurrentSettings.DefaultModelId);
            Assert.AreEqual(originalSettings.CustomPrompts["test"], _settingsService.CurrentSettings.CustomPrompts["test"]);
        }
    }
}