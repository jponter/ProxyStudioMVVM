using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using ProxyStudio.Services;

namespace ProxyStudio.ViewModels;



public class ViewModelBase : ObservableObject
{
    protected IErrorHandlingService ErrorHandler { get; set; }
    protected ILogger Logger { get; set; }
    
    /// <summary>
    /// Handles exceptions in non-async contexts using fire-and-forget pattern
    /// </summary>
    protected void HandleSyncException(Exception ex, string userMessage, string context)
    {
        Logger?.LogError(ex, "Synchronous exception in {Context}: {Message}", context, ex.Message);
        
        if (ErrorHandler != null)
        {
            _ = Task.Run(async () => 
                await ErrorHandler.HandleExceptionAsync(ex, userMessage, context));
        }
    }
}