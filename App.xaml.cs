using System.Windows;
using MultiToolLoader.Services;

namespace MultiToolLoader
{
    public partial class App : Application
    {
        private static ISettingsService? _settingsService;
        private static ILoggingService? _loggingService;
        private static IChatService? _chatService;

        public static ISettingsService SettingsService => _settingsService ??= new SettingsService(LoggingService);
        public static ILoggingService LoggingService => _loggingService ??= new LoggingService();
        public static IChatService ChatService => _chatService ??= new ChatService(SettingsService, LoggingService);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize services
            _loggingService = new LoggingService();
            _settingsService = new SettingsService(_loggingService);
            _chatService = new ChatService(_settingsService, _loggingService);

            // Load settings
            _settingsService.LoadSettingsAsync().ConfigureAwait(false);

            // Show splash screen
            var splashScreen = new SplashScreen();
            splashScreen.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggingService.LogInformation("Application shutting down");
            base.OnExit(e);
        }
    }
}