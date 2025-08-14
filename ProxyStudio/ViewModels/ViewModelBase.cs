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