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
using QuestPDF.Infrastructure;

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

        // PDF Settings - bound to UI
        [ObservableProperty] private int _cardsPerRow;
        [ObservableProperty] private int _cardsPerColumn;
        [ObservableProperty] private float _cardSpacing;
        [ObservableProperty] private bool _showCuttingLines;
        [ObservableProperty] private string _cuttingLineColor;
        [ObservableProperty] private bool _isCuttingLineDashed;
        [ObservableProperty] private float _cuttingLineExtension;
        [ObservableProperty] private float _cuttingLineThickness;
        [ObservableProperty] private int _previewDpi;
        [ObservableProperty] private int _previewQuality;
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
            CardSpacing = 10f;
            ShowCuttingLines = true;
            CuttingLineColor = "#000000";
            IsCuttingLineDashed = false;
            CuttingLineExtension = 10f;
            CuttingLineThickness = 0.5f;
            PreviewDpi = 150;
            PreviewQuality = 85;
            SelectedPageSize = "A4";
            IsPortrait = true;

            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = _configManager.Config.PdfSettings;
            
            // Load settings but ensure reasonable defaults
            CardsPerRow = Math.Max(1, Math.Min(settings.CardsPerRow, 5)); // Clamp between 1-5
            CardsPerColumn = Math.Max(1, Math.Min(settings.CardsPerColumn, 5)); // Clamp between 1-5
            CardSpacing = Math.Max(0, settings.CardSpacing);
            ShowCuttingLines = settings.ShowCuttingLines;
            CuttingLineColor = string.IsNullOrEmpty(settings.CuttingLineColor) ? "#FF0000" : settings.CuttingLineColor;
            IsCuttingLineDashed = settings.IsCuttingLineDashed;
            CuttingLineExtension = Math.Max(0, settings.CuttingLineExtension);
            CuttingLineThickness = Math.Max(0.1f, settings.CuttingLineThickness);
            PreviewDpi = Math.Max(72, Math.Min(settings.PreviewDpi, 300));
            PreviewQuality = Math.Max(1, Math.Min(settings.PreviewQuality, 100));
            SelectedPageSize = string.IsNullOrEmpty(settings.PageSize) ? "A4" : settings.PageSize;
            IsPortrait = settings.IsPortrait;
            
            DebugHelper.WriteDebug($"Loaded settings: CardsPerRow={CardsPerRow}, CardsPerColumn={CardsPerColumn}, ShowCuttingLines={ShowCuttingLines}");
            
            // Auto-generate preview on startup
            _ = GeneratePreviewAsync();
        }

        private void SaveSettings()
        {
            var settings = _configManager.Config.PdfSettings;
            
            settings.CardsPerRow = CardsPerRow;
            settings.CardsPerColumn = CardsPerColumn;
            settings.CardSpacing = CardSpacing;
            settings.ShowCuttingLines = ShowCuttingLines;
            settings.CuttingLineColor = CuttingLineColor;
            settings.IsCuttingLineDashed = IsCuttingLineDashed;
            settings.CuttingLineExtension = CuttingLineExtension;
            settings.CuttingLineThickness = CuttingLineThickness;
            settings.PreviewDpi = PreviewDpi;
            settings.PreviewQuality = PreviewQuality;
            settings.PageSize = SelectedPageSize;
            settings.IsPortrait = IsPortrait;
            
            _configManager.SaveConfig();
        }

        private PdfGenerationOptions CreateOptions()
        {
            return new PdfGenerationOptions
            {
                PageSize = GetQuestPdfPageSize(SelectedPageSize),
                IsPortrait = IsPortrait,
                CardsPerRow = CardsPerRow,
                CardsPerColumn = CardsPerColumn,
                CardSpacing = CardSpacing,
                ShowCuttingLines = ShowCuttingLines,
                CuttingLineColor = CuttingLineColor,
                IsCuttingLineDashed = IsCuttingLineDashed,
                CuttingLineExtension = CuttingLineExtension,
                CuttingLineThickness = CuttingLineThickness,
                PreviewDpi = PreviewDpi,
                PreviewQuality = PreviewQuality
            };
        }

        private Size GetQuestPdfPageSize(string pageSizeName)
        {
            return pageSizeName switch
            {
                "A4" => new Size(595, 842),        // A4 in points
                "A3" => new Size(842, 1191),       // A3 in points
                "A5" => new Size(420, 595),        // A5 in points
                "Letter" => new Size(612, 792),    // Letter in points
                "Legal" => new Size(612, 1008),    // Legal in points
                "Tabloid" => new Size(792, 1224),  // Tabloid in points
                _ => new Size(595, 842)             // Default to A4
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
                DebugHelper.WriteDebug($"Starting preview generation with {_cards.Count} cards");
                var options = CreateOptions();
                DebugHelper.WriteDebug($"Created options - PageSize: {options.PageSize}, CardsPerRow: {options.CardsPerRow}");
                
                PreviewImage = await _pdfService.GeneratePreviewImageAsync(_cards, options);
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
                DebugHelper.WriteDebug($"Created options - PageSize: {options.PageSize}, CardsPerRow: {options.CardsPerRow}");
                
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
        private async Task SavePdfAsAsync()
        {
            if (IsGeneratingPdf || _cards.Count == 0) return;

            // This would need to be implemented with proper file dialog
            // For now, just call GeneratePdfAsync
            await GeneratePdfAsync();
        }

        // Property change handlers to auto-regenerate preview
        partial void OnCardsPerRowChanged(int value)
        {
            if (value > 0 && value <= 10)
                _ = GeneratePreviewAsync();
        }

        partial void OnCardsPerColumnChanged(int value)
        {
            if (value > 0 && value <= 10)
                _ = GeneratePreviewAsync();
        }

        partial void OnCardSpacingChanged(float value)
        {
            if (value >= 0)
                _ = GeneratePreviewAsync();
        }

        partial void OnShowCuttingLinesChanged(bool value)
        {
            _ = GeneratePreviewAsync();
        }

        partial void OnCuttingLineColorChanged(string value)
        {
            _ = GeneratePreviewAsync();
        }

        partial void OnIsCuttingLineDashedChanged(bool value)
        {
            _ = GeneratePreviewAsync();
        }

        partial void OnCuttingLineExtensionChanged(float value)
        {
            _ = GeneratePreviewAsync();
        }

        partial void OnCuttingLineThicknessChanged(float value)
        {
            _ = GeneratePreviewAsync();
        }

        partial void OnPreviewDpiChanged(int value)
        {
            if (value >= 72 && value <= 300)
                _ = GeneratePreviewAsync();
        }

        partial void OnPreviewQualityChanged(int value)
        {
            if (value >= 1 && value <= 100)
                _ = GeneratePreviewAsync();
        }

        partial void OnSelectedPageSizeChanged(string value)
        {
            _ = GeneratePreviewAsync();
        }

        partial void OnIsPortraitChanged(bool value)
        {
            _ = GeneratePreviewAsync();
        }
    }
}