using System;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Metsys.Bson;
using ProxyStudio.Helpers;
using ProxyStudio.ViewModels;
using Avalonia.Styling;
using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace ProxyStudio;

public partial class MainView : Window
{
    //di configmanager interface
    private readonly IConfigManager _configManager; // Configuration settings
   
    public MainView()
    {
        InitializeComponent();
        //DataContext = new MainViewModel();
        
        
        
        _configManager = App.Services?.GetRequiredService<IConfigManager>() ?? throw new ArgumentNullException(nameof(IConfigManager));
       //
       //_configManager.LoadConfigAsync();
       
       
        
        
        // Load configuration at startup
        
         
        var config = _configManager.LoadConfig(); //synchronous loading
        
        //load configuration settings
        //config = ConfigManager.LoadConfig(); // Load the configuration settings
        if (config.WindowWidth > 0) Width = config.WindowWidth; // Set the window width from the configuration
        if (config.WindowHeight > 0)this.Height = config.WindowHeight; // Set the window height from the configuration
        // this.Top = config.WindowTop; // Set the window top position from the configuration
        // this.Left = config.WindowLeft; // Set the window left position from the configuration
        
        //need to use mainwindow.poisition for Avalonia
        this.Position = new PixelPoint(config.WindowLeft, config.WindowTop);

        PositionChanged += CacheGeometry;
        this.GetObservable<Rect>(BoundsProperty)
            .Subscribe(rect => CacheGeometry(null, null));
        
        

    }
    /// <summary>Keep the config object up-to-date while the app is running.</summary>
    private void CacheGeometry(object? s, EventArgs? _)
    {
        var config = _configManager.Config;
        // Only store real coordinates when we’re not maximised/minimised
        if (WindowState == WindowState.Normal)
        {
            config.WindowLeft   = Position.X;
            config.WindowTop    = Position.Y;
            config.WindowWidth  = (int)Width;
            config.WindowHeight = (int)Height;
        }
        config.WindowState = WindowState;     // normal / maximised / minimised
    }

    
    protected override void OnClosed(EventArgs e)
    {
     
        base.OnClosed(e);
        
        DebugHelper.WriteDebug("Saving configuration settings on window close.");
        
        //todo fix the config saving
       _configManager.SaveConfig();
    }

    
}