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
}