using MultiToolLoader.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace MultiToolLoader.Services
{
    public interface IErrorHandlingService
    {
        Task HandleErrorAsync(Exception ex, string context, ErrorSeverity severity = ErrorSeverity.Error);
        Task HandleErrorAsync(string message, string context, ErrorSeverity severity = ErrorSeverity.Error);
        ErrorInfo GetLastError();
        IEnumerable<ErrorInfo> GetErrorHistory();
        void ClearErrorHistory();
        event EventHandler<ErrorInfo> OnErrorOccurred;
    }

    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILoggingService _loggingService;
        private readonly ConcurrentQueue<ErrorInfo> _errorHistory;
        private readonly int _maxHistoryItems = 100;
        private ErrorInfo? _lastError;

        public event EventHandler<ErrorInfo>? OnErrorOccurred;

        public ErrorHandlingService(ILoggingService loggingService)
        {
            _loggingService = loggingService;
            _errorHistory = new ConcurrentQueue<ErrorInfo>();
        }

        public async Task HandleErrorAsync(Exception ex, string context, ErrorSeverity severity = ErrorSeverity.Error)
        {
            var errorInfo = new ErrorInfo
            {
                Timestamp = DateTime.UtcNow,
                Context = context,
                Message = GetUserFriendlyMessage(ex),
                TechnicalMessage = ex.Message,
                StackTrace = ex.StackTrace,
                Severity = severity,
                ErrorCode = GetErrorCode(ex),
                ErrorType = ex.GetType().Name
            };

            await ProcessErrorAsync(errorInfo);
        }

        public async Task HandleErrorAsync(string message, string context, ErrorSeverity severity = ErrorSeverity.Error)
        {
            var errorInfo = new ErrorInfo
            {
                Timestamp = DateTime.UtcNow,
                Context = context,
                Message = message,
                TechnicalMessage = message,
                Severity = severity,
                ErrorCode = "USER_ERROR"
            };

            await ProcessErrorAsync(errorInfo);
        }

        private async Task ProcessErrorAsync(ErrorInfo errorInfo)
        {
            // Log error
            switch (errorInfo.Severity)
            {
                case ErrorSeverity.Warning:
                    _loggingService.LogWarning($"{errorInfo.Context}: {errorInfo.TechnicalMessage}");
                    break;
                case ErrorSeverity.Error:
                    _loggingService.LogError($"{errorInfo.Context}: {errorInfo.TechnicalMessage}");
                    break;
                case ErrorSeverity.Critical:
                    _loggingService.LogError($"CRITICAL - {errorInfo.Context}: {errorInfo.TechnicalMessage}",
                                           new Exception(errorInfo.TechnicalMessage));
                    break;
            }

            // Store error
            _lastError = errorInfo;
            _errorHistory.Enqueue(errorInfo);
            while (_errorHistory.Count > _maxHistoryItems)
            {
                _errorHistory.TryDequeue(out _);
            }

            // Notify subscribers
            OnErrorOccurred?.Invoke(this, errorInfo);

            // Show UI message if needed
            await ShowErrorMessageAsync(errorInfo);
        }

        private async Task ShowErrorMessageAsync(ErrorInfo errorInfo)
        {
            if (errorInfo.Severity >= ErrorSeverity.Error)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    string title = errorInfo.Severity == ErrorSeverity.Critical ?
                        "Kritischer Fehler" : "Fehler";

                    var message = $"{errorInfo.Message}\n\nFehlercode: {errorInfo.ErrorCode}";

                    if (errorInfo.Severity == ErrorSeverity.Critical)
                    {
                        message += "\n\nBitte starten Sie die Anwendung neu.";
                    }

                    CustomMessageBox.Show(title, message);
                });
            }
        }

        private string GetUserFriendlyMessage(Exception ex) => ex switch
        {
            HttpRequestException => "Es konnte keine Verbindung zum Server hergestellt werden. " +
                                  "Bitte überprüfen Sie Ihre Internetverbindung.",

            UnauthorizedAccessException => "Sie haben keine Berechtigung für diese Aktion. " +
                                         "Bitte überprüfen Sie Ihre Zugangsdaten.",

            TaskCanceledException => "Die Anfrage wurde wegen Zeitüberschreitung abgebrochen. " +
                                   "Bitte versuchen Sie es später erneut.",

            ArgumentException => "Ungültige Eingabe. Bitte überprüfen Sie Ihre Eingaben.",

            InvalidOperationException => "Die Aktion konnte nicht ausgeführt werden. " +
                                       "Bitte versuchen Sie es erneut.",

            _ => "Ein unerwarteter Fehler ist aufgetreten. " +
                 "Bitte versuchen Sie es erneut oder kontaktieren Sie den Support."
        };

        private string GetErrorCode(Exception ex)
        {
            var baseCode = ex switch
            {
                HttpRequestException => "NET",
                UnauthorizedAccessException => "AUTH",
                TaskCanceledException => "TIMEOUT",
                ArgumentException => "INPUT",
                InvalidOperationException => "OPERATION",
                _ => "SYSTEM"
            };

            return $"{baseCode}_{DateTime.UtcNow.Ticks % 10000:0000}";
        }

        public ErrorInfo GetLastError() => _lastError ?? new ErrorInfo();

        public IEnumerable<ErrorInfo> GetErrorHistory() => _errorHistory.ToList();

        public void ClearErrorHistory() => _errorHistory.Clear();
    }

    public class ErrorInfo
    {
        public DateTime Timestamp { get; set; }
        public string Context { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TechnicalMessage { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public ErrorSeverity Severity { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;

        public override string ToString() =>
            $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Severity} - {Context}: {Message} (Code: {ErrorCode})";
    }

    public enum ErrorSeverity
    {
        Warning,
        Error,
        Critical
    }
}