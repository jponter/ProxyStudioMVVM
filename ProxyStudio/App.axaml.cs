using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
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


namespace ProxyStudio;

public partial class App : Application
{
    private static readonly object LoggerLock = new object();
    private static readonly object InitializationLock = new object();
    private static volatile bool _isInitialized = false;
    private static int _initializationCount = 0;
    public ThemeType SelectedTheme { get; set; } = ThemeType.DarkProfessional;
   
    public static IServiceProvider? Services { get; private set; }
    
    public override void Initialize()
    {
        //edit for sync
        
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // CRITICAL: Prevent multiple initializations
        lock (InitializationLock)
        {
            var currentCount = Interlocked.Increment(ref _initializationCount);
            
            if (_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine($"WARNING: OnFrameworkInitializationCompleted called multiple times (#{currentCount}). Ignoring duplicate call.");
                base.OnFrameworkInitializationCompleted();
                return;
            }

            if (Design.IsDesignMode)
            {
                System.Diagnostics.Debug.WriteLine($"Skipping initialization in design mode (call #{currentCount})");
                base.OnFrameworkInitializationCompleted();
                return;
            }

            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine($"Starting SINGLE initialization (call #{currentCount})");
        }

        try
        {
            await InitializeApplicationAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CRITICAL: Application initialization failed: {ex}");
            
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
        var services = new ServiceCollection();

        // Register configuration first
        services.AddSingleton<IConfigManager, ConfigManager>();

        // Initialize logging ONCE
        InitializeLogging();

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
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IMpcFillService, MpcFillService>();
        services.AddSingleton<IThemeService, ThemeService>();
        
        // Register ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<PrintViewModel>();
        services.AddSingleton<DesignTimeConfigManager>();
        
        Services = services.BuildServiceProvider();
        
        // Single startup log entry
        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("=== PROXYSTUDIO SINGLE STARTUP {StartTime} (PID: {ProcessId}) ===", 
            DateTime.Now, Environment.ProcessId);
        
        try 
        {
            var themeService = Services.GetRequiredService<IThemeService>();
            logger.LogInformation("Theme service found: {ServiceType}", themeService.GetType().Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Theme service not found in DI container");
    
            // List all registered services for debugging
            var registeredServices = services.Select(s => s.ServiceType.Name).ToList();
            logger.LogInformation("Registered services: {Services}", string.Join(", ", registeredServices));
        }
        
        
        
        // Initialize application
        try
        {
            var configManager = Services.GetRequiredService<IConfigManager>();
            var errorHandler = Services.GetRequiredService<IErrorHandlingService>();
            configManager.LoadConfig();
            logger.LogInformation("Configuration loaded");
            
            
            try
            {
                var themeService = Services.GetRequiredService<IThemeService>();
                var savedTheme =  themeService.LoadThemePreference();
                await themeService.ApplyThemeAsync(savedTheme);
            }
            catch (Exception ex)
            {
                // Log the error but don't crash
                logger.LogError(ex, "Failed to apply theme, continuing without custom theme");
            }
            
            
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainView(configManager);
                var mainViewModel = Services.GetRequiredService<MainViewModel>();
                mainWindow.DataContext = mainViewModel;
                desktop.MainWindow = mainWindow;
                
                logger.LogInformation("Application initialized successfully - single instance");
            }
        }
        catch (Exception ex)
        {
            //var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogCritical(ex, "Critical startup failure");
            throw;
        }
        
        // Register shutdown handler ONCE
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktopLifetime.ShutdownRequested += OnShutdownRequested;
        }
    }
    
    private static void InitializeLogging()
    {
        // Only initialize logging if not already done
        if (Log.Logger != Serilog.Core.Logger.None)
        {
            System.Diagnostics.Debug.WriteLine("Logging already initialized, skipping...");
            return;
        }

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProxyStudio", "Logs");
        
        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, "proxystudio.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB max
                retainedFileCountLimit: 5,
                shared: true,
                buffered: false,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
            .CreateLogger();

        System.Diagnostics.Debug.WriteLine($"Logging initialized to: {logFilePath}");
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        try
        {
            var logger = Services?.GetService<ILogger<App>>();
            logger?.LogInformation("=== PROXYSTUDIO SHUTDOWN {ShutdownTime} ===", DateTime.Now);
            
            Log.CloseAndFlush();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex}");
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
                System.Diagnostics.Debug.WriteLine($"Found {files.Length} log files:");
                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    System.Diagnostics.Debug.WriteLine($"  {Path.GetFileName(file)} - {info.Length} bytes - {info.CreationTime}");
                }
            }
        }
    }
}