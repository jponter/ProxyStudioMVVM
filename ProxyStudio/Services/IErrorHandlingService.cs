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
using System.Threading.Tasks;

namespace ProxyStudio.Services;

public enum ErrorSeverity
{
    Information,       // Just informational
    Warning,    // Something to be aware of
    Error,      // Something went wrong but recoverable
    Critical    // Serious error that might affect functionality
}

public class UserError
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public ErrorSeverity Severity { get; set; }
    public Exception? Exception { get; set; }
    public string? RecoveryAction { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? OperationContext { get; set; }
}


public interface IErrorHandlingService
{
    // Show errors to user
    Task ShowErrorAsync(string title, string message, ErrorSeverity severity = ErrorSeverity.Error, Exception? exception = null);
    Task ShowErrorAsync(UserError error);
        
    // Handle exceptions with user-friendly messages
    Task HandleExceptionAsync(Exception exception, string userFriendlyMessage, string operationContext = "");
        
    // Validation helpers
    bool ValidateAndShowError(bool condition, string errorMessage, string title = "Validation Error");
        
    // Recovery suggestions
    Task ShowRecoverableErrorAsync(string title, string message, string recoveryAction, Func<Task> recoveryCallback);
        
    // Error reporting
    Task ReportErrorAsync(Exception exception, string additionalContext = "");
        
    // Error history for debugging
    System.Collections.Generic.List<UserError> GetRecentErrors(int count = 10);
    
    // New methods for background thread error logging
    Task LogErrorAsync(UserError error);
    Task LogErrorAsync(string title, string message, ErrorSeverity severity = ErrorSeverity.Error, Exception? exception = null);
}