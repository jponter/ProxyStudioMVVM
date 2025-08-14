/*
ProxyStudio - A cross-platform proxy management application.
Copyright (C) 2025 James Ponter

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
*/


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
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
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
            
            _logger.LogInformation("ErrorHandlingService initialized with theme-integrated styled dialogs");
        }

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

        // public async Task ShowErrorAsync(UserError error)
        // {
        //     var logLevel = error.Severity switch
        //     {
        //         ErrorSeverity.Information => LogLevel.Information,
        //         ErrorSeverity.Warning => LogLevel.Warning,
        //         ErrorSeverity.Error => LogLevel.Error,
        //         ErrorSeverity.Critical => LogLevel.Critical,
        //         _ => LogLevel.Error
        //     };
        //
        //     if (error.Exception != null)
        //     {
        //         _logger.Log(logLevel, error.Exception, "USER ERROR: {Title} - {Message}", error.Title, error.Message);
        //     }
        //     else
        //     {
        //         _logger.Log(logLevel, "USER ERROR: {Title} - {Message}", error.Title, error.Message);
        //     }
        //
        //     lock (_lockObject)
        //     {
        //         _errorHistory.Add(error);
        //     }
        //
        //     await ShowCustomErrorDialogAsync(error);
        // }

        public async Task HandleExceptionAsync(Exception exception, string userFriendlyMessage, string operationContext = "")
        {
            var title = "Operation Failed";
            var severity = ErrorSeverity.Error;

            if (exception is OutOfMemoryException)
            {
                title = "Memory Error";
                severity = ErrorSeverity.Critical;
            }
            else if (exception is UnauthorizedAccessException)
            {
                title = "Access Denied";
                severity = ErrorSeverity.Warning;
            }
            else if (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                title = "File Not Found";
                severity = ErrorSeverity.Warning;
            }

            var message = userFriendlyMessage;
            var recoveryAction = GetRecoveryAction(exception);

            if (ShouldShowTechnicalDetails(exception))
            {
                message += $"\n\nTechnical details: {exception.Message}";
            }

            var error = new UserError
            {
                Title = title,
                Message = message,
                Severity = severity,
                Exception = exception,
                RecoveryAction = recoveryAction,
                OperationContext = operationContext
            };

            await ShowErrorAsync(error);
        }

        public bool ValidateAndShowError(bool condition, string errorMessage, string title = "Validation Error")
        {
            if (condition) return true;

            _ = Task.Run(async () => await ShowErrorAsync(title, errorMessage, ErrorSeverity.Warning));
            return false;
        }

        public async Task ShowRecoverableErrorAsync(string title, string message, string recoveryAction, Func<Task> recoveryCallback)
        {
            try
            {
                var shouldRecover = await ShowRecoveryDialogAsync(new UserError
                {
                    Title = title,
                    Message = message,
                    RecoveryAction = recoveryAction,
                    Severity = ErrorSeverity.Warning
                });

                if (shouldRecover && recoveryCallback != null)
                {
                    try
                    {
                        await recoveryCallback();
                        await ShowErrorAsync("Recovery Successful", "The operation completed successfully.", ErrorSeverity.Information);
                    }
                    catch (Exception ex)
                    {
                        await ShowErrorAsync("Recovery Failed", $"The recovery action failed: {ex.Message}\n\nYou may need to try a different approach.", ErrorSeverity.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show recoverable error dialog");
                await ShowErrorAsync("Error Display Failed", "Could not show the error dialog properly. You may need to try a different approach.", ErrorSeverity.Error);
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

        // ENHANCED: Custom Avalonia 11 compatible dialog with THEME INTEGRATION
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
                
                await dialog.ShowDialog((Window)topLevel);
                
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
                _logger.LogDebug("Recovery dialog result: {Title} = {Result}", title, result ? "Yes" : "No");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show confirmation dialog: {Title}", error.Title);
                return false;
            }
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            try
            {
                var result = await ShowStyledYesNoDialogAsync(title, message);
                _logger.LogDebug("Confirmation dialog result: {Title} = {Result}", title, result ? "Yes" : "No");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show confirmation dialog: {Title}", title);
                return false;
            }
        }

        // THEME-INTEGRATED: Create a properly styled error dialog using DynamicResource
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

            // MAIN CONTAINER with theme-integrated background
            var mainPanel = new Border
            {
                // Use DynamicResource instead of hardcoded color
                [!Border.BackgroundProperty] = new DynamicResourceExtension("SurfaceBrush"),
                [!Border.BorderBrushProperty] = GetSeverityDynamicBrush(severity),
                BorderThickness = new Thickness(0, 4, 0, 0), // Top accent border
                Padding = new Thickness(0)
            };

            var contentPanel = new DockPanel
            {
                Margin = new Thickness(24, 20, 24, 20)
            };

            // HEADER SECTION with themed icon and title
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Margin = new Thickness(0, 0, 0, 20)
            };

            // Icon with theme-integrated styling
            var iconBorder = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                [!Border.BackgroundProperty] = GetSeverityLightDynamicBrush(severity),
                Child = new TextBlock
                {
                    Text = GetSeverityIcon(severity),
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    [!TextBlock.ForegroundProperty] = GetSeverityDynamicBrush(severity)
                }
            };

            // Title with theme-integrated contrast
            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("TextPrimaryBrush"),
                MaxWidth = 360
            };

            headerPanel.Children.Add(iconBorder);
            headerPanel.Children.Add(titleText);

            // MESSAGE AREA with theme-integrated styling
            var messageContainer = new Border
            {
                [!Border.BackgroundProperty] = new DynamicResourceExtension("BackgroundBrush"),
                [!Border.BorderBrushProperty] = new DynamicResourceExtension("BorderBrush"),
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
                    [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("TextSecondaryBrush"),
                    Background = Brushes.Transparent
                }
            };

            messageContainer.Child = messageScroll;

            // BUTTON AREA with theme-integrated styling
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

            // Main container with theme integration
            var mainPanel = new Border
            {
                [!Border.BackgroundProperty] = new DynamicResourceExtension("SurfaceBrush"),
                [!Border.BorderBrushProperty] = new DynamicResourceExtension("InfoBrush"),
                BorderThickness = new Thickness(0, 4, 0, 0),
                Padding = new Thickness(0)
            };

            var contentPanel = new DockPanel
            {
                Margin = new Thickness(24, 20, 24, 20)
            };

            // Message with theme integration
            var messageContainer = new Border
            {
                [!Border.BackgroundProperty] = new DynamicResourceExtension("BackgroundBrush"),
                [!Border.BorderBrushProperty] = new DynamicResourceExtension("BorderBrush"),
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
                [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("TextSecondaryBrush"),
                Background = Brushes.Transparent
            };

            messageContainer.Child = messageText;

            // Buttons with theme integration
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

            await dialog.ShowDialog((Window)topLevel);
            return result;
        }

        // THEME-INTEGRATED: Create properly styled buttons using DynamicResource
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
                // Primary button styling using theme resources
                button[!Button.BackgroundProperty] = GetSeverityDynamicBrush(severity);
                button[!Button.ForegroundProperty] = new DynamicResourceExtension("BackgroundBrush"); // Contrasting text
                button[!Button.BorderBrushProperty] = GetSeverityDynamicBrush(severity);
                button.BorderThickness = new Thickness(1);

                // Add hover effect using theme hover brushes
                var hoverStyle = new Style(x => x.OfType<Button>().Class(":pointerover"));
                hoverStyle.Setters.Add(new Setter(Button.BackgroundProperty, GetSeverityHoverDynamicBrush(severity)));
                button.Styles.Add(hoverStyle);
            }
            else
            {
                // Secondary button styling using neutral theme colors
                button[!Button.BackgroundProperty] = new DynamicResourceExtension("BackgroundBrush");
                button[!Button.ForegroundProperty] = new DynamicResourceExtension("TextPrimaryBrush");
                button[!Button.BorderBrushProperty] = new DynamicResourceExtension("BorderBrush");
                button.BorderThickness = new Thickness(1);

                // Add hover effect for secondary buttons
                var hoverStyle = new Style(x => x.OfType<Button>().Class(":pointerover"));
                hoverStyle.Setters.Add(new Setter(Button.BackgroundProperty, new DynamicResourceExtension("SurfaceHoverBrush")));
                button.Styles.Add(hoverStyle);
            }

            return button;
        }

        // THEME-INTEGRATED: Helper methods for severity-based styling using DynamicResource
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

        /// <summary>
        /// Get the appropriate DynamicResource brush for severity colors
        /// Maps to your 12-color theme system's semantic colors
        /// </summary>
        private static DynamicResourceExtension GetSeverityDynamicBrush(ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorSeverity.Information => new DynamicResourceExtension("InfoBrush"),
                ErrorSeverity.Warning => new DynamicResourceExtension("WarningBrush"),
                ErrorSeverity.Error => new DynamicResourceExtension("ErrorBrush"),
                ErrorSeverity.Critical => new DynamicResourceExtension("ErrorBrush"), // Use Error for Critical
                _ => new DynamicResourceExtension("InfoBrush")
            };
        }

        /// <summary>
        /// Get the appropriate DynamicResource hover brush for severity colors
        /// Uses your auto-generated hover variants (15% darker)
        /// </summary>
        private static DynamicResourceExtension GetSeverityHoverDynamicBrush(ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorSeverity.Information => new DynamicResourceExtension("InfoHoverBrush"),
                ErrorSeverity.Warning => new DynamicResourceExtension("WarningHoverBrush"),
                ErrorSeverity.Error => new DynamicResourceExtension("ErrorHoverBrush"),
                ErrorSeverity.Critical => new DynamicResourceExtension("ErrorHoverBrush"),
                _ => new DynamicResourceExtension("InfoHoverBrush")
            };
        }

        /// <summary>
        /// Get the appropriate DynamicResource light brush for severity backgrounds
        /// Uses your auto-generated light variants (80% lighter)
        /// </summary>
        private static DynamicResourceExtension GetSeverityLightDynamicBrush(ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorSeverity.Information => new DynamicResourceExtension("InfoLightBrush"),
                ErrorSeverity.Warning => new DynamicResourceExtension("WarningLightBrush"),
                ErrorSeverity.Error => new DynamicResourceExtension("ErrorLightBrush"),
                ErrorSeverity.Critical => new DynamicResourceExtension("ErrorLightBrush"),
                _ => new DynamicResourceExtension("InfoLightBrush")
            };
        }

        // LEGACY METHODS - These remain for backwards compatibility but now use theme integration
        // These are kept exactly as they were but now return theme-integrated brushes

        private static SolidColorBrush GetSeverityAccentColor(ErrorSeverity severity)
        {
            // Fallback method - tries to resolve from theme, falls back to hardcoded if needed
            var app = Application.Current;
            
            var resourceKey = severity switch
            {
                ErrorSeverity.Information => "InfoBrush",
                ErrorSeverity.Warning => "WarningBrush", 
                ErrorSeverity.Error => "ErrorBrush",
                ErrorSeverity.Critical => "ErrorBrush",
                _ => "InfoBrush"
            };

            if (app?.Resources?.TryGetValue(resourceKey, out var brush) == true && brush is SolidColorBrush themeBrush)
            {
                return themeBrush;
            }

            // Fallback to original hardcoded colors if theme brush not found
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
            // Try to get theme light brush first
            var app = Application.Current;
            
            var resourceKey = severity switch
            {
                ErrorSeverity.Information => "InfoLightBrush",
                ErrorSeverity.Warning => "WarningLightBrush",
                ErrorSeverity.Error => "ErrorLightBrush", 
                ErrorSeverity.Critical => "ErrorLightBrush",
                _ => "InfoLightBrush"
            };

            if (app?.Resources?.TryGetValue(resourceKey, out var brush) == true && brush is SolidColorBrush themeBrush)
            {
                return themeBrush;
            }

            // Fallback to original hardcoded colors if theme brush not found
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
            // Try to get theme color first
            var app = Application.Current;
            
            var resourceKey = severity switch
            {
                ErrorSeverity.Information => "InfoBrush",
                ErrorSeverity.Warning => "WarningBrush",
                ErrorSeverity.Error => "ErrorBrush",
                ErrorSeverity.Critical => "ErrorBrush", 
                _ => "InfoBrush"
            };

            if (app?.Resources?.TryGetValue(resourceKey, out var brush) == true && brush is SolidColorBrush themeBrush)
            {
                return themeBrush;
            }

            // Fallback to original hardcoded colors if theme brush not found
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

        private TopLevel? GetTopLevelWindow()
        {
            return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
        }

        private static string GetRecoveryAction(Exception exception)
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
        
        
        /// <summary>
    /// Logs an error to the history without showing a dialog - safe for background threads
    /// </summary>
    public async Task LogErrorAsync(UserError error)
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
            _logger.Log(logLevel, error.Exception, "BACKGROUND ERROR: {Title} - {Message}", error.Title, error.Message);
        }
        else
        {
            _logger.Log(logLevel, "BACKGROUND ERROR: {Title} - {Message}", error.Title, error.Message);
        }

        lock (_lockObject)
        {
            _errorHistory.Add(error);
            if (_errorHistory.Count > 100)
            {
                _errorHistory.RemoveAt(0);
            }
        }

        // Don't show dialog for background errors
        await Task.CompletedTask;
    }

    /// <summary>
    /// Convenience method for background error logging
    /// </summary>
    public async Task LogErrorAsync(string title, string message, ErrorSeverity severity = ErrorSeverity.Error, Exception? exception = null)
    {
        var error = new UserError
        {
            Title = title,
            Message = message,
            Severity = severity,
            Exception = exception,
            Timestamp = DateTime.Now
        };

        await LogErrorAsync(error);
    }

    /// <summary>
    /// Enhanced ShowErrorAsync that safely handles UI thread dispatching
    /// </summary>
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

        // Safely show dialog on UI thread
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            // Already on UI thread
            await ShowCustomErrorDialogAsync(error);
        }
        else
        {
            // Dispatch to UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await ShowCustomErrorDialogAsync(error);
            });
        }
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