using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MultiToolLoader.Controls;
using MultiToolLoader.Services;
using MultiToolLoader.Helpers;

namespace MultiToolLoader
{
    public partial class MainWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ILoggingService _loggingService;
        private UserControl? _currentView;
        private bool _isMaximized;

        private readonly ChatControl _chatControl;
        private readonly SettingsControl _settingsControl;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            _settingsService = App.SettingsService;
            _errorHandlingService = App.ErrorHandlingService;
            _loggingService = App.LoggingService;

            // Initialize controls
            _chatControl = new ChatControl();
            _settingsControl = new SettingsControl();

            // Set initial view
            ShowDashboard();

            // Subscribe to events
            _settingsService.SettingsChanged += SettingsService_SettingsChanged;
            _errorHandlingService.OnErrorOccurred += ErrorHandlingService_OnErrorOccurred;

            // Initialize window state
            UpdateMaximizeRestoreButton();

            _loggingService.LogInformation("MainWindow initialized");
        }

        private void SettingsService_SettingsChanged(object? sender, Settings e)
        {
            try
            {
                ApplySettings(e);
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleErrorAsync(ex, "Settings Application");
            }
        }

        private void ErrorHandlingService_OnErrorOccurred(object? sender, ErrorInfo e)
        {
            UpdateStatusBar(e.Message, true);
        }

        private void ApplySettings(Settings settings)
        {
            // Apply theme
            if (settings.IsDarkMode)
            {
                MainBorder.Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(30, 27, 75), 0),
                        new GradientStop(Color.FromRgb(49, 46, 129), 1)
                    }
                };
            }
            else
            {
                MainBorder.Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(249, 250, 251), 0),
                        new GradientStop(Color.FromRgb(243, 244, 246), 1)
                    }
                };
            }
        }

        private void UpdateStatusBar(string message, bool isError = false)
        {
            StatusText.Text = message;
            StatusText.Foreground = new SolidColorBrush(
                isError ? Color.FromRgb(239, 68, 68) : Color.FromRgb(176, 176, 176));
        }

        private void MenuButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton button)
            {
                switch (button.Name)
                {
                    case "DashboardButton":
                        ShowDashboard();
                        break;
                    case "ChatButton":
                        ShowView(_chatControl, "Chat");
                        break;
                    case "SettingsButton":
                        ShowView(_settingsControl, "Einstellungen");
                        break;
                }
            }
        }

        private void ShowDashboard()
        {
            var dashboard = new TextBlock
            {
                Text = "Welcome to Dashboard",
                FontSize = 24,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ShowView(dashboard, "Dashboard");
        }

        private async void ShowView(UIElement newView, string context)
        {
            try
            {
                if (_currentView != null)
                {
                    await AnimateViewTransition(newView);
                }
                else
                {
                    MainContent.Content = newView;
                }

                _currentView = newView as UserControl;
                UpdateStatusBar($"Aktiver Bereich: {context}");
                _loggingService.LogInformation($"View changed to {context}");
            }
            catch (Exception ex)
            {
                await _errorHandlingService.HandleErrorAsync(ex, "View Transition");
            }
        }

        private async Task AnimateViewTransition(UIElement newView)
        {
            var oldView = MainContent.Content as UIElement;
            if (oldView == null) return;

            // Fade out current view
            await Task.WhenAll(
                AnimateHelper.FadeOut(oldView, 0.2),
                AnimateHelper.SlideOut(oldView, -50)
            );

            MainContent.Content = newView;

            // Fade in new view
            await Task.WhenAll(
                AnimateHelper.FadeIn(newView, 0.2),
                AnimateHelper.SlideIn(newView, 50)
            );
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            _isMaximized = !_isMaximized;
            WindowState = _isMaximized ? WindowState.Maximized : WindowState.Normal;
            UpdateMaximizeRestoreButton();
        }

        private void UpdateMaximizeRestoreButton()
        {
            if (_isMaximized)
            {
                MaximizeIcon.Data = Geometry.Parse(
                    "M 3,3 H 13 V 13 H 3 Z M 1,9 H 3 M 5,1 V 3 M 1,5 H 3");
            }
            else
            {
                MaximizeIcon.Data = Geometry.Parse(
                    "M 0,0 H 10 V 10 H 0 Z");
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save any pending changes
                await _settingsService.SaveSettingsAsync(_settingsService.CurrentSettings);

                // Animate window close
                await AnimateWindowClose();

                // Log application exit
                _loggingService.LogInformation("Application closed normally");

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                await _errorHandlingService.HandleErrorAsync(ex, "Application Close");
                Application.Current.Shutdown();
            }
        }

        private async Task AnimateWindowClose()
        {
            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var scaleTransform = new ScaleTransform(1, 1);
            this.RenderTransform = scaleTransform;

            var scaleAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0.8,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var tcs = new TaskCompletionSource<bool>();
            animation.Completed += (s, e) => tcs.SetResult(true);

            this.BeginAnimation(OpacityProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

            await tcs.Task;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            AnimateWindowOpen();
        }

        private void AnimateWindowOpen()
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var scaleTransform = new ScaleTransform(0.8, 0.8);
            this.RenderTransform = scaleTransform;

            var scaleAnimation = new DoubleAnimation
            {
                From = 0.8,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            this.BeginAnimation(OpacityProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }
    }
}