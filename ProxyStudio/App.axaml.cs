// Corrected App.axaml.cs with proper Serilog dynamic level switching
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;
using ProxyStudio.ViewModels;
using ProxyStudio.Services;
using Serilog;
using Serilog.Events;
using Serilog.Core;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ProxyStudio;

public partial class App : Application
{
    private static readonly object LoggerLock = new object();
    private static readonly object InitializationLock = new object();
    private static volatile bool _isInitialized = false;
    private static int _initializationCount = 0;
    
    // ✅ CRITICAL: Use the SAME LoggingLevelSwitch for both console and file
    private static LoggingLevelSwitch _loggingLevelSwitch = new LoggingLevelSwitch();
    
    public static IServiceProvider? Services { get; private set; }
    
    public static IConfigManager? _singleConfigManager { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("=== OnFrameworkInitializationCompleted CALLED ===");
        
        
        // CRITICAL: Prevent multiple initializations
        lock (InitializationLock)
        {
            var currentCount = Interlocked.Increment(ref _initializationCount);
            
            if (_isInitialized)
            {
                Console.WriteLine($"WARNING: OnFrameworkInitializationCompleted called multiple times (#{currentCount}). Ignoring duplicate call.");
                base.OnFrameworkInitializationCompleted();
                return;
            }

            if (Design.IsDesignMode)
            {
                Console.WriteLine($"Skipping initialization in design mode (call #{currentCount})");
                base.OnFrameworkInitializationCompleted();
                return;
            }

            _isInitialized = true;
            Console.WriteLine($"Starting SINGLE initialization (call #{currentCount})");
            
        }

        try
        {
            Console.WriteLine("About to call InitializeApplicationAsync...");
            await InitializeApplicationAsync();
            Console.WriteLine("InitializeApplicationAsync completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL: Application initialization failed: {ex}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Reset flag so we can try again if needed
            lock (InitializationLock)
            {
                _isInitialized = false;
            }
            
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private async System.Threading.Tasks.Task InitializeApplicationAsync()
    {
        Console.WriteLine("=== InitializeApplicationAsync STARTING ===");
        
        var services = new ServiceCollection();

        // Register configuration first
        Console.WriteLine("Registering IConfigManager...");
        _singleConfigManager = new ConfigManager();
        services.AddSingleton<IConfigManager>(_singleConfigManager);
        // var tempServiceProvider = services.BuildServiceProvider();
        // var configManager = tempServiceProvider.GetRequiredService<IConfigManager>();
        
        //Console.WriteLine("Loading config...");
        //configManager.LoadConfig();
        _singleConfigManager.LoadConfig();
        var configLogLevel = _singleConfigManager.Config.LoggingSettings.MinimumLogLevel;
        var initialLogLevel = (LogEventLevel)configLogLevel;
        
        Console.WriteLine($"Config loaded - MinimumLogLevel: {configLogLevel} -> {initialLogLevel}");
        
        // Initialize logging FIRST
        Console.WriteLine("Calling InitializeLogging...");
        InitializeLogging(initialLogLevel);
        Console.WriteLine("InitializeLogging completed");

        // Add Microsoft.Extensions.Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: false);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Register services
        services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
        services.AddSingleton<IPdfGenerationService, PdfGenerationService>();
        
        //optimised http client registration
        // ✅ OPTIMIZED: Configure global HTTP client with enhanced settings
        services.AddSingleton<HttpClient>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<HttpClient>>();
            logger?.LogInformation("Creating optimized global HTTP client");

            var handler = new HttpClientHandler()
            {
                // Increase max connections per server for better parallel performance
                MaxConnectionsPerServer = 20, // Up from default 2 - great for parallel MPC Fill downloads
            
                // Disable cookies to reduce overhead (most API calls don't need them)
                UseCookies = false,
            
                // Enable automatic decompression for better performance
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            
                // Use system proxy settings if available
                UseProxy = true,
                UseDefaultCredentials = false
            };

            var client = new HttpClient(handler, disposeHandler: true)
            {
                // Set reasonable timeout (45 seconds for slower connections/large files)
                Timeout = TimeSpan.FromSeconds(45)
            };

            // Add standard headers for better compatibility
            client.DefaultRequestHeaders.Add("User-Agent", 
                $"ProxyStudio/1.0 (.NET {Environment.Version}; {Environment.OSVersion})");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            client.DefaultRequestHeaders.ConnectionClose = false; // Keep connections alive for reuse

            logger?.LogInformation("Global HTTP client configured: MaxConnections=20, Timeout=45s, Compression=Enabled");
        
            return client;
        });
        
        
        
        
        
        
        services.AddSingleton<IMpcFillService, MpcFillService>();
        services.AddSingleton<IThemeService,ThemeService>();
        
        // Register ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<PrintViewModel>();
        services.AddSingleton<DesignTimeConfigManager>();
        
        Services = services.BuildServiceProvider();
        
        // Get logger and log startup
        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("=== PROXYSTUDIO SINGLE STARTUP {StartTime} (PID: {ProcessId}) ===", 
            DateTime.Now, Environment.ProcessId);
        logger.LogInformation("Initial log level set to: {LogLevel}", initialLogLevel);
        
        // Initialize application
        try
        {
            var errorHandler = Services.GetRequiredService<IErrorHandlingService>();
            logger.LogInformation("Configuration loaded");
            
            // Apply theme
            try
            {
                var themeService = Services.GetRequiredService<IThemeService>();
                var savedTheme = themeService.LoadThemePreference();
                await themeService.ApplyThemeAsync(savedTheme);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply theme, continuing without custom theme");
            }
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainView(_singleConfigManager, 
                    Services.GetRequiredService<ILogger<MainView>>());
                
                var mainViewModel = Services.GetRequiredService<MainViewModel>();
                mainWindow.DataContext = mainViewModel;
                desktop.MainWindow = mainWindow;
                
                logger.LogInformation("Application initialized successfully - single instance");
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical($"Error in application initialization: {ex}");
            throw;
        }
        
        // Register shutdown handler
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktopLifetime.ShutdownRequested += OnShutdownRequested;
        }
        
        logger.LogInformation("=== InitializeApplicationAsync COMPLETED ===");
    }
    
    // ✅ CLEAN APPROACH: Use only global level switch (no sink-specific switches)
private static void InitializeLogging(LogEventLevel initialLevel = LogEventLevel.Information)
{
    Console.WriteLine("=== INITIALIZING SERILOG (CLEAN APPROACH) ===");
    
    var logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProxyStudio", "Logs");
    
    Directory.CreateDirectory(logDirectory);
    var logFilePath = Path.Combine(logDirectory, "proxystudio.log");

    Console.WriteLine($"Log file path: {logFilePath}");
    Console.WriteLine($"Initial level: {initialLevel}");

    // Close any existing logger
    try
    {
        if (Log.Logger != null && Log.Logger != Serilog.Core.Logger.None)
        {
            Console.WriteLine("Closing existing logger...");
            Log.CloseAndFlush();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error closing existing logger: {ex.Message}");
    }

    // Set the level switch
    _loggingLevelSwitch.MinimumLevel = initialLevel;
    Console.WriteLine($"Level switch set to: {_loggingLevelSwitch.MinimumLevel}");
    Log.Debug($"Level switch set to: {_loggingLevelSwitch.MinimumLevel}");

    try
    {
        // ✅ SIMPLEST APPROACH: Only use global MinimumLevel.ControlledBy()
        // Let the global level switch control EVERYTHING
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_loggingLevelSwitch)
            .Enrich.FromLogContext()
            .WriteTo.Async(a => a.File(
                path: logFilePath,
                levelSwitch: _loggingLevelSwitch,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                retainedFileCountLimit: 5,
                shared: false,
                buffered: false,
                flushToDiskInterval: TimeSpan.FromMilliseconds(100),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
            ))
            .WriteTo.Console(
                // ❌ NO levelSwitch parameter - let global control it
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
            .CreateLogger();

        Log.Debug("Serilog logger created successfully with GLOBAL level switch only");
        
        // Test immediately
        Log.Information("=== SERILOG TEST: Initialization complete at level {Level} ===", initialLevel);
        Log.Debug("TEST DEBUG: This should appear if level is Debug or lower");
        Log.Information("TEST INFO: This should appear if level is Info or lower");
        Log.Warning("TEST WARNING: This should appear if level is Warning or lower");
        Log.Error("TEST ERROR: This should appear if level is Error or lower");
        
        Console.WriteLine("Test messages sent");
        
        // Check file creation
        Thread.Sleep(200); // Give file more time to be created
        if (File.Exists(logFilePath))
        {
            var fileInfo = new FileInfo(logFilePath);
            Console.WriteLine($"SUCCESS: Log file exists: {fileInfo.Length} bytes");
        }
        else
        {
            Console.WriteLine($"WARNING: Log file not found immediately - checking again...");
            Thread.Sleep(500);
            if (File.Exists(logFilePath))
            {
                var fileInfo = new FileInfo(logFilePath);
                Console.WriteLine($"SUCCESS: Log file created: {fileInfo.Length} bytes");
            }
            else
            {
                Console.WriteLine($"ERROR: Log file still not created");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR initializing Serilog: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
    
    Log.Debug("=== SERILOG INITIALIZATION COMPLETE ===");
}

    // ✅ CORRECTED: UpdateLogLevel method that properly updates both sinks
    public static void UpdateLogLevel(LogEventLevel newLevel)
    {
        try
        {
            lock (LoggerLock)
            {
                Console.WriteLine($"=== REBUILDING LOGGER WITH NEW LEVEL: {newLevel} ===");

                _loggingLevelSwitch.MinimumLevel = newLevel;

                //_logging.CloseAndFlush();

                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ProxyStudio", "Logs");
                Directory.CreateDirectory(logDirectory);
                var logFilePath = Path.Combine(logDirectory, "proxystudio.log");

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(_loggingLevelSwitch)
                    .Enrich.FromLogContext()
                    .WriteTo.Async(a => a.File(
                        path: logFilePath,
                        levelSwitch: _loggingLevelSwitch,
                        rollingInterval: RollingInterval.Day,
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: 10 * 1024 * 1024,
                        retainedFileCountLimit: 5,
                        shared: true,
                        buffered: false,
                        flushToDiskInterval: TimeSpan.FromMilliseconds(100),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                    ))
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
                    .CreateLogger();
                // Thread.Sleep(500);
                // Log.Information("Logger rebuilt with new level: {NewLevel}", newLevel);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR rebuilding logger: {ex}");
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        try
        {
            var logger = Services?.GetService<ILogger<App>>();
            
            logger?.LogInformation("=== PROXYSTUDIO SHUTDOWN REQUESTED {ShutdownTime} ===", DateTime.Now);
            
            // ✅ CRITICAL: Save config using the SAME ConfigManager instance
            if (_singleConfigManager != null)
            {
                logger?.LogInformation("Saving configuration using SINGLE ConfigManager instance...");
                _singleConfigManager.SaveConfig();
                logger?.LogInformation("Configuration saved successfully during shutdown");
            }
            else
            {
                logger?.LogWarning("ConfigManager instance is null during shutdown - config not saved!");
            }

            
            logger?.LogInformation("=== PROXYSTUDIO SHUTDOWN {ShutdownTime} ===", DateTime.Now);
            
            Log.CloseAndFlush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during shutdown: {ex}");
        }
    }
    
    public static class LoggingDiagnostics
    {
        public static void CheckLogFiles()
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProxyStudio", "Logs");

            if (Directory.Exists(logDirectory))
            {
                var files = Directory.GetFiles(logDirectory, "*.log");
                //Console.WriteLine($"Found {files.Length} log files");
                Log.Debug("Found {FilesLength} log files",files.Length);
            }
        }
    }
}