using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProxyStudio.Helpers;
using ProxyStudio.ViewModels;

namespace ProxyStudio.Views;

public partial class MainView : Window
{
    //di configmanager interface
    private IConfigManager _configManager; // Configuration settings
    private  ILogger<MainView> _logger;
    public bool GlobalBleedEnabled { get; set; }
    
    // ScrollViewer for the card grid
    private ScrollViewer? _cardScrollViewer;
    private UniformGrid? _cardUniformGrid;
    private const double MinCardWidth = 421.0; // Minimum width for cards
    private const double ScrollViewerPadding = 80.0; // Padding for the ScrollViewer
   
    
    // Safe parameterless constructor for design-time
    public MainView() 
    {
        // Always call InitializeComponent first
        InitializeComponent();
        
        // Only set up DI when NOT in design mode
        if (!Design.IsDesignMode)
        {
            try
            {
                var configManager = App.Services?.GetService<IConfigManager>() ?? new DesignTimeConfigManager();
                var logger = App.Services?.GetService<ILogger<MainView>>() ?? NullLogger<MainView>.Instance;
                InitializeWithDependencies(configManager, logger);
            }
            catch (Exception ex)
            {
                // Fallback to design-time dependencies if DI fails
                var configManager = new DesignTimeConfigManager();
                var logger = NullLogger<MainView>.Instance;
                InitializeWithDependencies(configManager, logger);
                
                // Log the error if possible
                System.Diagnostics.Debug.WriteLine($"MainView DI initialization failed: {ex.Message}");
            }
        }
        else
        {
            // Design-time setup with safe defaults
            var designConfigManager = new DesignTimeConfigManager();
            _configManager = designConfigManager;
            
            // Set design-time DataContext if needed
            if (DataContext == null)
            {
                DataContext = new DesignTimeMainViewModel();
            }
        }
    }
    
    
    
    // // Add a parameterless constructor for design-time
    // public MainView() : this(new DesignTimeConfigManager(), 
    //                          App.Services?.GetRequiredService<ILogger<MainView>>() ?? throw new ArgumentNullException(nameof(ILogger<MainView>)))
    // {
    //     // This constructor will be used by the designer
    // }
    
    public MainView(IConfigManager configManager, ILogger<MainView> logger)
    {
        //Console.WriteLine("MainView Constructor called");
        logger.BeginScope("MainView Constructor");
        logger.LogInformation("Initializing MainView with configuration manager.");
        InitializeComponent();
        InitializeWithDependencies(configManager, logger);
       //  //DataContext = new MainViewModel();
       //  
       //  _configManager = configManager;
       //  
       //  //_configManager = App.Services?.GetRequiredService<IConfigManager>() ?? throw new ArgumentNullException(nameof(IConfigManager));
       // //
       // //_configManager.LoadConfigAsync();
       //
       // //var config = ConfigManager.LoadConfig()
       //  var config = _configManager.Config;
       //  
       //  // Load configuration at startup
       //  
       //  logger.LogInformation("Loading Window configuration settings at startup.");
       //  logger.LogDebug($"Window Width: {Width}, Height: {Height}, Left: {Position.X}, Top: {Position.Y}" +  $", State: {WindowState}",
       //      config.WindowWidth, config.WindowHeight, config.WindowLeft, config.WindowTop);
       //  
       //  //DebugHelper.WriteDebug($"MainView() GlobalBleedEnabled = {config.GlobalBleedEnabled}");
       //  
       //  //load configuration settings
       //  //; // Load the configuration settings
       //  if (config.WindowWidth > 0) Width = config.WindowWidth; // Set the window width from the configuration
       //  if (config.WindowHeight > 0)this.Height = config.WindowHeight; // Set the window height from the configuration
       //  // this.Top = config.WindowTop; // Set the window top position from the configuration
       //  // this.Left = config.WindowLeft; // Set the window left position from the configuration
       //  
       //  //need to use mainwindow.poisition for Avalonia
       //  this.Position = new PixelPoint(config.WindowLeft, config.WindowTop);
       //
       //
       //  PositionChanged += CacheGeometry;
       //  this.GetObservable<Rect>(BoundsProperty)
       //      .Subscribe(rect => CacheGeometry(null, null));
       //  
        

    }
    
    private void InitializeWithDependencies(IConfigManager configManager, ILogger<MainView> logger)
    {
        logger.BeginScope("MainView Constructor");
        logger.LogInformation("Initializing MainView with configuration manager.");
        
        _configManager = configManager;
        _logger = logger;
        
        var config = _configManager.Config;
        
        logger.LogInformation("Loading Window configuration settings at startup.");
        logger.LogDebug($"Window Width: {Width}, Height: {Height}, Left: {Position.X}, Top: {Position.Y}, State: {WindowState}");
        
        // Load configuration settings
        if (config.WindowWidth > 0) Width = config.WindowWidth;
        if (config.WindowHeight > 0) Height = config.WindowHeight;
        
        // Set window position
        Position = new PixelPoint(config.WindowLeft, config.WindowTop);
        
        // Set up event handlers
        PositionChanged += CacheGeometry;
        this.GetObservable<Rect>(BoundsProperty)
            .Subscribe(rect => CacheGeometry(null, null));
        
        //set up the tab refresh handler
        this.Loaded += OnMainViewLoaded;
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
    
    private void OnMainViewLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Find the TabControl and subscribe to SelectionChanged event
            var tabControl = this.FindControl<TabControl>("MainTabControl");
            if (tabControl != null)
            {
                tabControl.SelectionChanged += OnTabSelectionChanged;
                _logger?.LogDebug("Successfully subscribed to TabControl SelectionChanged event");
            }
            else
            {
                _logger?.LogWarning("Could not find TabControl with name 'MainTabControl'");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting up tab selection handler");
        }

        try
        {
            // Find the ScrollViewer containing the card grid
            _cardScrollViewer = this.FindControl<ScrollViewer>("CardScrollViewer");

            if (_cardScrollViewer != null)
            {
                _cardScrollViewer.SizeChanged += CardScrollViewer_SizeChanged;
                _logger?.LogDebug("Successfully subscribed to CardScrollViewer SizeChanged event");
            }
            else
            {
                _logger?.LogWarning("Could not find CardScrollViewer with name 'CardScrollViewer'");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting up CardScrollViewer event");
        }
        
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is TabControl tabControl && DataContext is MainViewModel mainViewModel)
            {
                _logger?.LogDebug("Tab selection changed to index: {TabIndex}", tabControl.SelectedIndex);
                
                // Check if the logging tab is selected (index 3: Cards=0, Printing=1, Settings=2, ThemeEditor=3, Logging=4)
                if (tabControl.SelectedIndex == 4 && mainViewModel.LoggingSettingsViewModel != null)
                {
                    _logger?.LogDebug("Logging tab selected - refreshing recent errors");
                    
                    // Refresh the recent errors when switching to logging tab
                    mainViewModel.LoggingSettingsViewModel.RefreshStatusCommand.Execute(null);
                    
                    _logger?.LogDebug("Recent errors refreshed successfully");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling tab selection change");
            // Don't throw - this is UI event handling
        }
    }
    
    private void CardScrollViewer_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_cardUniformGrid == null)
        {
            // Find the UniformGrid within the ListBox ItemsPanel
            var cardGrid = this.FindControl<ListBox>("CardGrid");
            if (cardGrid?.Presenter?.Panel is UniformGrid uniformGrid)
            {
                _cardUniformGrid = uniformGrid;
            }
        }

        if (_cardUniformGrid != null && e.NewSize.Width > 0)
        {
            UpdateGridColumns(e.NewSize.Width);
        }
    }
    
    private void UpdateGridColumns(double containerWidth)
    {
        if (_cardUniformGrid == null) return;

        // Calculate available width
        var availableWidth = containerWidth - ScrollViewerPadding;
            
        if (availableWidth <= 0)
        {
            _cardUniformGrid.Columns = 2;
            _logger?.LogWarning("Available width is too small, setting columns to minimum of 2.");
            return;
        }

        // Calculate optimal number of columns
        var possibleColumns = Math.Floor(availableWidth / MinCardWidth);
        var columns = Math.Max(2, Math.Min(6, (int)possibleColumns));
            
        // Only update if changed to avoid unnecessary layout passes
        if (_cardUniformGrid.Columns != columns)
        {
            _cardUniformGrid.Columns = columns;
            _logger?.LogDebug("Updated card grid columns to {Columns} based on available width {AvailableWidth}", columns, availableWidth);
        }
    } 

    
    // protected override void OnClosed(EventArgs e)
    // {
    //  
    //     base.OnClosed(e);
    //     
    //     //DebugHelper.WriteDebug("Saving configuration settings on window close.");
    //     //Log.Debug("Saving configuration settings on window close.");
    //     
    //    //_configManager.SaveConfig();
    // }

    
}