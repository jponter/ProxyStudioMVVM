using System;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ProxyStudio.Helpers;
using ProxyStudio.ViewModels;


namespace ProxyStudio;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        
        var services = new ServiceCollection();

        // Register your configuration manager as singleton
        services.AddSingleton<IConfigManager, ConfigManager>();

        // Register your ViewModels
        services.AddTransient<MainViewModel>();
        
        Services = services.BuildServiceProvider();
        
        var configManager = Services.GetRequiredService<IConfigManager>();
        
        configManager.LoadConfig();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            
            
            var mainWindow = new MainView(configManager);
            
            // Set the DataContext AFTER the window is created
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
            
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}