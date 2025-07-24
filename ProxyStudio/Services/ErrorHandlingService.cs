// Updated ErrorHandlingService.cs with proper styling and visibility

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.Logging;

namespace ProxyStudio.Services
{
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger<ErrorHandlingService> _logger;
        private readonly List<UserError> _errorHistory;
        private readonly object _lockObject = new();

        public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
        {
            _logger = logger;
            _errorHistory = new List<UserError>();
            
            _logger.LogInformation("ErrorHandlingService initialized with enhanced styled dialogs");
        }

        // ... (other methods remain the same until ShowCustomErrorDialogAsync) ...

        public async Task ShowErrorAsync(string title, string message, ErrorSeverity severity = ErrorSeverity.Error, Exception? exception = null)
        {
            var error = new UserError
            {
                Title = title,
                Message = message,
                Severity = severity,
                Exception = exception
            };

            await ShowErrorAsync(error);
        }

        public async Task ShowErrorAsync(UserError error)
        {
            var logLevel = error.Severity switch
            {
                ErrorSeverity.Information => LogLevel.Information,
                ErrorSeverity.Warning => LogLevel.Warning,
                ErrorSeverity.Error => LogLevel.Error,
                ErrorSeverity.Critical => LogLevel.Critical,
                _ => LogLevel.Error
            };

            if (error.Exception != null)
            {
                _logger.Log(logLevel, error.Exception, "USER ERROR: {Title} - {Message}", error.Title, error.Message);
            }
            else
            {
                _logger.Log(logLevel, "USER ERROR: {Title} - {Message}", error.Title, error.Message);
            }

            lock (_lockObject)
            {
                _errorHistory.Add(error);
                if (_errorHistory.Count > 100)
                {
                    _errorHistory.RemoveAt(0);
                }
            }

            await ShowCustomErrorDialogAsync(error);
        }

        public async Task HandleExceptionAsync(Exception exception, string userFriendlyMessage, string operationContext = "")
        {
            var title = GetFriendlyErrorTitle(exception);
            var message = userFriendlyMessage;

            if (!string.IsNullOrEmpty(operationContext))
            {
                message = $"Operation: {operationContext}\n\n{message}";
            }

            if (ShouldShowTechnicalDetails(exception))
            {
                message += $"\n\nTechnical details: {exception.Message}";
            }

            var error = new UserError
            {
                Title = title,
                Message = message,
                Severity = GetErrorSeverity(exception),
                Exception = exception,
                OperationContext = operationContext,
                RecoveryAction = GetRecoveryAction(exception)
            };

            await ShowErrorAsync(error);
        }

        public bool ValidateAndShowError(bool condition, string errorMessage, string title = "Validation Error")
        {
            if (!condition)
            {
                _ = Task.Run(async () => await ShowErrorAsync(title, errorMessage, ErrorSeverity.Warning));
                return false;
            }
            return true;
        }

        public async Task ShowRecoverableErrorAsync(string title, string message, string recoveryAction, Func<Task> recoveryCallback)
        {
            var error = new UserError
            {
                Title = title,
                Message = message,
                Severity = ErrorSeverity.Error,
                RecoveryAction = recoveryAction
            };

            var shouldRetry = await ShowRecoveryDialogAsync(error);
            
            if (shouldRetry)
            {
                try
                {
                    _logger.LogInformation("User chose to retry operation: {RecoveryAction}", recoveryAction);
                    await recoveryCallback();
                    await ShowErrorAsync("Recovery Successful", "The operation has been completed successfully.", ErrorSeverity.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Recovery action failed: {RecoveryAction}", recoveryAction);
                    await HandleExceptionAsync(ex, "The recovery action failed. You may need to try a different approach.", $"Recovery: {recoveryAction}");
                }
            }
        }

        public async Task ReportErrorAsync(Exception exception, string additionalContext = "")
        {
            _logger.LogCritical(exception, "CRITICAL ERROR REPORT: {Context}", additionalContext);
            
            var message = "An unexpected error occurred and has been logged for analysis.";
            
            if (!string.IsNullOrEmpty(additionalContext))
            {
                message += $"\n\nContext: {additionalContext}";
            }

            message += $"\n\nError: {exception.Message}";

            await ShowErrorAsync("Critical Error Reported", message, ErrorSeverity.Critical, exception);
        }

        public List<UserError> GetRecentErrors(int count = 10)
        {
            lock (_lockObject)
            {
                return _errorHistory.TakeLast(count).ToList();
            }
        }

        // ENHANCED: Custom Avalonia 11 compatible dialog with proper styling
        private async Task ShowCustomErrorDialogAsync(UserError error)
        {
            try
            {
                var topLevel = GetTopLevelWindow();
                if (topLevel == null) 
                {
                    _logger.LogWarning("Cannot show error dialog - no main window available");
                    return;
                }

                var title = error.Title;
                var message = error.Message;

                if (!string.IsNullOrEmpty(error.RecoveryAction))
                {
                    message += $"\n\n💡 Suggestion: {error.RecoveryAction}";
                }

                var dialog = CreateStyledErrorDialog(title, message, error.Severity);
                
                _logger.LogDebug("Showing styled error dialog: {Title}", title);
                
                await dialog.ShowDialog(topLevel);
                
                _logger.LogDebug("Error dialog closed: {Title}", title);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "CRITICAL: Failed to show error dialog for: {ErrorTitle}", error.Title);
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR DISPLAY FAILURE: {error.Title} - {error.Message}");
            }
        }

        private async Task<bool> ShowRecoveryDialogAsync(UserError error)
        {
            try
            {
                var topLevel = GetTopLevelWindow();
                if (topLevel == null) 
                {
                    _logger.LogWarning("Cannot show recovery dialog - no main window available");
                    return false;
                }

                var title = $"🔧 {error.Title}";
                var message = $"{error.Message}\n\n🚀 Would you like to try: {error.RecoveryAction}?";
                
                var result = await ShowStyledYesNoDialogAsync(title, message);
                
                _logger.LogDebug("Recovery dialog shown: {Title}, User chose: {Result}", error.Title, result ? "Yes" : "No");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show recovery dialog for: {ErrorTitle}", error.Title);
                return false;
            }
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
        {
            try
            {
                var result = await ShowStyledYesNoDialogAsync(title, message);
                _logger.LogDebug("Confirmation dialog: {Title}, Result: {Result}", title, result ? "Yes" : "No");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show confirmation dialog: {Title}", title);
                return false;
            }
        }

        // ENHANCED: Create a properly styled error dialog with good contrast
        private Window CreateStyledErrorDialog(string title, string message, ErrorSeverity severity)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 500,
                Height = 300,
                MinWidth = 400,
                MinHeight = 250,
                MaxWidth = 800,
                MaxHeight = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = true,
                SystemDecorations = SystemDecorations.Full
            };

            // MAIN CONTAINER with proper background
            var mainPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)), // Light gray background
                BorderBrush = GetSeverityAccentColor(severity),
                BorderThickness = new Thickness(0, 4, 0, 0), // Top accent border
                Padding = new Thickness(0)
            };

            var contentPanel = new DockPanel
            {
                Margin = new Thickness(24, 20, 24, 20)
            };

            // HEADER SECTION with icon and title
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Margin = new Thickness(0, 0, 0, 20)
            };

            // Icon with proper styling
            var iconBorder = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                Background = GetSeverityBackgroundColor(severity),
                Child = new TextBlock
                {
                    Text = GetSeverityIcon(severity),
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = GetSeverityIconColor(severity)
                }
            };

            // Title with proper contrast
            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 37, 41)), // Dark text
                MaxWidth = 360
            };

            headerPanel.Children.Add(iconBorder);
            headerPanel.Children.Add(titleText);

            // MESSAGE AREA with proper scrolling and contrast
            var messageContainer = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var messageScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 200,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    LineHeight = 22,
                    Foreground = new SolidColorBrush(Color.FromRgb(73, 80, 87)), // Medium dark text
                    Background = Brushes.Transparent
                }
            };

            messageContainer.Child = messageScroll;

            // BUTTON AREA with proper styling
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12
            };

            var okButton = CreateStyledButton("OK", true, severity);
            okButton.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(okButton);

            // LAYOUT
            DockPanel.SetDock(headerPanel, Dock.Top);
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            
            contentPanel.Children.Add(headerPanel);
            contentPanel.Children.Add(buttonPanel);
            contentPanel.Children.Add(messageContainer);

            mainPanel.Child = contentPanel;
            dialog.Content = mainPanel;

            return dialog;
        }

        private async Task<bool> ShowStyledYesNoDialogAsync(string title, string message)
        {
            var topLevel = GetTopLevelWindow();
            if (topLevel == null) return false;

            var result = false;
            var dialog = new Window
            {
                Title = title,
                Width = 450,
                Height = 250,
                MinWidth = 400,
                MinHeight = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                SystemDecorations = SystemDecorations.Full
            };

            // Main container with proper styling
            var mainPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 144, 220)),
                BorderThickness = new Thickness(0, 4, 0, 0),
                Padding = new Thickness(0)
            };

            var contentPanel = new DockPanel
            {
                Margin = new Thickness(24, 20, 24, 20)
            };

            // Message with proper styling
            var messageContainer = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                LineHeight = 22,
                Foreground = new SolidColorBrush(Color.FromRgb(73, 80, 87)),
                Background = Brushes.Transparent
            };

            messageContainer.Child = messageText;

            // Buttons with proper styling
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12
            };

            var yesButton = CreateStyledButton("Yes", true, ErrorSeverity.Information);
            var noButton = CreateStyledButton("No", false, ErrorSeverity.Warning);

            yesButton.Click += (s, e) =>
            {
                result = true;
                dialog.Close();
            };

            noButton.Click += (s, e) =>
            {
                result = false;
                dialog.Close();
            };

            buttonPanel.Children.Add(noButton);
            buttonPanel.Children.Add(yesButton);

            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            contentPanel.Children.Add(messageContainer);
            contentPanel.Children.Add(buttonPanel);

            mainPanel.Child = contentPanel;
            dialog.Content = mainPanel;

            await dialog.ShowDialog(topLevel);
            return result;
        }

        // ENHANCED: Create properly styled buttons with good contrast
        private Button CreateStyledButton(string text, bool isPrimary, ErrorSeverity severity)
        {
            var button = new Button
            {
                Content = text,
                Width = 90,
                Height = 36,
                FontSize = 14,
                FontWeight = FontWeight.Medium,
                CornerRadius = new CornerRadius(6),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };

            if (isPrimary)
            {
                // Primary button styling based on severity
                var (bg, hover, pressed, text_color) = severity switch
                {
                    ErrorSeverity.Information => (
                        Color.FromRgb(52, 144, 220),      // Blue
                        Color.FromRgb(41, 121, 193),      // Darker blue
                        Color.FromRgb(31, 100, 161),      // Even darker
                        Colors.White
                    ),
                    ErrorSeverity.Warning => (
                        Color.FromRgb(255, 193, 7),       // Amber
                        Color.FromRgb(255, 179, 0),       // Darker amber
                        Color.FromRgb(212, 148, 0),       // Even darker
                        Color.FromRgb(33, 37, 41)         // Dark text on light background
                    ),
                    ErrorSeverity.Error => (
                        Color.FromRgb(220, 53, 69),       // Red
                        Color.FromRgb(200, 35, 51),       // Darker red
                        Color.FromRgb(176, 23, 39),       // Even darker
                        Colors.White
                    ),
                    ErrorSeverity.Critical => (
                        Color.FromRgb(134, 30, 48),       // Dark red
                        Color.FromRgb(114, 20, 38),       // Darker
                        Color.FromRgb(94, 10, 28),        // Even darker
                        Colors.White
                    ),
                    _ => (
                        Color.FromRgb(52, 144, 220),      // Default blue
                        Color.FromRgb(41, 121, 193),
                        Color.FromRgb(31, 100, 161),
                        Colors.White
                    )
                };

                button.Background = new SolidColorBrush(bg);
                button.Foreground = new SolidColorBrush(text_color);
                button.BorderBrush = new SolidColorBrush(bg);
                button.BorderThickness = new Thickness(1);

                // Add hover effects (this is simplified - you might want to use styles for full hover support)
            }
            else
            {
                // Secondary button styling
                button.Background = new SolidColorBrush(Colors.White);
                button.Foreground = new SolidColorBrush(Color.FromRgb(73, 80, 87));
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230));
                button.BorderThickness = new Thickness(1);
            }

            return button;
        }

        // Helper methods for severity-based styling
        private static string GetSeverityIcon(ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorSeverity.Information => "ℹ",
                ErrorSeverity.Warning => "⚠",
                ErrorSeverity.Error => "✕",
                ErrorSeverity.Critical => "⛔",
                _ => "ℹ"
            };
        }

        private static SolidColorBrush GetSeverityAccentColor(ErrorSeverity severity)
        {
            var color = severity switch
            {
                ErrorSeverity.Information => Color.FromRgb(52, 144, 220),      // Blue
                ErrorSeverity.Warning => Color.FromRgb(255, 193, 7),    // Amber
                ErrorSeverity.Error => Color.FromRgb(220, 53, 69),      // Red
                ErrorSeverity.Critical => Color.FromRgb(134, 30, 48),   // Dark red
                _ => Color.FromRgb(52, 144, 220)
            };
            return new SolidColorBrush(color);
        }

        private static SolidColorBrush GetSeverityBackgroundColor(ErrorSeverity severity)
        {
            var color = severity switch
            {
                ErrorSeverity.Information => Color.FromRgb(207, 232, 252),      // Light blue
                ErrorSeverity.Warning => Color.FromRgb(255, 248, 204),   // Light amber
                ErrorSeverity.Error => Color.FromRgb(253, 218, 221),     // Light red
                ErrorSeverity.Critical => Color.FromRgb(241, 208, 213),  // Light dark red
                _ => Color.FromRgb(207, 232, 252)
            };
            return new SolidColorBrush(color);
        }

        private static SolidColorBrush GetSeverityIconColor(ErrorSeverity severity)
        {
            var color = severity switch
            {
                ErrorSeverity.Information => Color.FromRgb(31, 100, 161),       // Dark blue
                ErrorSeverity.Warning => Color.FromRgb(161, 109, 0),     // Dark amber
                ErrorSeverity.Error => Color.FromRgb(157, 30, 43),       // Dark red
                ErrorSeverity.Critical => Color.FromRgb(94, 10, 28),     // Very dark red
                _ => Color.FromRgb(31, 100, 161)
            };
            return new SolidColorBrush(color);
        }

        private Window? GetTopLevelWindow()
        {
            try
            {
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return desktop.MainWindow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top level window");
            }
            return null;
        }

        // Helper methods (same as before)
        private static string GetFriendlyErrorTitle(Exception exception)
        {
            return exception switch
            {
                FileNotFoundException => "File Not Found",
                DirectoryNotFoundException => "Folder Not Found", 
                UnauthorizedAccessException => "Access Denied",
                OutOfMemoryException => "Insufficient Memory",
                InvalidOperationException => "Operation Failed",
                ArgumentException => "Invalid Input",
                HttpRequestException => "Network Error",
                TimeoutException => "Operation Timed Out",
                NotSupportedException => "Feature Not Supported",
                _ => "Unexpected Error"
            };
        }

        private static ErrorSeverity GetErrorSeverity(Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => ErrorSeverity.Critical,
                UnauthorizedAccessException => ErrorSeverity.Critical,
                FileNotFoundException => ErrorSeverity.Error,
                DirectoryNotFoundException => ErrorSeverity.Error,
                HttpRequestException => ErrorSeverity.Warning,
                TimeoutException => ErrorSeverity.Warning,
                ArgumentException => ErrorSeverity.Warning,
                NotSupportedException => ErrorSeverity.Warning,
                _ => ErrorSeverity.Error
            };
        }

        private static string? GetRecoveryAction(Exception exception)
        {
            return exception switch
            {
                FileNotFoundException => "Check that the file exists and try selecting it again",
                DirectoryNotFoundException => "Verify the folder path exists or create it",
                UnauthorizedAccessException => "Run as administrator or check file permissions",
                OutOfMemoryException => "Close other applications to free memory",
                HttpRequestException => "Check your internet connection and try again",
                TimeoutException => "Try again or check your network connection", 
                ArgumentException => "Verify your input values are correct",
                NotSupportedException => "Try a different approach or update the application",
                _ => "Try the operation again or restart the application"
            };
        }

        private static bool ShouldShowTechnicalDetails(Exception exception)
        {
            return exception is not (
                FileNotFoundException or 
                DirectoryNotFoundException or 
                UnauthorizedAccessException or
                ArgumentException
            );
        }
    }

    // Extension method for confirmation dialogs
    public static class ErrorHandlingServiceExtensions
    {
        public static async Task<bool> ConfirmActionAsync(this IErrorHandlingService errorHandler, 
            string title, string message, string actionName = "continue")
        {
            if (errorHandler is ErrorHandlingService service)
            {
                return await service.ShowConfirmationAsync(title, message);
            }
            
            return true;
        }
    }
}