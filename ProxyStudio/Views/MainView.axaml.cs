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
using Microsoft.Extensions.Logging;
using Serilog;

namespace ProxyStudio;

public partial class MainView : Window
{
    //di configmanager interface
    private readonly IConfigManager _configManager; // Configuration settings
    public bool GlobalBleedEnabled { get; set; }
   
    
    // Add a parameterless constructor for design-time
    public MainView() : this(new DesignTimeConfigManager(), 
                             App.Services?.GetRequiredService<ILogger<MainView>>() ?? throw new ArgumentNullException(nameof(ILogger<MainView>)))
    {
        // This constructor will be used by the designer
    }
    
    public MainView(IConfigManager configManager, ILogger<MainView> logger)
    {
        //Console.WriteLine("MainView Constructor called");
        logger.BeginScope("MainView Constructor");
        logger.LogInformation("Initializing MainView with configuration manager.");
        InitializeComponent();
        //DataContext = new MainViewModel();
        
        _configManager = configManager;
        
        //_configManager = App.Services?.GetRequiredService<IConfigManager>() ?? throw new ArgumentNullException(nameof(IConfigManager));
       //
       //_configManager.LoadConfigAsync();
       
       //var config = ConfigManager.LoadConfig()
        var config = _configManager.Config;
        
        // Load configuration at startup
        
        logger.LogInformation("Loading Window configuration settings at startup.");
        logger.LogDebug($"Window Width: {Width}, Height: {Height}, Left: {Position.X}, Top: {Position.Y}" +  $", State: {WindowState}",
            config.WindowWidth, config.WindowHeight, config.WindowLeft, config.WindowTop);
        
        //DebugHelper.WriteDebug($"MainView() GlobalBleedEnabled = {config.GlobalBleedEnabled}");
        
        //load configuration settings
        //; // Load the configuration settings
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
        
        //DebugHelper.WriteDebug("Saving configuration settings on window close.");
        Log.Debug("Saving configuration settings on window close.");
        
       _configManager.SaveConfig();
    }

    
}