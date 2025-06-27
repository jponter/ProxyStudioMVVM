using System;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Metsys.Bson;
using ProxyStudio.Helpers;
using ProxyStudio.ViewModels;
using Avalonia.Styling;
using System.Reactive.Linq;

namespace ProxyStudio;

public partial class MainView : Window
{
    //todo move this to dependancy injection
    private AppConfig config; // Configuration settings
   
    public MainView()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        
        
        //load configuration settings
        config = ConfigManager.LoadConfig(); // Load the configuration settings
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
        ConfigManager.SaveConfig(config); // Save the configuration settings to file
    }

    
}