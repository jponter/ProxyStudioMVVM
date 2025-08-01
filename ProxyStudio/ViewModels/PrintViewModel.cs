using System;

using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using ProxyStudio.Services;
using Serilog;


//description: ViewModel for handling PDF generation and preview functionality in ProxyStudio.
// debug has been changed to using Microsoft.Extensions.Logging for better performance and flexibility.


namespace ProxyStudio.ViewModels
{
    public partial class PrintViewModel : ViewModelBase
    {
        private readonly IPdfGenerationService _pdfService;
        private readonly IConfigManager _configManager;
        private readonly CardCollection _cards;
        private readonly ILogger<PrintViewModel> _logger;
        private readonly IErrorHandlingService _errorHandler;

        // NEW: Flag to prevent saving during initialization
        private bool _isInitializing = true;

        // Preview
        [ObservableProperty] private Bitmap? _previewImage;
        [ObservableProperty] private bool _isGeneratingPreview;
        [ObservableProperty] private bool _isGeneratingPdf;
        [ObservableProperty] private decimal _previewZoom = 100m;
        
        // Computed properties for actual preview dimensions (affects layout)
        public double ActualPreviewWidth => PreviewImage?.PixelSize.Width * PreviewScale ?? 400;
        public double ActualPreviewHeight => PreviewImage?.PixelSize.Height * PreviewScale ?? 300;
        
        // Multi-page preview support
        [ObservableProperty] private int _currentPreviewPage = 1;
        [ObservableProperty] private int _totalPreviewPages = 1;

        // PDF Settings - bound to UI (using decimal for NumericUpDown compatibility)
        [ObservableProperty] private decimal _cardsPerRow;
        [ObservableProperty] private decimal _cardsPerColumn;
        [ObservableProperty] private decimal _cardSpacing;
        [ObservableProperty] private bool _showCuttingLines;
        [ObservableProperty] private string _cuttingLineColor;
        [ObservableProperty] private bool _isCuttingLineDashed;
        [ObservableProperty] private decimal _cuttingLineExtension;
        [ObservableProperty] private decimal _cuttingLineThickness;
        [ObservableProperty] private decimal _previewDpi;
        [ObservableProperty] private decimal _previewQuality;
        [ObservableProperty] private string _selectedPageSize;
        [ObservableProperty] private bool _isPortrait;
        
        // Print Resolution Settings
        [ObservableProperty] private decimal _printDpi;
        [ObservableProperty] private bool _ensureMinimumPrintDpi;

        // Progress reporting properties
        [ObservableProperty] private string _pdfGenerationStatus = "";
        [ObservableProperty] private double _pdfGenerationProgress = 0.0;
        [ObservableProperty] private string _currentOperation = "";
        [ObservableProperty] private string _timeRemaining = "";
        [ObservableProperty] private bool _showProgressDetails = false;
        [ObservableProperty] private int _cardCount = 0;
        [ObservableProperty] private double _estimatedFileSize = 0.0;

        // Available options
        public ObservableCollection<string> PageSizes { get; } = new()
        {
            "A4", "A3", "A5", "Letter", "Legal", "Tabloid"
        };

        public ObservableCollection<string> PredefinedColors { get; } = new()
        {
            "#000000", "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF"
        };

        // Common print DPI options
        public ObservableCollection<int> CommonPrintDpiOptions { get; } = new()
        {
            150, 300, 600, 1200
        };

        public PrintViewModel(IPdfGenerationService pdfService, IConfigManager configManager, CardCollection cards, ILogger<PrintViewModel> logger, IErrorHandlingService errorHandlingService)
        {
            _pdfService = pdfService;
            _configManager = configManager;
            _cards = cards;
            _logger = logger;
            _errorHandler = errorHandlingService;

            _logger.BeginScope("PrintViewModel Initialization");
            _logger.LogDebug($"PrintViewModel constructor: Received {cards?.Count ?? 0} cards");

            // Set minimal defaults (will be overridden by LoadSettings)
            CardsPerRow = 3;
            CardsPerColumn = 3;
            CardSpacing = 0m;  // CHANGED: was 10m
            ShowCuttingLines = true;
            CuttingLineColor = "#FF0000";  // CHANGED: was "#000000"
            IsCuttingLineDashed = false;
            CuttingLineExtension = 10m;
            CuttingLineThickness = 1;  // CHANGED: was 0.5f
            PreviewDpi = 150;
            PreviewQuality = 85;
            SelectedPageSize = "A4";
            IsPortrait = true;

            // Initialize print resolution with defaults (will be overridden by LoadSettings)
            PrintDpi = 300m;
            EnsureMinimumPrintDpi = false;  // CHANGED: was true

            // Initialize computed properties with defaults
            CardCount = 0;
            EstimatedFileSize = 0.0;

            _logger.LogDebug("PrintViewModel constructor: Set default values, about to load settings...");

            // Load actual settings from config (this will override the defaults above)
            
            
            // IMPORTANT: Enable saving after initialization is complete
            _isInitializing = false;
            _logger.LogDebug("PrintViewModel initialization complete - config saving now enabled");
            
            _logger.LogDebug($"Loading Config");
            // Load settings from config
            LoadSettings();
            _logger.LogDebug("Config loaded successfully");
        }

        // Constructor for design-time support
        public PrintViewModel(IConfigManager configManager) : this(new DesignTimePdfService(), configManager, new CardCollection(), null!, null!)
        {
            // Design-time constructor that creates a mock PDF service
        }

        // Simple design-time PDF service
        private class DesignTimePdfService : IPdfGenerationService
        {
            public Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options, IProgress<PdfGenerationProgress>? progress = null)
            {
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
            // Return a placeholder image for design-time
                return Task.FromResult<Bitmap>(null!);
            }
        }

        private void LoadSettings()
        {
            var settings = _configManager.Config.PdfSettings;
            
            _logger.LogDebug($"LoadSettings: Loading from config file...");
            _logger.LogDebug($"Config values - PrintDpi={settings.PrintDpi}, EnsureMinimum={settings.EnsureMinimumPrintDpi}");
            _logger.LogDebug($"Config values - CardsPerRow={settings.CardsPerRow}, CardsPerColumn={settings.CardsPerColumn}");
            _logger.LogDebug($"Config values - CardSpacing={settings.CardSpacing}, ShowCuttingLines={settings.ShowCuttingLines}");
            _logger.LogDebug($"Config values - IsPortrait={settings.IsPortrait}, PageSize={settings.PageSize}");
            
            // Load ALL settings from config (not just some)
            CardsPerRow = Math.Max(1, Math.Min(settings.CardsPerRow, 5));
            CardsPerColumn = Math.Max(1, Math.Min(settings.CardsPerColumn, 5));
            CardSpacing = Math.Max(0, (decimal)settings.CardSpacing);
            ShowCuttingLines = settings.ShowCuttingLines;
            CuttingLineColor = string.IsNullOrEmpty(settings.CuttingLineColor) ? "#FF0000" : settings.CuttingLineColor;
            IsCuttingLineDashed = settings.IsCuttingLineDashed;
            CuttingLineExtension = Math.Max(0, (decimal)settings.CuttingLineExtension);
            CuttingLineThickness = Math.Max(0.1m, (decimal)settings.CuttingLineThickness);
            PreviewDpi = Math.Max(72, Math.Min(settings.PreviewDpi, 300));
            PreviewQuality = Math.Max(1, Math.Min(settings.PreviewQuality, 100));
            SelectedPageSize = string.IsNullOrEmpty(settings.PageSize) ? "A4" : settings.PageSize;
            IsPortrait = settings.IsPortrait;
            
            // IMPORTANT: Load print resolution settings (these were missing!)
            PrintDpi = Math.Max(150m, Math.Min((decimal)settings.PrintDpi, 1200m));
            EnsureMinimumPrintDpi = settings.EnsureMinimumPrintDpi;
            
            // Update computed properties after loading all settings
            UpdateCardCount();
            UpdateEstimatedFileSize();
            
            _logger.LogDebug($"LoadSettings completed:");
            _logger.LogDebug($"  Layout: {CardsPerRow}x{CardsPerColumn}, Spacing: {CardSpacing}");
            _logger.LogDebug($"  Print: {PrintDpi} DPI, MinEnforced: {EnsureMinimumPrintDpi}");
            _logger.LogDebug($"  Cutting: {ShowCuttingLines}, Color: {CuttingLineColor}, Extension: {CuttingLineExtension}");
            _logger.LogDebug($"  Page: {SelectedPageSize} {(IsPortrait ? "Portrait" : "Landscape")}");
            _logger.LogDebug($"  Preview: {PreviewDpi} DPI, Quality: {PreviewQuality}");
            _logger.LogDebug($"  Cards: {CardCount}, EstimatedSize: {EstimatedFileSize:F2} MB");
            _logger.LogDebug($"  PrintDpi: {PrintDpi}, EnsureMinPrintDpi: {EnsureMinimumPrintDpi}");
            
            // Auto-generate preview on startup
            _ = GeneratePreviewAsync();
        }

        private void SaveSettings()
        {
            // Don't save during initialization
            if (_isInitializing)
            {
                _logger.LogDebug("SaveSettings: Skipped during initialization");
                return;
            }
            
            var settings = _configManager.Config.PdfSettings;
            
            // Convert decimal back to the original types
            // settings.CardsPerRow = (int)CardsPerRow;
            // settings.CardsPerColumn = (int)CardsPerColumn;
            settings.CardSpacing = (float)CardSpacing;
            settings.ShowCuttingLines = ShowCuttingLines;
            settings.CuttingLineColor = CuttingLineColor;
            settings.IsCuttingLineDashed = IsCuttingLineDashed;
            settings.CuttingLineExtension = (float)CuttingLineExtension;
            settings.CuttingLineThickness = (float)CuttingLineThickness;
            settings.PreviewDpi = (int)PreviewDpi;
            settings.PreviewQuality = (int)PreviewQuality;
            settings.PageSize = SelectedPageSize;
            settings.IsPortrait = IsPortrait;
            
            // Save print resolution settings
            settings.PrintDpi = (int)PrintDpi;
            settings.EnsureMinimumPrintDpi = EnsureMinimumPrintDpi;
            
            _configManager.SaveConfig();
            
            _logger.LogDebug($"SaveSettings: Saved to config - PrintDpi={settings.PrintDpi}, IsPortrait={settings.IsPortrait}, CardSpacing={settings.CardSpacing}");
        }

        private PdfGenerationOptions CreateOptions()
        {
            // Ensure minimum DPI if setting is enabled
            var actualPrintDpi = EnsureMinimumPrintDpi ? 
                Math.Max((int)PrintDpi, _configManager.Config.PdfSettings.MinimumPrintDpi) : 
                (int)PrintDpi;
        
            _logger.LogDebug($"Creating PDF options with PrintDpi={actualPrintDpi} (requested: {PrintDpi}, minimum enforced: {EnsureMinimumPrintDpi})");
    
            // Use fixed layout based on orientation
            var actualCardsPerRow = IsPortrait ? 3 : 4;
            var actualCardsPerColumn = IsPortrait ? 3 : 2;
    
            return new PdfGenerationOptions
            {
                IsPortrait = IsPortrait,
                PageSize = SelectedPageSize,
                CardsPerRow = actualCardsPerRow,        // FIXED: Use calculated layout
                CardsPerColumn = actualCardsPerColumn,  // FIXED: Use calculated layout
                CardSpacing = (float)CardSpacing,
                ShowCuttingLines = ShowCuttingLines,
                CuttingLineColor = CuttingLineColor,
                IsCuttingLineDashed = IsCuttingLineDashed,
                CuttingLineExtension = (float)CuttingLineExtension,
                CuttingLineThickness = (float)CuttingLineThickness,
                PreviewDpi = (int)PreviewDpi,
                PreviewQuality = (int)PreviewQuality,
                PrintDpi = actualPrintDpi
            };
        }

        private void UpdateCardCount()
        {
            var newCount = _cards?.Count ?? 0;
            CardCount = newCount;
            _logger.LogDebug($"UpdateCardCount: Set to {newCount} cards");
        }
        
        private void UpdateEstimatedFileSize()
{
    if (_cards == null || _cards.Count == 0)
    {
        EstimatedFileSize = 0;
        _logger.LogDebug($"UpdateEstimatedFileSize: No cards, setting to 0");
        return;
    }
    
    // UPDATED: Real-world data from actual PDF generation
    // Previous estimate was based on theoretical calculations
    // New data from actual generation:
    // 600 DPI: 39.31 MB (9 cards) = 4.37 MB per card
    // We need to extrapolate other DPI values based on this real data
    
    var dpiValue = (double)PrintDpi;
    var cardCount = _cards.Count;
    
    double mbPerCard;
    
    // File size scales roughly with the SQUARE of DPI (pixel count)
    // Using 600 DPI as our reference point: 4.37 MB per card
    
    if (dpiValue <= 150)
    {
        // Very low DPI: approximately 1/16th the size of 600 DPI
        var ratio = Math.Pow(dpiValue / 600.0, 2.0);
        mbPerCard = 4.37 * ratio; // Will be around 0.27 MB per card at 150 DPI
    }
    else if (dpiValue <= 300)
    {
        // Standard print DPI: approximately 1/4th the size of 600 DPI
        var ratio = Math.Pow(dpiValue / 600.0, 2.0);
        mbPerCard = 4.37 * ratio; // Will be around 1.09 MB per card at 300 DPI
    }
    else if (dpiValue <= 600)
    {
        // Linear interpolation from 300 DPI to our known 600 DPI point
        var ratio300 = Math.Pow(300.0 / 600.0, 2.0); // 0.25
        var size300 = 4.37 * ratio300; // ~1.09 MB
        
        var t = (dpiValue - 300) / (600 - 300); // 0 to 1
        mbPerCard = size300 + t * (4.37 - size300); // Interpolate to 4.37 MB at 600 DPI
    }
    else if (dpiValue <= 1200)
    {
        // Extrapolate above 600 DPI: file size continues to scale quadratically
        var ratio = Math.Pow(dpiValue / 600.0, 2.0);
        mbPerCard = 4.37 * ratio; // Will be around 17.48 MB per card at 1200 DPI
    }
    else
    {
        // Very high DPI: continue quadratic scaling with slight efficiency penalty
        var ratio = Math.Pow(dpiValue / 600.0, 2.2); // Slightly steeper curve for very high DPI
        mbPerCard = 4.37 * ratio;
    }
    
    var totalMB = mbPerCard * cardCount;
    EstimatedFileSize = Math.Max(0.1, totalMB);
    
    _logger.LogDebug($"UpdateEstimatedFileSize: {cardCount} cards at {PrintDpi} DPI");
    _logger.LogDebug($"  Using REAL data: 600 DPI = 4.37 MB per card (from 39.31 MB / 9 cards)");
    _logger.LogDebug($"  Calculated ratio for {PrintDpi} DPI: {Math.Pow(dpiValue / 600.0, 2.0):F3}");
    _logger.LogDebug($"  Per card: {mbPerCard:F2} MB, Total: {EstimatedFileSize:F1} MB");
    
    // Show accuracy predictions for common DPI values
    if (Math.Abs(dpiValue - 150) < 1) 
        _logger.LogDebug($"  📊 150 DPI estimate: {totalMB:F1} MB (theoretical)");
    else if (Math.Abs(dpiValue - 300) < 1) 
        _logger.LogDebug($"  📊 300 DPI estimate: {totalMB:F1} MB (should be ~1/4 of 600 DPI)");
    else if (Math.Abs(dpiValue - 600) < 1) 
        _logger.LogDebug($"  ✅ 600 DPI estimate: {totalMB:F1} MB (based on REAL data: 39.31 MB for 9 cards)");
    else if (Math.Abs(dpiValue - 1200) < 1) 
        _logger.LogDebug($"  📊 1200 DPI estimate: {totalMB:F1} MB (should be ~4x of 600 DPI = ~157 MB for 9 cards)");
}

        // Update the existing RefreshCardInfo method to also clear preview when no cards
        public void RefreshCardInfo()
        {
            _logger.LogDebug($"RefreshCardInfo called - cards collection has {_cards?.Count ?? 0} cards");
            UpdateCardCount();
            UpdateEstimatedFileSize();
    
            // If no cards, clear the preview
            if (CardCount == 0)
            {
                ClearPreview();
                _logger.LogDebug("RefreshCardInfo: No cards found, cleared preview");
            }
    
            _logger.LogDebug($"RefreshCardInfo completed - CardCount={CardCount}, EstimatedFileSize={EstimatedFileSize:F2} MB");
        }

        [RelayCommand]
        private async Task GeneratePreviewAsync()
        {
            using var scope = _logger.BeginScope("Generate Preview Async");
            if (IsGeneratingPreview || _cards.Count == 0) 
            {
                _logger.LogDebug($"Cannot generate preview - IsGeneratingPreview: {IsGeneratingPreview}, Cards count: {_cards.Count}");
                return;
            }

            IsGeneratingPreview = true;
            try
            {
                UpdatePageInfo();
        
                _logger.LogDebug($"Starting preview generation with {_cards.Count} cards (Page {CurrentPreviewPage} of {TotalPreviewPages})");
                var options = CreateOptions();
                _logger.LogDebug($"Created options - PrintDpi: {options.PrintDpi}, PreviewDpi: {options.PreviewDpi}, Layout: {options.CardsPerRow}x{options.CardsPerColumn}");
        
                // Get cards for current preview page using FIXED layout
                var cardsPerPage = options.CardsPerRow * options.CardsPerColumn; // Use options layout
                var startIndex = (CurrentPreviewPage - 1) * cardsPerPage;
                var pageCards = new CardCollection();
                pageCards.AddRange(_cards.Skip(startIndex).Take(cardsPerPage));
        
                PreviewImage = await _pdfService.GeneratePreviewImageAsync(pageCards, options);
                _logger.LogDebug($"Preview generation completed. Image is null: {PreviewImage == null}");
        
                SaveSettings();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleExceptionAsync(ex, "Error generating preview", "An error occurred while generating the preview image.");
                _logger.LogError($"Error generating preview: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                IsGeneratingPreview = false;
            }
        }

        [RelayCommand]
        private async Task GeneratePdfAsync()
        {
            using var scope = _logger.BeginScope("Generate PDF Async");
            
            if (IsGeneratingPdf || _cards.Count == 0) 
            {
                _logger.LogDebug($"Cannot generate PDF - IsGeneratingPdf: {IsGeneratingPdf}, Cards count: {_cards.Count}");
                return;
            }

            IsGeneratingPdf = true;
            ShowProgressDetails = true;
            PdfGenerationProgress = 0;
            PdfGenerationStatus = "Starting PDF generation...";
            CurrentOperation = "";
            TimeRemaining = "";

            try
            {
                var options = CreateOptions();
                _logger.LogDebug($"Starting PDF generation with {_cards.Count} cards at {options.PrintDpi} DPI");
                
                // Create progress reporter
                var progress = new Progress<PdfGenerationProgress>(progressInfo =>
                {
                    // Update UI on the main thread
                    PdfGenerationProgress = progressInfo.PercentageComplete;
                    PdfGenerationStatus = $"{progressInfo.PercentageComplete:F0}% - {progressInfo.StatusMessage}";
                    CurrentOperation = progressInfo.CurrentOperation;
                    
                    if (progressInfo.EstimatedRemainingTime.HasValue && progressInfo.EstimatedRemainingTime.Value.TotalSeconds > 1)
                    {
                        var remaining = progressInfo.EstimatedRemainingTime.Value;
                        if (remaining.TotalMinutes >= 1)
                        {
                            TimeRemaining = $"~{remaining.Minutes}m {remaining.Seconds}s remaining";
                        }
                        else
                        {
                            TimeRemaining = $"~{remaining.Seconds}s remaining";
                        }
                    }
                    else if (progressInfo.PercentageComplete >= 99)
                    {
                        TimeRemaining = "Almost done...";
                    }
                    
                    _logger.LogDebug($"Progress: {progressInfo.PercentageComplete:F1}% - {progressInfo.CurrentOperation} - {progressInfo.CurrentCardName}");
                });
                
                var pdfBytes = await _pdfService.GeneratePdfAsync(_cards, options, progress);
                
                PdfGenerationStatus = $"PDF generated successfully! ({pdfBytes.Length / (1024.0 * 1024.0):F2} MB)";
                PdfGenerationProgress = 100;
                CurrentOperation = "Saving file...";
                TimeRemaining = "";
                
                _logger.LogDebug($"PDF generation completed at {options.PrintDpi} DPI. Size: {pdfBytes.Length} bytes");
                
                var settings = _configManager.Config.PdfSettings;
                var fileName = $"{settings.DefaultFileName}_{DateTime.Now:yyyyMMdd_HHmmss}_{options.PrintDpi}DPI.pdf";
                var filePath = Path.Combine(settings.DefaultOutputPath, fileName);
                
                await File.WriteAllBytesAsync(filePath, pdfBytes);
                
                PdfGenerationStatus = $"PDF saved successfully! ({pdfBytes.Length / (1024.0 * 1024.0):F2} MB)";
                CurrentOperation = $"Saved to: {Path.GetFileName(filePath)}";
                
                _logger.LogDebug($"PDF saved to: {filePath}");
                SaveSettings();
                
                // Hide progress after a delay
                await Task.Delay(3000);
                ShowProgressDetails = false;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleExceptionAsync(ex, "Error generating PDF", "An error occurred while generating the PDF file.");
                _logger.LogCritical($"Error generating PDF: {ex.Message}");
                _logger.LogCritical($"Stack trace: {ex.StackTrace}");
                
                PdfGenerationStatus = $"Error: {ex.Message}";
                PdfGenerationProgress = 0;
                CurrentOperation = "PDF generation failed";
                TimeRemaining = "";
                
                // Hide progress after error delay
                await Task.Delay(5000);
                ShowProgressDetails = false;
            }
            finally
            {
                IsGeneratingPdf = false;
            }
        }

        [RelayCommand]
        private async Task ZoomInAsync()
        {
            if (PreviewZoom < 300m)
            {
                PreviewZoom = Math.Min(300m, PreviewZoom + 25m);
                _logger.LogDebug($"Zoomed in to {PreviewZoom}%");
            }
        }

        [RelayCommand]
        private async Task ZoomOutAsync()
        {
            if (PreviewZoom > 25m)
            {
                PreviewZoom = Math.Max(25m, PreviewZoom - 25m);
                _logger.LogDebug($"Zoomed out to {PreviewZoom}%");
            }
        }

        [RelayCommand]
        private async Task ResetZoomAsync()
        {
            PreviewZoom = 100m;
            _logger.LogDebug("Reset zoom to 100%");
        }

        [RelayCommand]
        private async Task SavePdfAsAsync()
        {
            if (IsGeneratingPdf || _cards.Count == 0) return;
            await GeneratePdfAsync();
        }

        // Computed property for ScaleTransform
        public double PreviewScale => (double)PreviewZoom / 100.0;

        [RelayCommand]
        private async Task PreviousPageAsync()
        {
            if (CurrentPreviewPage > 1)
            {
                CurrentPreviewPage--;
                await GeneratePreviewAsync();
            }
        }

        [RelayCommand]
        private async Task NextPageAsync()
        {
            if (CurrentPreviewPage < TotalPreviewPages)
            {
                CurrentPreviewPage++;
                await GeneratePreviewAsync();
            }
        }

        private void UpdatePageInfo()
        {
            // Use fixed layout based on orientation
            var actualCardsPerRow = IsPortrait ? 3 : 4;
            var actualCardsPerColumn = IsPortrait ? 3 : 2;
            var cardsPerPage = actualCardsPerRow * actualCardsPerColumn;
    
            TotalPreviewPages = cardsPerPage == 0 ? 1 : (int)Math.Ceiling((double)_cards.Count / cardsPerPage);
    
            if (CurrentPreviewPage > TotalPreviewPages)
                CurrentPreviewPage = Math.Max(1, TotalPreviewPages);
        
            _logger.LogDebug($"Updated page info: Page {CurrentPreviewPage} of {TotalPreviewPages} (Fixed layout: {actualCardsPerRow}x{actualCardsPerColumn})");
        }

        // Command for cancellation (future enhancement)
        public bool CanCancelPdfGeneration => IsGeneratingPdf;
        
        [RelayCommand(CanExecute = nameof(CanCancelPdfGeneration))]
        private void CancelPdfGeneration()
        {
            _logger.LogDebug("PDF generation cancellation requested (not yet implemented)");
        }

        // Update property change handlers to not affect layout
        partial void OnCardsPerRowChanged(decimal value)
        {
            _logger.LogDebug($"OnCardsPerRowChanged: {value} (initializing: {_isInitializing}) - NOTE: Using fixed layout, this setting has no effect");
            // Remove preview regeneration since layout is fixed
            if (!_isInitializing)
            {
                SaveSettings(); // Keep for compatibility but doesn't affect layout
            }
        }

        partial void OnCardsPerColumnChanged(decimal value)
        {
            _logger.LogDebug($"OnCardsPerColumnChanged: {value} (initializing: {_isInitializing}) - NOTE: Using fixed layout, this setting has no effect");
            // Remove preview regeneration since layout is fixed
            if (!_isInitializing)
            {
                SaveSettings(); // Keep for compatibility but doesn't affect layout
            }
        }

        partial void OnCardSpacingChanged(decimal value)
        {
            _logger.LogDebug($"OnCardSpacingChanged: {value} (initializing: {_isInitializing})");
            if (value >= 0 && !_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnShowCuttingLinesChanged(bool value)
        {
            _logger.LogDebug($"OnShowCuttingLinesChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnCuttingLineColorChanged(string value)
        {
            _logger.LogDebug($"OnCuttingLineColorChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnIsCuttingLineDashedChanged(bool value)
        {
            _logger.LogDebug($"OnIsCuttingLineDashedChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnCuttingLineExtensionChanged(decimal value)
        {
            _logger.LogDebug($"OnCuttingLineExtensionChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnCuttingLineThicknessChanged(decimal value)
        {
            _logger.LogDebug($"OnCuttingLineThicknessChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnPreviewDpiChanged(decimal value)
        {
            _logger.LogDebug($"OnPreviewDpiChanged: {value} (initializing: {_isInitializing})");
            if (value >= 72 && value <= 300 && !_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnPreviewQualityChanged(decimal value)
        {
            _logger.LogDebug($"OnPreviewQualityChanged: {value} (initializing: {_isInitializing})");
            if (value >= 1 && value <= 100 && !_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnSelectedPageSizeChanged(string value)
        {
            _logger.LogDebug($"OnSelectedPageSizeChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnIsPortraitChanged(bool value)
        {
            _logger.LogDebug($"OnIsPortraitChanged: {value} (initializing: {_isInitializing}) - This affects fixed layout");
            if (!_isInitializing)
            {
                CurrentPreviewPage = 1; // Reset to page 1 when layout changes
                _ = GeneratePreviewAsync(); // Regenerate because layout actually changed
                SaveSettings();
            }
        }

        partial void OnPrintDpiChanged(decimal value)
        {
            _logger.LogDebug($"OnPrintDpiChanged: {value} (initializing: {_isInitializing})");
            UpdateEstimatedFileSize(); // Always update estimate
            
            if (value >= 150m && value <= 1200m && !_isInitializing)
            {
                SaveSettings();
            }
        }

        partial void OnEnsureMinimumPrintDpiChanged(bool value)
        {
            _logger.LogDebug($"OnEnsureMinimumPrintDpiChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                SaveSettings();
            }
        }

        partial void OnPreviewZoomChanged(decimal value)
        {
            _logger.LogDebug($"OnPreviewZoomChanged: {value}%");
            OnPropertyChanged(nameof(PreviewScale));
            OnPropertyChanged(nameof(ActualPreviewWidth));   // Updated property name
            OnPropertyChanged(nameof(ActualPreviewHeight));  // Updated property name
        }

// Also update when PreviewImage changes
        partial void OnPreviewImageChanged(Bitmap? value)
        {
            OnPropertyChanged(nameof(ActualPreviewWidth));   // Updated property name
            OnPropertyChanged(nameof(ActualPreviewHeight));  // Updated property name
            _logger.LogDebug($"PreviewImage changed. New size: {value?.PixelSize.Width}x{value?.PixelSize.Height}");
        }
        
        
        // Add this method to your PrintViewModel.cs class

        /// <summary>
        /// Clears the preview and resets preview-related properties
        /// </summary>
        public void ClearPreview()
        {
            _logger.LogDebug("ClearPreview: Clearing preview image and resetting preview state");
    
            PreviewImage = null;
            CurrentPreviewPage = 1;
            TotalPreviewPages = 1;
    
            // Also clear any generation status
            PdfGenerationStatus = "";
            PdfGenerationProgress = 0.0;
            CurrentOperation = "";
            TimeRemaining = "";
            ShowProgressDetails = false;
    
            _logger.LogDebug("ClearPreview: Preview cleared successfully");
        }
        
        
    }
}