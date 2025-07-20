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

        // Preview
        [ObservableProperty] private Bitmap? _previewImage;
        [ObservableProperty] private bool _isGeneratingPreview;
        [ObservableProperty] private bool _isGeneratingPdf;
        [ObservableProperty] private decimal _previewZoom = 100m; // New zoom property
        
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

        // Available options
        public ObservableCollection<string> PageSizes { get; } = new()
        {
            "A4", "A3", "A5", "Letter", "Legal", "Tabloid"
        };

        public ObservableCollection<string> PredefinedColors { get; } = new()
        {
            "#000000", "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF"
        };

        public PrintViewModel(IPdfGenerationService pdfService, IConfigManager configManager, CardCollection cards)
        {
            _pdfService = pdfService;
            _configManager = configManager;
            _cards = cards;

            // Set default values first
            CardsPerRow = 3;
            CardsPerColumn = 3;
            CardSpacing = 10m;
            ShowCuttingLines = true;
            CuttingLineColor = "#000000";
            IsCuttingLineDashed = false;
            CuttingLineExtension = 10m;
            CuttingLineThickness = 0.5m;
            PreviewDpi = 150;
            PreviewQuality = 85;
            SelectedPageSize = "A4";
            IsPortrait = true;

            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = _configManager.Config.PdfSettings;
            
            DebugHelper.WriteDebug($"Loading settings - Raw values: CardsPerRow={settings.CardsPerRow}, CardsPerColumn={settings.CardsPerColumn}");
            
            // Load settings but ensure reasonable defaults (convert to decimal)
            CardsPerRow = Math.Max(1, Math.Min(settings.CardsPerRow, 5)); // Clamp between 1-5
            CardsPerColumn = Math.Max(1, Math.Min(settings.CardsPerColumn, 5)); // Clamp between 1-5
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
            
            DebugHelper.WriteDebug($"Loaded settings: CardsPerRow={CardsPerRow}, CardsPerColumn={CardsPerColumn}, ShowCuttingLines={ShowCuttingLines}");
            DebugHelper.WriteDebug($"CardSpacing={CardSpacing}, CuttingLineExtension={CuttingLineExtension}, CuttingLineThickness={CuttingLineThickness}");
            
            // Auto-generate preview on startup
            _ = GeneratePreviewAsync();
        }

        private void SaveSettings()
        {
            var settings = _configManager.Config.PdfSettings;
            
            // Convert decimal back to the original types
            settings.CardsPerRow = (int)CardsPerRow;
            settings.CardsPerColumn = (int)CardsPerColumn;
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
            
            _configManager.SaveConfig();
        }

        private PdfGenerationOptions CreateOptions()
        {
            return new PdfGenerationOptions
            {
                IsPortrait = IsPortrait,
                PageSize = SelectedPageSize,
                // ❌ Remove these lines - we're now using fixed layouts based on orientation
                // CardsPerRow = (int)CardsPerRow,     
                // CardsPerColumn = (int)CardsPerColumn,
        
                // ✅ Don't set CardsPerRow/CardsPerColumn - let the PDF service decide based on IsPortrait
                CardSpacing = (float)CardSpacing,
                ShowCuttingLines = ShowCuttingLines,
                CuttingLineColor = CuttingLineColor,
                IsCuttingLineDashed = IsCuttingLineDashed,
                CuttingLineExtension = (float)CuttingLineExtension,
                CuttingLineThickness = (float)CuttingLineThickness,
                PreviewDpi = (int)PreviewDpi,
                PreviewQuality = (int)PreviewQuality
            };
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
                // Update page information
                UpdatePageInfo();
                
                DebugHelper.WriteDebug($"Starting preview generation with {_cards.Count} cards (Page {CurrentPreviewPage} of {TotalPreviewPages})");
                var options = CreateOptions();
                DebugHelper.WriteDebug($"Created options - CardsPerRow: {options.CardsPerRow}, CardsPerColumn: {options.CardsPerColumn}");
                
                // Get cards for current preview page
                var cardsPerPage = options.CardsPerRow * options.CardsPerColumn;
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
                // Handle error appropriately
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
            try
            {
                DebugHelper.WriteDebug($"Starting PDF generation with {_cards.Count} cards");
                var options = CreateOptions();
                DebugHelper.WriteDebug($"Created options - CardsPerRow: {options.CardsPerRow}, CardsPerColumn: {options.CardsPerColumn}");
                
                var pdfBytes = await _pdfService.GeneratePdfAsync(_cards, options);
                DebugHelper.WriteDebug($"PDF generation completed. Size: {pdfBytes.Length} bytes");
                
                var settings = _configManager.Config.PdfSettings;
                var fileName = $"{settings.DefaultFileName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var filePath = Path.Combine(settings.DefaultOutputPath, fileName);
                
                await File.WriteAllBytesAsync(filePath, pdfBytes);
                
                DebugHelper.WriteDebug($"PDF saved to: {filePath}");
                SaveSettings();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error generating PDF: {ex.Message}");
                DebugHelper.WriteDebug($"Stack trace: {ex.StackTrace}");
                // Handle error appropriately
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

            // This would need to be implemented with proper file dialog
            // For now, just call GeneratePdfAsync
            await GeneratePdfAsync();
        }

        // Computed property for ScaleTransform (converts percentage to scale factor)
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
            var cardsPerPage = (int)(CardsPerRow * CardsPerColumn);
            TotalPreviewPages = cardsPerPage == 0 ? 1 : (int)Math.Ceiling((double)_cards.Count / cardsPerPage);
            
            // Ensure current page is valid
            if (CurrentPreviewPage > TotalPreviewPages)
                CurrentPreviewPage = Math.Max(1, TotalPreviewPages);
                
            DebugHelper.WriteDebug($"Updated page info: Page {CurrentPreviewPage} of {TotalPreviewPages}");
        }

        // Property change handlers to auto-regenerate preview
        partial void OnCardsPerRowChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCardsPerRowChanged: {value}");
            if (value > 0 && value <= 10)
            {
                CurrentPreviewPage = 1; // Reset to first page when layout changes
                _ = GeneratePreviewAsync();
            }
        }

        partial void OnCardsPerColumnChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCardsPerColumnChanged: {value}");
            if (value > 0 && value <= 10)
            {
                CurrentPreviewPage = 1; // Reset to first page when layout changes
                _ = GeneratePreviewAsync();
            }
        }

        partial void OnCardSpacingChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCardSpacingChanged: {value}");
            if (value >= 0)
                _ = GeneratePreviewAsync();
        }

        partial void OnShowCuttingLinesChanged(bool value)
        {
            DebugHelper.WriteDebug($"OnShowCuttingLinesChanged: {value}");
            _ = GeneratePreviewAsync();
        }

        partial void OnCuttingLineColorChanged(string value)
        {
            DebugHelper.WriteDebug($"OnCuttingLineColorChanged: {value}");
            _ = GeneratePreviewAsync();
        }

        partial void OnIsCuttingLineDashedChanged(bool value)
        {
            DebugHelper.WriteDebug($"OnIsCuttingLineDashedChanged: {value}");
            _ = GeneratePreviewAsync();
        }

        partial void OnCuttingLineExtensionChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCuttingLineExtensionChanged: {value}");
            _ = GeneratePreviewAsync();
        }

        partial void OnCuttingLineThicknessChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnCuttingLineThicknessChanged: {value}");
            _ = GeneratePreviewAsync();
        }

        partial void OnPreviewDpiChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnPreviewDpiChanged: {value}");
            if (value >= 72 && value <= 300)
                _ = GeneratePreviewAsync();
        }

        partial void OnPreviewQualityChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnPreviewQualityChanged: {value}");
            if (value >= 1 && value <= 100)
                _ = GeneratePreviewAsync();
        }

        partial void OnSelectedPageSizeChanged(string value)
        {
            // Note: Page size is handled directly in the PDF service
            // This is kept for UI consistency but doesn't affect the actual PDF generation
            _ = GeneratePreviewAsync();
        }

        partial void OnIsPortraitChanged(bool value)
        {
            _ = GeneratePreviewAsync();
        }

        partial void OnPreviewZoomChanged(decimal value)
        {
            DebugHelper.WriteDebug($"OnPreviewZoomChanged: {value}%");
            // Notify that PreviewScale has also changed
            OnPropertyChanged(nameof(PreviewScale));
        }
    }
}