using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using ProxyStudio.Services;

namespace ProxyStudio.ViewModels
{
    public partial class PrintViewModel : ViewModelBase
    {
        private readonly IPdfGenerationService _pdfService;
        private readonly IConfigManager _configManager;
        private readonly CardCollection _cards;

        // NEW: Flag to prevent saving during initialization
        private bool _isInitializing = true;

        // Preview
        [ObservableProperty] private Bitmap? _previewImage;
        [ObservableProperty] private bool _isGeneratingPreview;
        [ObservableProperty] private bool _isGeneratingPdf;
        [ObservableProperty] private decimal _previewZoom = 100m;
        
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

        public PrintViewModel(IPdfGenerationService pdfService, IConfigManager configManager, CardCollection cards)
        {
            _pdfService = pdfService;
            _configManager = configManager;
            _cards = cards;

            DebugHelper.WriteDebug($"PrintViewModel constructor: Received {cards?.Count ?? 0} cards");

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

            DebugHelper.WriteDebug("PrintViewModel constructor: Set default values, about to load settings...");

            // Load actual settings from config (this will override the defaults above)
            LoadSettings();
            
            // IMPORTANT: Enable saving after initialization is complete
            _isInitializing = false;
            DebugHelper.WriteDebug("PrintViewModel initialization complete - config saving now enabled");
        }

        // Constructor for design-time support
        public PrintViewModel(IConfigManager configManager) : this(new DesignTimePdfService(), configManager, new CardCollection())
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
                return Task.FromResult<Bitmap>(null!);
            }
        }

        private void LoadSettings()
        {
            var settings = _configManager.Config.PdfSettings;
            
            DebugHelper.WriteDebug($"LoadSettings: Loading from config file...");
            DebugHelper.WriteDebug($"Config values - PrintDpi={settings.PrintDpi}, EnsureMinimum={settings.EnsureMinimumPrintDpi}");
            DebugHelper.WriteDebug($"Config values - CardsPerRow={settings.CardsPerRow}, CardsPerColumn={settings.CardsPerColumn}");
            DebugHelper.WriteDebug($"Config values - CardSpacing={settings.CardSpacing}, ShowCuttingLines={settings.ShowCuttingLines}");
            DebugHelper.WriteDebug($"Config values - IsPortrait={settings.IsPortrait}, PageSize={settings.PageSize}");
            
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
            
            DebugHelper.WriteDebug($"LoadSettings completed:");
            DebugHelper.WriteDebug($"  Layout: {CardsPerRow}x{CardsPerColumn}, Spacing: {CardSpacing}");
            DebugHelper.WriteDebug($"  Print: {PrintDpi} DPI, MinEnforced: {EnsureMinimumPrintDpi}");
            DebugHelper.WriteDebug($"  Cutting: {ShowCuttingLines}, Color: {CuttingLineColor}, Extension: {CuttingLineExtension}");
            DebugHelper.WriteDebug($"  Page: {SelectedPageSize} {(IsPortrait ? "Portrait" : "Landscape")}");
            DebugHelper.WriteDebug($"  Preview: {PreviewDpi} DPI, Quality: {PreviewQuality}");
            DebugHelper.WriteDebug($"  Cards: {CardCount}, EstimatedSize: {EstimatedFileSize:F2} MB");
            
            // Auto-generate preview on startup
            _ = GeneratePreviewAsync();
        }

        private void SaveSettings()
        {
            // Don't save during initialization
            if (_isInitializing)
            {
                DebugHelper.WriteDebug("SaveSettings: Skipped during initialization");
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
            
            DebugHelper.WriteDebug($"SaveSettings: Saved to config - PrintDpi={settings.PrintDpi}, IsPortrait={settings.IsPortrait}, CardSpacing={settings.CardSpacing}");
        }

        private PdfGenerationOptions CreateOptions()
        {
            // Ensure minimum DPI if setting is enabled
            var actualPrintDpi = EnsureMinimumPrintDpi ? 
                Math.Max((int)PrintDpi, _configManager.Config.PdfSettings.MinimumPrintDpi) : 
                (int)PrintDpi;
        
            DebugHelper.WriteDebug($"Creating PDF options with PrintDpi={actualPrintDpi} (requested: {PrintDpi}, minimum enforced: {EnsureMinimumPrintDpi})");
    
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
            DebugHelper.WriteDebug($"UpdateCardCount: Set to {newCount} cards");
        }
        
        private void UpdateEstimatedFileSize()
        {
            if (_cards == null || _cards.Count == 0)
            {
                EstimatedFileSize = 0;
                DebugHelper.WriteDebug($"UpdateEstimatedFileSize: No cards, setting to 0");
                return;
            }
            
            // Ultra-accurate estimation using piecewise interpolation of your actual data:
            // 300 DPI: 0.43 MB (8 cards) = 0.054 MB per card
            // 600 DPI: 8.5 MB (8 cards) = 1.063 MB per card  
            // 1200 DPI: 30 MB (8 cards) = 3.75 MB per card
            //
            // Analysis: The relationship is steeper than exponential - use logarithmic interpolation
            
            var dpiValue = (double)PrintDpi;
            var cardCount = _cards.Count;
            
            double mbPerCard;
            
            if (dpiValue <= 300)
            {
                // Below 300 DPI: Linear scaling from a reasonable baseline
                var ratio = dpiValue / 300.0;
                mbPerCard = 0.054 * Math.Pow(ratio, 2.0); // Quadratic for low DPI
            }
            else if (dpiValue <= 600)
            {
                // 300-600 DPI: Interpolate between known points
                var t = (dpiValue - 300) / (600 - 300); // 0 to 1
                var start = 0.054; // 300 DPI per card
                var end = 1.063;   // 600 DPI per card
                mbPerCard = start + t * (end - start);
            }
            else if (dpiValue <= 1200)
            {
                // 600-1200 DPI: Interpolate between known points
                var t = (dpiValue - 600) / (1200 - 600); // 0 to 1
                var start = 1.063; // 600 DPI per card
                var end = 3.75;    // 1200 DPI per card
                mbPerCard = start + t * (end - start);
            }
            else
            {
                // Above 1200 DPI: Extrapolate with steep scaling
                var ratio = dpiValue / 1200.0;
                mbPerCard = 3.75 * Math.Pow(ratio, 3.2); // Steep scaling for very high DPI
            }
            
            var totalMB = mbPerCard * cardCount;
            EstimatedFileSize = Math.Max(0.1, totalMB);
            
            DebugHelper.WriteDebug($"UpdateEstimatedFileSize: {cardCount} cards at {PrintDpi} DPI");
            DebugHelper.WriteDebug($"  Using piecewise interpolation of actual data points");
            DebugHelper.WriteDebug($"  Per card: {mbPerCard:F3} MB, Total: {EstimatedFileSize:F2} MB");
            
            // Show accuracy against known results
            if (Math.Abs(dpiValue - 300) < 1) DebugHelper.WriteDebug($"  ✅ Expected 0.43 MB, Estimated: {totalMB:F2} MB (should be exact)");
            else if (Math.Abs(dpiValue - 600) < 1) DebugHelper.WriteDebug($"  ✅ Expected 8.5 MB, Estimated: {totalMB:F2} MB (should be exact)");
            else if (Math.Abs(dpiValue - 1200) < 1) DebugHelper.WriteDebug($"  ✅ Expected 30 MB, Estimated: {totalMB:F2} MB (should be exact)");
        }

        // Method to refresh card information when cards collection changes
        public void RefreshCardInfo()
        {
            DebugHelper.WriteDebug($"RefreshCardInfo called - cards collection has {_cards?.Count ?? 0} cards");
            UpdateCardCount();
            UpdateEstimatedFileSize();
            DebugHelper.WriteDebug($"RefreshCardInfo completed - CardCount={CardCount}, EstimatedFileSize={EstimatedFileSize:F2} MB");
        }

        [RelayCommand]
        private async Task GeneratePreviewAsync()
        {
            if (IsGeneratingPreview || _cards.Count == 0) 
            {
                DebugHelper.WriteDebug($"Cannot generate preview - IsGeneratingPreview: {IsGeneratingPreview}, Cards count: {_cards.Count}");
                return;
            }

            IsGeneratingPreview = true;
            try
            {
                UpdatePageInfo();
        
                DebugHelper.WriteDebug($"Starting preview generation with {_cards.Count} cards (Page {CurrentPreviewPage} of {TotalPreviewPages})");
                var options = CreateOptions();
                DebugHelper.WriteDebug($"Created options - PrintDpi: {options.PrintDpi}, PreviewDpi: {options.PreviewDpi}, Layout: {options.CardsPerRow}x{options.CardsPerColumn}");
        
                // Get cards for current preview page using FIXED layout
                var cardsPerPage = options.CardsPerRow * options.CardsPerColumn; // Use options layout
                var startIndex = (CurrentPreviewPage - 1) * cardsPerPage;
                var pageCards = new CardCollection();
                pageCards.AddRange(_cards.Skip(startIndex).Take(cardsPerPage));
        
                PreviewImage = await _pdfService.GeneratePreviewImageAsync(pageCards, options);
                DebugHelper.WriteDebug($"Preview generation completed. Image is null: {PreviewImage == null}");
        
                SaveSettings();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error generating preview: {ex.Message}");
                DebugHelper.WriteDebug($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                IsGeneratingPreview = false;
            }
        }

        [RelayCommand]
        private async Task GeneratePdfAsync()
        {
            if (IsGeneratingPdf || _cards.Count == 0) 
            {
                DebugHelper.WriteDebug($"Cannot generate PDF - IsGeneratingPdf: {IsGeneratingPdf}, Cards count: {_cards.Count}");
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
                DebugHelper.WriteDebug($"Starting PDF generation with {_cards.Count} cards at {options.PrintDpi} DPI");
                
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
                    
                    DebugHelper.WriteDebug($"Progress: {progressInfo.PercentageComplete:F1}% - {progressInfo.CurrentOperation} - {progressInfo.CurrentCardName}");
                });
                
                var pdfBytes = await _pdfService.GeneratePdfAsync(_cards, options, progress);
                
                PdfGenerationStatus = $"PDF generated successfully! ({pdfBytes.Length / (1024.0 * 1024.0):F2} MB)";
                PdfGenerationProgress = 100;
                CurrentOperation = "Saving file...";
                TimeRemaining = "";
                
                DebugHelper.WriteDebug($"PDF generation completed at {options.PrintDpi} DPI. Size: {pdfBytes.Length} bytes");
                
                var settings = _configManager.Config.PdfSettings;
                var fileName = $"{settings.DefaultFileName}_{DateTime.Now:yyyyMMdd_HHmmss}_{options.PrintDpi}DPI.pdf";
                var filePath = Path.Combine(settings.DefaultOutputPath, fileName);
                
                await File.WriteAllBytesAsync(filePath, pdfBytes);
                
                PdfGenerationStatus = $"PDF saved successfully! ({pdfBytes.Length / (1024.0 * 1024.0):F2} MB)";
                CurrentOperation = $"Saved to: {Path.GetFileName(filePath)}";
                
                DebugHelper.WriteDebug($"PDF saved to: {filePath}");
                SaveSettings();
                
                // Hide progress after a delay
                await Task.Delay(3000);
                ShowProgressDetails = false;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error generating PDF: {ex.Message}");
                DebugHelper.WriteDebug($"Stack trace: {ex.StackTrace}");
                
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
            if (PreviewZoom < 200m)
            {
                PreviewZoom = Math.Min(200m, PreviewZoom + 25m);
                DebugHelper.WriteDebug($"Zoomed in to {PreviewZoom}%");
            }
        }

        [RelayCommand]
        private async Task ZoomOutAsync()
        {
            if (PreviewZoom > 25m)
            {
                PreviewZoom = Math.Max(25m, PreviewZoom - 25m);
                DebugHelper.WriteDebug($"Zoomed out to {PreviewZoom}%");
            }
        }

        [RelayCommand]
        private async Task ResetZoomAsync()
        {
            PreviewZoom = 100m;
            DebugHelper.WriteDebug("Reset zoom to 100%");
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
        
            DebugHelper.WriteDebug($"Updated page info: Page {CurrentPreviewPage} of {TotalPreviewPages} (Fixed layout: {actualCardsPerRow}x{actualCardsPerColumn})");
        }

        // Command for cancellation (future enhancement)
        public bool CanCancelPdfGeneration => IsGeneratingPdf;
        
        [RelayCommand(CanExecute = nameof(CanCancelPdfGeneration))]
        private void CancelPdfGeneration()
        {
            DebugHelper.WriteDebug("PDF generation cancellation requested (not yet implemented)");
        }

        // Update property change handlers to not affect layout
        partial void OnCardsPerRowChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCardsPerRowChanged: {value} (initializing: {_isInitializing}) - NOTE: Using fixed layout, this setting has no effect");
            // Remove preview regeneration since layout is fixed
            if (!_isInitializing)
            {
                SaveSettings(); // Keep for compatibility but doesn't affect layout
            }
        }

        partial void OnCardsPerColumnChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCardsPerColumnChanged: {value} (initializing: {_isInitializing}) - NOTE: Using fixed layout, this setting has no effect");
            // Remove preview regeneration since layout is fixed
            if (!_isInitializing)
            {
                SaveSettings(); // Keep for compatibility but doesn't affect layout
            }
        }

        partial void OnCardSpacingChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCardSpacingChanged: {value} (initializing: {_isInitializing})");
            if (value >= 0 && !_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnShowCuttingLinesChanged(bool value)
        {
            DebugHelper.WriteDebug($"OnShowCuttingLinesChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnCuttingLineColorChanged(string value)
        {
            DebugHelper.WriteDebug($"OnCuttingLineColorChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnIsCuttingLineDashedChanged(bool value)
        {
            DebugHelper.WriteDebug($"OnIsCuttingLineDashedChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnCuttingLineExtensionChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCuttingLineExtensionChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnCuttingLineThicknessChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCuttingLineThicknessChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnPreviewDpiChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnPreviewDpiChanged: {value} (initializing: {_isInitializing})");
            if (value >= 72 && value <= 300 && !_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnPreviewQualityChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnPreviewQualityChanged: {value} (initializing: {_isInitializing})");
            if (value >= 1 && value <= 100 && !_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnSelectedPageSizeChanged(string value)
        {
            DebugHelper.WriteDebug($"OnSelectedPageSizeChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                _ = GeneratePreviewAsync();
                SaveSettings();
            }
        }

        partial void OnIsPortraitChanged(bool value)
        {
            DebugHelper.WriteDebug($"OnIsPortraitChanged: {value} (initializing: {_isInitializing}) - This affects fixed layout");
            if (!_isInitializing)
            {
                CurrentPreviewPage = 1; // Reset to page 1 when layout changes
                _ = GeneratePreviewAsync(); // Regenerate because layout actually changed
                SaveSettings();
            }
        }

        partial void OnPrintDpiChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnPrintDpiChanged: {value} (initializing: {_isInitializing})");
            UpdateEstimatedFileSize(); // Always update estimate
            
            if (value >= 150m && value <= 1200m && !_isInitializing)
            {
                SaveSettings();
            }
        }

        partial void OnEnsureMinimumPrintDpiChanged(bool value)
        {
            DebugHelper.WriteDebug($"OnEnsureMinimumPrintDpiChanged: {value} (initializing: {_isInitializing})");
            if (!_isInitializing)
            {
                SaveSettings();
            }
        }

        partial void OnPreviewZoomChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnPreviewZoomChanged: {value}%");
            OnPropertyChanged(nameof(PreviewScale));
        }
    }
}