using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ProxyStudio.Helpers;

using ProxyStudio.ViewModels;
using ProxyStudio.Services;


namespace ProxyStudio;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    
    public override void Initialize()
    {
        //edit for sync
        
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // Register your configuration manager as singleton
        services.AddSingleton<IConfigManager, ConfigManager>();

        // Register PDF generation service
        services.AddSingleton<IPdfGenerationService, PdfGenerationService>();

        // Register your ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<PrintViewModel>();
        services.AddSingleton<DesignTimeConfigManager>(); // Design-time config manager for design mode
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IMpcFillService, MpcFillService>(); // Maps interface to implementation
        
        
        Services = services.BuildServiceProvider();
        
        var configManager = Services.GetRequiredService<IConfigManager>();
        var pdfService = Services.GetRequiredService<IPdfGenerationService>();
        var mpcFillService = Services.GetRequiredService<IMpcFillService>();
        
        
        configManager.LoadConfig();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainView(configManager);
            
            // Set the DataContext AFTER the window is created
            mainWindow.DataContext = new MainViewModel(configManager, pdfService, mpcFillService);
            
            
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}