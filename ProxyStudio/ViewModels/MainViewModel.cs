using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Dialogs;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Metsys.Bson;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using ProxyStudio.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ProxyStudio.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // UPDATED: Card dimensions in points (72 DPI standard) - FIXED at exactly 63mm × 88mm
    private const double CARD_WIDTH_MM = 63.0;
    private const double CARD_HEIGHT_MM = 88.0;
    
    // Convert mm to inches, then to points (1 inch = 25.4mm, 1 inch = 72 points)
    private const double CARD_WIDTH_INCHES = CARD_WIDTH_MM / 25.4;    // 2.480 inches
    private const double CARD_HEIGHT_INCHES = CARD_HEIGHT_MM / 25.4;  // 3.465 inches
    
    private const double CARD_WIDTH_POINTS = CARD_WIDTH_INCHES * 72;   // 178.583 points
    private const double CARD_HEIGHT_POINTS = CARD_HEIGHT_INCHES * 72; // 249.449 points
    
    
    //di interfaces
    private readonly IConfigManager _configManager;
    private readonly IPdfGenerationService _pdfService;
    private readonly IMpcFillService _mpcFillService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IErrorHandlingService _errorHandler;
    private readonly IThemeService _themeService;
    private readonly IServiceProvider _serviceProvider;
    
    public ThemeSettingsViewModel? ThemeSettingsViewModel { get; private set; }
    
    
    
    
    //public ObservableCollection<Card> Cards { get; } = new();
    public CardCollection Cards { get; private set; } = new();

    // Print ViewModel
    [ObservableProperty] private PrintViewModel? _printViewModel;
    [ObservableProperty] private LoggingSettingsViewModel? _loggingSettingsViewModel; 
    

    // configuration properties
    [ObservableProperty] private bool _globalBleedEnabled;
    
    // MPC Fill Progress Properties
    [ObservableProperty] private string _mpcFillStatus = "";
    [ObservableProperty] private double _mpcFillProgress = 0.0;
    [ObservableProperty] private string _mpcFillCurrentOperation = "";
    [ObservableProperty] private string _mpcFillCurrentCard = "";
    [ObservableProperty] private string _mpcFillTimeRemaining = "";
    [ObservableProperty] private bool _showMpcFillProgress = false;
    [ObservableProperty] private bool _isLoadingMpcFill = false;
    
    //single image loading properties
    [ObservableProperty] private bool _isLoadingSingleImage = false;
    

    partial void OnGlobalBleedEnabledChanged(bool value)
    {
        _configManager.Config.GlobalBleedEnabled = value;
        
        _logger.LogDebug($"Global bleed enabled changed to {value}");
    }

    //selected card
    [ObservableProperty] private Card? _selectedCard;

    // check to see if we are busy before adding cards
    private bool CanAddTestCards()
    {
        return !IsBusy;
    }

    [ObservableProperty] private bool _isBusy;

    //constructor with design time check as some of the DI stuff breaks it
    public MainViewModel(IConfigManager configManager, IPdfGenerationService pdfService, 
        IMpcFillService mpcFillService, ILogger<MainViewModel> logger, IErrorHandlingService errorHandler, ILoggerFactory loggerFactory, IThemeService themeService, IServiceProvider serviceProvider )
    {
        
        _logger = logger;
        _logger.LogInformation("MainViewModel constructor starting...");
        
        _mpcFillService = mpcFillService;
        _logger.LogInformation("MpcFillService assigned");
        _configManager = configManager;
        _pdfService = pdfService;
        _serviceProvider = serviceProvider;
        
        _errorHandler = errorHandler;
        _logger.LogInformation("ErrorHandler assigned");
        _themeService = themeService;
        
        // Add this to your MainViewModel constructor
        App.LoggingDiagnostics.CheckLogFiles();
        
        // Add instance ID to help track multiple instances
        var instanceId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("MainViewModel initializing (Instance: {InstanceId})", instanceId);
        
       
        ThemeSettingsViewModel = new ThemeSettingsViewModel(themeService, loggerFactory.CreateLogger<ThemeSettingsViewModel>(), errorHandler);

        if (Design.IsDesignMode)
        {
            _logger.LogDebug("Running in design mode - using minimal initialization");
            // Only set up minimal data for design-time
            // Don't load config, don't load images, etc.
            Cards.AddRange(AddTestCards());
            // Create a simple PrintViewModel for design time
            //PrintViewModel = new PrintViewModel(_pdfService, _configManager, Cards);
            return;
        }

        try
        {
            GlobalBleedEnabled = _configManager.Config.GlobalBleedEnabled;
            _logger.LogInformation("GlobalBleedEnabled set");
            var printViewModelLogger = loggerFactory.CreateLogger<PrintViewModel>();
            
            PrintViewModel = new PrintViewModel(_pdfService, _configManager, Cards, printViewModelLogger, _errorHandler);
            _logger.LogInformation("PrintViewModel created");
            var loggingSettingsLogger = loggerFactory.CreateLogger<LoggingSettingsViewModel>();
            LoggingSettingsViewModel = new LoggingSettingsViewModel(_configManager, loggingSettingsLogger, _errorHandler);

        
            _logger.LogInformation("MainViewModel initialized successfully (Instance: {InstanceId})", instanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MainViewModel (Instance: {InstanceId})", instanceId);
            _ = _errorHandler.HandleExceptionAsync(ex, "Failed to initialize the main application", "MainViewModel Constructor");
        }
    }

    // Constructor for design-time support - UPDATED
    // Design-time constructor (simpler)
    // DESIGN-TIME CONSTRUCTOR: Super simple, no complex dependencies
    public MainViewModel(IConfigManager configManager) : this(
        configManager, 
        new DesignTimePdfService(), 
        new DesignTimeMpcFillService(), 
        Microsoft.Extensions.Logging.Abstractions.NullLogger<MainViewModel>.Instance, // Use built-in null logger
        new DesignTimeErrorHandlingService(),
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,  // Use built-in null factory
        new DesignTimeThemeService(),
        new ServiceCollection()
            .AddSingleton<IConfigManager>(configManager)
            .AddSingleton<IPdfGenerationService, DesignTimePdfService>()
            .AddSingleton<IMpcFillService, DesignTimeMpcFillService>()
            .AddSingleton<IErrorHandlingService, DesignTimeErrorHandlingService>()
            .AddSingleton<IThemeService, DesignTimeThemeService>()
            .BuildServiceProvider()
    )
    {
        // Design-time constructor
    }
    
    private class DesignTimeThemeService : IThemeService
    {
        public ThemeType CurrentTheme => ThemeType.DarkProfessional;

        public IReadOnlyList<ThemeDefinition> AvailableThemes { get; } = new List<ThemeDefinition>
        {
            new() {
                Type = ThemeType.DarkProfessional,
                Name = "Dark Professional",
                Description = "Design-time theme",
                ResourcePath = "",
                IsDark = true
            }
        };

        public ThemeType LoadThemePreference()
        {
            throw new NotImplementedException();
        }

        public event EventHandler<ThemeType>? ThemeChanged;

        public Task ApplyThemeAsync(ThemeType theme)
        {
            return Task.CompletedTask;
        }

        public bool SaveThemePreference(ThemeType theme)
        {
            throw new NotImplementedException();
        }

       
    }
    
    // ADD DESIGN-TIME MPC FILL SERVICE
    private class DesignTimeMpcFillService : IMpcFillService
    {
        public Task<List<Card>> LoadCardsFromXmlAsync(string xmlFilePath, IProgress<MpcFillProgress>? progress = null)
        {
            return Task.FromResult(new List<Card>());
        }

        public Task<byte[]> DownloadCardImageAsync(string cardId)
        {
            return Task.FromResult(new byte[0]);
        }

        public Task<List<Card>> ProcessXmlContentAsync(string xmlContent, IProgress<MpcFillProgress>? progress = null)
        {
            return Task.FromResult(new List<Card>());
        }
    }
    
    

    // Simple design-time PDF service
    // Simple design-time PDF service
    private class DesignTimePdfService : IPdfGenerationService
    {
        public Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options, IProgress<PdfGenerationProgress>? progress = null)
        {
            // Simulate progress for design-time
            if (progress != null)
            {
                var progressInfo = new PdfGenerationProgress
                {
                    CurrentStep = 1,
                    TotalSteps = 1,
                    CurrentOperation = "Design-time generation",
                    //PercentageComplete = 100
                };
                progress.Report(progressInfo);
            }
        
            return Task.FromResult(new byte[0]);
        }

        public Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return Task.FromResult<Bitmap>(null!);
        }
    }
    
    private class DesignTimeErrorHandlingService : IErrorHandlingService
    {
        public Task ShowErrorAsync(string title, string message, ErrorSeverity severity = ErrorSeverity.Error, Exception? exception = null)
        {
            // In design time, just return completed task
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(UserError error)
        {
            // In design time, just return completed task
            return Task.CompletedTask;
        }

        public Task HandleExceptionAsync(Exception exception, string userFriendlyMessage, string operationContext = "")
        {
            // In design time, just log to debug and return completed task
            System.Diagnostics.Debug.WriteLine($"Design-time error: {userFriendlyMessage} - {exception?.Message}");
            return Task.CompletedTask;
        }

        public bool ValidateAndShowError(bool condition, string errorMessage, string title = "Validation Error")
        {
            // In design time, always return the condition as-is
            return condition;
        }

        public Task ShowRecoverableErrorAsync(string title, string message, string recoveryAction, Func<Task> recoveryCallback)
        {
            // In design time, just return completed task
            return Task.CompletedTask;
        }

        public Task ReportErrorAsync(Exception exception, string additionalContext = "")
        {
            // In design time, just log to debug and return completed task
            System.Diagnostics.Debug.WriteLine($"Design-time error report: {additionalContext} - {exception?.Message}");
            return Task.CompletedTask;
        }

        public List<UserError> GetRecentErrors(int count = 10)
        {
            // In design time, return empty list
            return new List<UserError>();
        }
    }

    
    private class DesignTimeLogger : ILogger<MainViewModel>
    {
        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // Do nothing in design time
        }
    }
    
    
    // Update the AddTestCardsRelayAsync method in MainViewModel:

    [RelayCommand(CanExecute = nameof(CanAddTestCards))]
    private async Task AddTestCardsRelayAsync()
    {
        using var scope = _logger.BeginScope("AddTestCards");
        
        try
        {
            if (!_errorHandler.ValidateAndShowError(!IsBusy, "Cannot add test cards while another operation is in progress"))
                return;

            IsBusy = true;
            _logger.LogInformation("Starting to add test cards");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var newCards = await Task.Run(() => AddTestCards());
            stopwatch.Stop();
            
            if (newCards != null && newCards.Count > 0)
            {
                Cards.AddRange(newCards);
                PrintViewModel?.RefreshCardInfo();
                PrintViewModel?.GeneratePreviewCommand.Execute(null);
                
                _logger.LogInformation("Successfully added {CardCount} test cards in {ElapsedMs}ms. Total cards: {TotalCards}", 
                    newCards.Count, stopwatch.ElapsedMilliseconds, Cards.Count);
                    
                await _errorHandler.ShowErrorAsync("Test Cards Added", 
                    $"Successfully added {newCards.Count} test cards to your collection.", 
                    ErrorSeverity.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add test cards");
            await _errorHandler.HandleExceptionAsync(ex, "Failed to add test cards", "AddTestCards");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void EditCard(Card card)
    {
        // open an edit dialog, navigate, etc.
        // e.g. DialogService.ShowEditCard(card);
        SelectedCard = card;
    }

    

    private List<Card> AddTestCards()
    {
        List<Card> cards = new();

        _logger.LogDebug("Loading high-resolution images for dynamic DPI scaling...");
    
        // Load images at their native resolution
        var image = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/preacher.jpg");
        var image2 = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/vampire.jpg");

        _logger.LogDebug($"Loaded images: preacher={image.Width}x{image.Height}, vampire={image2.Width}x{image2.Height}");

        // IMPORTANT: Store images at a high base resolution instead of scaling at startup
        // We'll use 600 DPI (1500x2100) as our "source" resolution that can be scaled down
        const int baseDpi = 600;
        var baseWidth = (int)(CARD_WIDTH_INCHES * baseDpi);   // 1500 pixels
        var baseHeight = (int)(CARD_HEIGHT_INCHES * baseDpi);  // 2100 pixels

        // Resize to high base resolution for maximum quality
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(baseWidth, baseHeight),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3 // High-quality resampling
        }));

        image2.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(baseWidth, baseHeight),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3
        }));

        // Store as high-quality PNG to preserve all detail for later scaling
        var pngEncoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
        {
            CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression,
            ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
        };

        using var ms1 = new MemoryStream();
        image.Save(ms1, pngEncoder);
        var buffer = ms1.ToArray();

        using var ms2 = new MemoryStream();
        image2.Save(ms2, pngEncoder);
        var buffer2 = ms2.ToArray();

        _logger.LogDebug($"Created high-resolution base images: {baseWidth}x{baseHeight} ({baseDpi} DPI base)");
        _logger.LogDebug($"Image sizes: preacher={buffer.Length} bytes, vampire={buffer2.Length} bytes");

        for (var i = 0; i < 2; i++)
        {
            cards.Add(new Card("Preacher of the Schism", "12345", buffer, _configManager));
            cards.Add(new Card("Vampire Token", "563726", buffer2, _configManager));
            cards.Add(new Card("Preacher of the Schism", "12345", buffer, _configManager));
            cards.Add(new Card("Vampire Token", "563726", buffer2, _configManager));
        }

        foreach (var card in cards) card.EditMeCommand = EditCardCommand;
        foreach (var card in cards) card.EnableBleed= true;
        
    
        _logger.LogDebug($"Created {cards.Count} cards with high-resolution images ready for dynamic DPI scaling");
        return cards;
    }
    
    
    
    
    
    /// <summary>
    /// Processes image files dropped onto the application
    /// </summary>
    /// <param name="imageData">The raw image data</param>
    /// <param name="fileName">The original file name (used as card name)</param>
    public async Task ProcessImageFileAsync(byte[] imageData, string fileName)
    {
        try
        {
            _logger.LogDebug($"Processing image file: {fileName}");
        
            IsBusy = true;
        
            var card = await Task.Run(() => CreateCardFromImage(imageData, fileName));
        
            if (card != null)
            {
                Cards.AddCard(card);
                PrintViewModel?.RefreshCardInfo();
                PrintViewModel?.GeneratePreviewCommand.Execute(null);
            
                _logger.LogDebug($"Successfully added card from image: {fileName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical($"Error processing image file {fileName}: {ex.Message}");
            await _errorHandler.HandleExceptionAsync(ex,"Error processing image file", "ProcessImageFileAsync");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Create a Card from image data, processing it to high resolution
    /// </summary>
    private Card CreateCardFromImage(byte[] imageData, string fileName)
    {
        // Use filename without extension as card name
        var cardName = Path.GetFileNameWithoutExtension(fileName);
        var cardId = Guid.NewGuid().ToString();
        _logger.LogDebug($"Creating card: {cardName} from {fileName} with ID {cardId}");
        // Process image using same high-resolution logic as AddTestCards
        var processedImageData = ProcessImageToHighResolution(imageData);
    
        var card = new Card(cardName, cardId, processedImageData, _configManager);
        card.EditMeCommand = EditCardCommand;
    
        return card;
    }

    /// <summary>
    /// Process image to high base resolution for quality scaling
    /// </summary>
    private byte[] ProcessImageToHighResolution(byte[] imageData)
    {
        //todo - we could check here to see if the aspect ration indicates bleed is needed
        
        const int baseDpi = 600;
        var baseWidth = (int)(CARD_WIDTH_INCHES * baseDpi);   // 1500 pixels
        var baseHeight = (int)(CARD_HEIGHT_INCHES * baseDpi);  // 2100 pixels
    
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageData);
    
        // Resize to high base resolution
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(baseWidth, baseHeight),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3
        }));
    
        // Save as high-quality PNG
        var pngEncoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
        {
            CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression,
            ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
        };
    
        using var ms = new MemoryStream();
        image.Save(ms, pngEncoder);
        return ms.ToArray();
    }


    
    // Add this RelayCommand to your MainViewModel.cs class

    [RelayCommand ]
    private void DeleteAllCards()
    {
        try
        {
            _logger.LogDebug("DeleteAllCards: Starting to delete all cards");
        
            var cardCount = Cards.Count;
        
            if (cardCount == 0)
            {
                _logger.LogDebug("DeleteAllCards: No cards to delete");
                return;
            }
        
            // Clear selection first
            SelectedCard = null;
        
            // Remove all cards from collection
            Cards.RemoveAllCards();
        
            // IMPORTANT: Refresh PrintViewModel after clearing cards
            // This will automatically clear the preview since CardCount = 0
            PrintViewModel?.RefreshCardInfo();
        
            _logger.LogDebug($"Successfully deleted all {cardCount} cards and cleared preview");
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Error deleting all cards: {ex.Message}");
            _errorHandler.HandleExceptionAsync(ex,"Error deleting all cards", "DeleteAllCards");
        }
    }


    



    [RelayCommand]
    private void DeleteCard(Card card)
    {
        try
        {
            if (card == null)
            {
                _logger.LogDebug("DeleteCard: Card parameter is null");
                return;
            }

            _logger.LogDebug($"Deleting card: {card.Name} (ID: {card.Id})");

            // Clear selection if we're deleting the selected card
            if (SelectedCard == card)
            {
                SelectedCard = null;
            }

            // Remove from collection
            Cards.RemoveCard(card);

            // Refresh PrintViewModel
            PrintViewModel?.RefreshCardInfo();
        
            // Regenerate preview to reflect the change
            PrintViewModel?.GeneratePreviewCommand.Execute(null);

            _logger.LogDebug($"Successfully deleted card. Remaining cards: {Cards.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogCritical($"Error deleting card {card?.Name}: {ex.Message}");
            _errorHandler.HandleExceptionAsync(ex, "Error deleting card", "DeleteCard");
        }
    }

// Optional: Add a method to check if deletion is allowed
    private bool CanDeleteCard(Card card)
    {
        return card != null && !IsBusy;
    }
    
    
    
    // UPDATE your ProcessMPCFillXML method
    public async Task ProcessMPCFillXML(string fileName)
    {
        using var scope = _logger.BeginScope("ProcessMPCFillXML");
        List<Card>? newCards = null;
        Exception? lastError = null;
        
        try
        {
            
            _logger.LogInformation($"Loading MPC Fill XML: {fileName}");
            IsBusy = true;
            IsLoadingMpcFill = true;
            ShowMpcFillProgress = true;
            MpcFillProgress = 0;
            MpcFillStatus = "Starting MPC Fill import...";

            var startTime = DateTime.Now;

            // Create progress reporter for UI updates
            var progress = new Progress<MpcFillProgress>(progressInfo =>
            {
                // Update UI properties on main thread
                MpcFillProgress = progressInfo.PercentageComplete;
                MpcFillStatus = $"{progressInfo.PercentageComplete:F0}% - {progressInfo.CurrentOperation}";
                MpcFillCurrentOperation = progressInfo.CurrentOperation;
                MpcFillCurrentCard = progressInfo.CurrentCardName;
                
                // Calculate time remaining
                var elapsed = DateTime.Now - startTime;
                if (progressInfo.CurrentStep > 1 && progressInfo.TotalSteps > 0)
                {
                    var averageTimePerStep = elapsed.TotalSeconds / (progressInfo.CurrentStep - 1);
                    var remainingSteps = progressInfo.TotalSteps - progressInfo.CurrentStep;
                    var estimatedRemaining = TimeSpan.FromSeconds(averageTimePerStep * remainingSteps);
                    
                    if (estimatedRemaining.TotalMinutes >= 1)
                    {
                        MpcFillTimeRemaining = $"~{estimatedRemaining.Minutes}m {estimatedRemaining.Seconds}s remaining";
                    }
                    else if (estimatedRemaining.TotalSeconds >= 5)
                    {
                        MpcFillTimeRemaining = $"~{estimatedRemaining.Seconds}s remaining";
                    }
                    else
                    {
                        MpcFillTimeRemaining = "Almost done...";
                    }
                }

                // Log progress for debugging too
                
                _logger.LogDebug($"MPC Fill Progress: {progressInfo.PercentageComplete:F0}% - {progressInfo.CurrentOperation} - {progressInfo.CurrentCardName}");
            });

            
            try
            {
                newCards = await _mpcFillService.LoadCardsFromXmlAsync(fileName, progress);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("missing 'fronts' element"))
            {
                // Handle specific XML format errors
                lastError = ex;
                await _errorHandler.ShowErrorAsync(
                    "Invalid XML Format",
                    "The selected file is not a valid MPC Fill XML file. Please ensure you're using an XML file exported from MPC Fill.",
                    ErrorSeverity.Error,
                    ex);
                
            }
            catch (XmlException ex)
            {
                // Handle XML parsing errors
                lastError = ex;
                await _errorHandler.ShowErrorAsync(
                    "XML Parse Error",
                    "The selected XML file is corrupted or contains invalid XML. Please try exporting the file again from MPC Fill.",
                    ErrorSeverity.Error,
                    ex);
                
            }
            catch (FileNotFoundException ex)
            {
                // Handle file not found
                lastError = ex;
                await _errorHandler.ShowErrorAsync(
                    "File Not Found",
                    $"The XML file could not be found: {Path.GetFileName(fileName)}",
                    ErrorSeverity.Error,
                    ex);
                
            }
            catch (UnauthorizedAccessException ex)
            {
                // Handle permission errors
                lastError = ex;
                await _errorHandler.ShowErrorAsync(
                    "Access Denied",
                    $"Cannot read the XML file. Please check that you have permission to access: {Path.GetFileName(fileName)}",
                    ErrorSeverity.Error,
                    ex);
                
            }
            catch (HttpRequestException ex)
            {
                // Handle network errors during image downloads
                lastError = ex;
                await _errorHandler.ShowRecoverableErrorAsync(
                    "Network Error",
                    "Failed to download card images from MPC Fill. This might be due to network connectivity issues.",
                    "Retry download",
                    async () => await ProcessMPCFillXML(fileName));
                
            }
            catch (Exception ex)
            {
                // Handle all other errors
                lastError = ex;
                await _errorHandler.HandleExceptionAsync(ex, 
                    "An unexpected error occurred while loading the MPC Fill XML file. Please check the file format and try again.", 
                    "ProcessMPCFillXML");
                
            }
            
            bool isSuccess = newCards != null && newCards.Count > 0 && lastError == null;

            if (isSuccess)
            {
                // Add to UI collection
                foreach (var card in newCards)
                {
                    card.EditMeCommand = EditCardCommand;
                    Cards.AddCard(card);
                }

                // Update final status
                MpcFillProgress = 100;
                MpcFillStatus = $"Successfully loaded {newCards.Count} cards!";
                MpcFillCurrentOperation = $"Added {newCards.Count} cards to collection";
                MpcFillTimeRemaining = "";

                // Refresh UI
                PrintViewModel?.RefreshCardInfo();
                PrintViewModel?.GeneratePreviewCommand.Execute(null);

               
                _logger.LogInformation($"Successfully loaded {newCards.Count} cards from MPC Fill XML");
                // Hide progress after a delay
                await Task.Delay(2000);
                ShowMpcFillProgress = false;
            }
            else
            {
                // ✅ Handle error case - update UI to show failure
                MpcFillStatus = "Import failed";
                MpcFillProgress = 0;
                MpcFillCurrentOperation = "Import failed";
                MpcFillTimeRemaining = "";
            
                _logger.LogWarning("MPC Fill import failed: {ErrorMessage}", lastError?.Message ?? "Unknown error");
            
                // Hide progress after error delay
                await Task.Delay(3000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ProcessMPCFillXML");
        
            // Final catch-all for any errors we missed
           
            // Show error in progress UI
            MpcFillStatus = $"Error: {ex.Message}";
            MpcFillProgress = 0;
            MpcFillCurrentOperation = "Import failed";
            MpcFillTimeRemaining = "";
            
            // Hide progress after error delay
            await Task.Delay(5000);
            ShowMpcFillProgress = false;
        }
        finally
        {
            // ✅ Always cleanup, regardless of success or failure
            ShowMpcFillProgress = false;
            IsBusy = false;
            IsLoadingMpcFill = false;
        
            _logger.LogDebug("ProcessMPCFillXML cleanup completed");
        }
    }
    
    // Add this RelayCommand to your MainViewModel

    [RelayCommand]
    private async Task LoadMpcFillXmlAsync()
    {
        try
        {
            // Get the top-level window for the file dialog
            var topLevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null;

            if (topLevel == null) return;

            // Show file picker
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select MPC Fill XML File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("XML Files")
                    {
                        Patterns = new[] { "*.xml" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                var filePath = file.TryGetLocalPath();
            
                if (!string.IsNullOrEmpty(filePath))
                {
                    await ProcessMPCFillXML(filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical($"Error in LoadMpcFillXmlAsync: {ex.Message}");
            await _errorHandler.HandleExceptionAsync(ex, "Error loading MPC Fill XML file", "LoadMpcFillXmlAsync");
        }
    }
    
    [RelayCommand]
    private async Task LoadSingleImage()
    {
        IsLoadingSingleImage = true;
        try
        {
            // Get the top-level window for the file dialog
            var topLevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null;

            if (topLevel == null) return;

            // Show file picker
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Image File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png",  }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                var filePath = file.TryGetLocalPath();
            
                if (!string.IsNullOrEmpty(filePath))
                {
                    using var stream = await file.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    var imageData = memoryStream.ToArray();
                            
                    await ProcessImageFileAsync(imageData, file.Name);
                    
                }
            }
        }
        catch (Exception ex)
        {
            IsLoadingSingleImage = false;
            _logger.LogCritical($"Error in LoadSingleImage: {ex.Message}");
            await _errorHandler.HandleExceptionAsync(ex,"Error loading single image", "LoadSingleImage");
        }
        IsLoadingSingleImage=false;
    }
    
    

    
}