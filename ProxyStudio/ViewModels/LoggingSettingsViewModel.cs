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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;
using ProxyStudio.Services;
using Serilog;
using Serilog.Events;

namespace ProxyStudio.ViewModels
{
    public partial class LoggingSettingsViewModel : ViewModelBase
    {
        private readonly IConfigManager _configManager;
        private readonly ILogger<LoggingSettingsViewModel> _logger;
        private readonly IErrorHandlingService _errorHandler;

        // Settings properties
        [ObservableProperty] private int _selectedLogLevel;
        [ObservableProperty] private int _logRetentionDays;
        [ObservableProperty] private int _maxLogFileSizeMB;
        [ObservableProperty] private bool _enablePerformanceLogging;
        [ObservableProperty] private bool _enableConsoleOutput;
        [ObservableProperty] private bool _includeStackTraces;
        [ObservableProperty] private string _currentLogDirectory = "";
        [ObservableProperty] private string _currentLogFile = "";
        [ObservableProperty] private long _currentLogFileSize;
        [ObservableProperty] private ObservableCollection<UserError> _recentErrors = new();

        // Available options
        public List<LogLevelOption> LogLevels { get; } = new()
        {
            new() { Level = 0, Name = "Trace", Description = "Very detailed logging (everything)" },
            new() { Level = 1, Name = "Debug", Description = "Development debugging information" },
            new() { Level = 2, Name = "Info", Description = "General information (recommended)" },
            new() { Level = 3, Name = "Warning", Description = "Warning messages only" },
            new() { Level = 4, Name = "Error", Description = "Error messages only" },
            new() { Level = 5, Name = "Critical", Description = "Critical errors only" }
        };

        public LoggingSettingsViewModel(IConfigManager configManager, ILogger<LoggingSettingsViewModel> logger, IErrorHandlingService errorHandler)
        {
            _configManager = configManager;
            _logger = logger;
            _errorHandler = errorHandler;

            LoadSettings();
            LoadCurrentStatus();
            LoadRecentErrors();
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _configManager.Config.LoggingSettings;
                
                SelectedLogLevel = settings.MinimumLogLevel;
                LogRetentionDays = settings.LogRetentionDays;
                MaxLogFileSizeMB = settings.MaxLogFileSizeMB;
                EnablePerformanceLogging = settings.EnablePerformanceLogging;
                EnableConsoleOutput = settings.EnableConsoleOutput;
                IncludeStackTraces = settings.IncludeStackTraces;
                
                _logger.LogDebug("Loaded logging settings from configuration - Current level: {LogLevel}", 
                    (LogEventLevel)SelectedLogLevel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load logging settings");
            }
        }

        private void LoadCurrentStatus()
        {
            try
            {
                // Get log directory from Serilog configuration
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ProxyStudio", "Logs");
                
                CurrentLogDirectory = logDirectory;
                CurrentLogFile = Path.Combine(logDirectory, "proxystudio.log");
                
                if (File.Exists(CurrentLogFile))
                {
                    var fileInfo = new FileInfo(CurrentLogFile);
                    CurrentLogFileSize = fileInfo.Length;
                }
                else
                {
                    CurrentLogFileSize = 0;
                }
                
                _logger.LogDebug("Loaded current log status - File: {LogFile}, Size: {FileSize} bytes", 
                    CurrentLogFile, CurrentLogFileSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load current log status");
            }
        }

        private void LoadRecentErrors()
        {
            try
            {
                var errors = _errorHandler.GetRecentErrors(20);
                RecentErrors.Clear();
                foreach (var error in errors)
                {
                    RecentErrors.Add(error);
                }
                
                _logger.LogDebug("Loaded {ErrorCount} recent errors", errors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load recent errors");
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Saving logging settings - New level: {LogLevel}", (LogEventLevel)SelectedLogLevel);
                
                var settings = _configManager.Config.LoggingSettings;
                settings.MinimumLogLevel = SelectedLogLevel;
                settings.LogRetentionDays = LogRetentionDays;
                settings.MaxLogFileSizeMB = MaxLogFileSizeMB;
                settings.EnablePerformanceLogging = EnablePerformanceLogging;
                settings.EnableConsoleOutput = EnableConsoleOutput;
                settings.IncludeStackTraces = IncludeStackTraces;
                
                _configManager.SaveConfig();
                
                // CRITICAL: Update Serilog level dynamically
                App.UpdateLogLevel((LogEventLevel)SelectedLogLevel);
                
                _logger.LogInformation("Logging settings saved and applied successfully - Active log level: {LogLevel}", 
                    (LogEventLevel)SelectedLogLevel);
                    
                await _errorHandler.ShowErrorAsync("Settings Saved", 
                    $"Logging settings have been saved and applied immediately. Current log level: {CurrentLogLevelName}", 
                    ErrorSeverity.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save logging settings");
                await _errorHandler.HandleExceptionAsync(ex, "Failed to save logging settings", "SaveLoggingSettings");
            }
        }

        [RelayCommand]
        private async Task OpenLogDirectoryAsync()
        {
            try
            {
                if (Directory.Exists(CurrentLogDirectory))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = CurrentLogDirectory,
                        UseShellExecute = true
                    });
                    
                    _logger.LogInformation("Opened log directory: {LogDirectory}", CurrentLogDirectory);
                }
                else
                {
                    await _errorHandler.ShowErrorAsync("Directory Not Found", 
                        $"Log directory does not exist: {CurrentLogDirectory}", ErrorSeverity.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open log directory");
                await _errorHandler.HandleExceptionAsync(ex, "Failed to open log directory", "OpenLogDirectory");
            }
        }

        [RelayCommand]
        private async Task ClearOldLogsAsync()
        {
            try
            {
                _logger.LogInformation("Clearing old log files (keeping {RetentionDays} days)", LogRetentionDays);
                
                var cutoffDate = DateTime.Now.AddDays(-LogRetentionDays);
                var logFiles = Directory.GetFiles(CurrentLogDirectory, "proxystudio*.log");
                var deletedCount = 0;
                
                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate && logFile != CurrentLogFile)
                    {
                        File.Delete(logFile);
                        deletedCount++;
                        _logger.LogDebug("Deleted old log file: {FileName}", Path.GetFileName(logFile));
                    }
                }
                
                LoadCurrentStatus(); // Refresh status
                
                await _errorHandler.ShowErrorAsync("Logs Cleared", 
                    $"Deleted {deletedCount} old log files (kept files from last {LogRetentionDays} days).", 
                    ErrorSeverity.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear old logs");
                await _errorHandler.HandleExceptionAsync(ex, "Failed to clear old log files", "ClearOldLogs");
            }
        }

        [RelayCommand]
        private void RefreshStatus()
        {
            try
            {
                LoadCurrentStatus();
                LoadRecentErrors();
                _logger.LogDebug("Refreshed logging status and recent errors");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh logging status");
            }
        }

        [RelayCommand]
        private async Task TestLoggingAsync()
        {
            try
            {
                // Test all log levels to verify the current setting is working
                _logger.LogTrace("🔍 TEST TRACE: This should only appear if log level is Trace");
                _logger.LogDebug("🐛 TEST DEBUG: This should only appear if log level is Debug or lower");
                _logger.LogInformation("ℹ️ TEST INFO: This should appear if log level is Info or lower");
                _logger.LogWarning("⚠️ TEST WARNING: This should appear if log level is Warning or lower");
                _logger.LogError("❌ TEST ERROR: This should appear if log level is Error or lower");
                _logger.LogCritical("🚨 TEST CRITICAL: This should always appear");
                
                // Test scope logging
                using var scope = _logger.BeginScope("TestLoggingScope");
                _logger.LogInformation("✅ TEST SCOPE: Operation completed within scope");
                
                // Force a flush to ensure messages are written immediately
                Log.CloseAndFlush();
                
                await _errorHandler.ShowErrorAsync("Logging Test Complete", 
                    $"Test messages have been written at all levels. Current active level: {CurrentLogLevelName}\n\n" +
                    $"Check the log file to see which messages appear based on your current setting. " +
                    $"Only messages at or above the '{CurrentLogLevelName}' level should be visible.", 
                    ErrorSeverity.Information);
                
                LoadRecentErrors(); // Refresh recent errors
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleExceptionAsync(ex, "Failed to test logging", "TestLogging");
            }
        }

        // UPDATED: Property change handler to immediately apply log level changes
        partial void OnSelectedLogLevelChanged(int value)
        {
            var newLevel = (LogEventLevel)value;
            _logger.LogInformation("Log level selection changed to: {LogLevel} - Use 'Save Settings' to apply", newLevel);
            
            // Optionally apply immediately without saving (uncomment if you want instant changes):
            // App.UpdateLogLevel(newLevel);
        }

        partial void OnLogRetentionDaysChanged(int value)
        {
            _logger.LogDebug("Log retention days changed to: {RetentionDays}", value);
        }

        partial void OnEnablePerformanceLoggingChanged(bool value)
        {
            _logger.LogDebug("Performance logging enabled changed to: {Enabled}", value);
        }

        // Computed properties
        public string CurrentLogFileSizeFormatted => 
            CurrentLogFileSize < 1024 ? $"{CurrentLogFileSize} bytes" :
            CurrentLogFileSize < 1024 * 1024 ? $"{CurrentLogFileSize / 1024.0:F1} KB" :
            $"{CurrentLogFileSize / (1024.0 * 1024.0):F1} MB";

        public string CurrentLogLevelName => 
            LogLevels.FirstOrDefault(l => l.Level == SelectedLogLevel)?.Name ?? "Unknown";
    }

    public class LogLevelOption
    {
        public int Level { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}