using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Quality;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace ProxyStudio.Services
{
    public interface IPdfGenerationService
    {
        Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options, IProgress<PdfGenerationProgress>? progress = null);
        Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options);
    }
    
    public class PdfGenerationProgress
    {
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public string CurrentOperation { get; set; } = "";
        public string CurrentCardName { get; set; } = "";
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan? EstimatedRemainingTime { get; set; }
        public long ProcessedBytes { get; set; }
    
        public double PercentageComplete => TotalSteps > 0 ? (double)CurrentStep / TotalSteps * 100 : 0;
    
        public string StatusMessage => $"Page {CurrentPage}/{TotalPages}: {CurrentOperation} - {CurrentCardName}";
    }

    public class PdfGenerationOptions
    {
        public bool IsPortrait { get; set; } = true;
        public int CardsPerRow { get; set; } = 3;
        public int CardsPerColumn { get; set; } = 3;
        public float CardSpacing { get; set; } = 0f;
        public bool ShowCuttingLines { get; set; } = true;
        public string CuttingLineColor { get; set; } = "#FF0000";
        public bool IsCuttingLineDashed { get; set; } = false;
        public float CuttingLineExtension { get; set; } = 10f;
        public float CuttingLineThickness { get; set; } = 2f;
        public float TopMargin { get; set; } = 20f;
        public float BottomMargin { get; set; } = 20f;
        public float LeftMargin { get; set; } = 20f;
        public float RightMargin { get; set; } = 20f;
        
        // Separate DPI settings for preview vs print
        public int PreviewDpi { get; set; } = 150;    // Lower for performance
        public int PrintDpi { get; set; } = 300;      // High quality for print
        public int PreviewQuality { get; set; } = 85;
        public string PageSize { get; set; } = "A4";
    }

    public class PdfGenerationService : IPdfGenerationService
    {
        // Card dimensions in points (72 DPI standard) - FIXED at exactly 2.5" x 3.5"
        private const double CARD_WIDTH_INCHES = 2.5;
        private const double CARD_HEIGHT_INCHES = 3.5;
        private const double CARD_WIDTH_POINTS = CARD_WIDTH_INCHES * 72; // 180 points
        private const double CARD_HEIGHT_POINTS = CARD_HEIGHT_INCHES * 72; // 252 points

        static PdfGenerationService()
        {
            // Set up PDFsharp with better font handling
            try
            {
                PdfSharp.Fonts.GlobalFontSettings.FontResolver = new SafeFontResolver();
                DebugHelper.WriteDebug("PDFsharp font resolver set up successfully");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error setting up font resolver: {ex.Message}");
            }
        }

        public async Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options, IProgress<PdfGenerationProgress>? progress = null)
{
    return await Task.Run(() =>
    {
        try
        {
            var startTime = DateTime.Now;
            var progressInfo = new PdfGenerationProgress();
            
            // Calculate total steps for progress tracking
            var actualCardsPerRow = options.IsPortrait ? 3 : 4;
            var actualCardsPerColumn = options.IsPortrait ? 3 : 2;
            var cardsPerPage = actualCardsPerRow * actualCardsPerColumn;
            var totalPages = (int)Math.Ceiling((double)cards.Count / cardsPerPage);
            
            progressInfo.TotalSteps = cards.Count + totalPages + 2; // Cards + Pages + Setup + Finalization
            progressInfo.TotalPages = totalPages;
            progressInfo.CurrentOperation = "Initializing PDF generation...";
            
            progress?.Report(progressInfo);
            
            DebugHelper.WriteDebug($"=== PDF GENERATION START ===");
            DebugHelper.WriteDebug($"Target DPI: {options.PrintDpi}");
            DebugHelper.WriteDebug($"Cards: {cards.Count}, Pages: {totalPages}");
            
            // Create PDF document
            progressInfo.CurrentStep = 1;
            progressInfo.CurrentOperation = "Creating PDF document...";
            progress?.Report(progressInfo);
            
            var document = new PdfDocument();
            ApplyHighDpiSettings(document, options.PrintDpi);
            
            DebugHelper.WriteDebug($"Layout: {actualCardsPerRow}x{actualCardsPerColumn}");
            
            // Track processed data
            var processedImageCount = 0;
            var totalProcessedBytes = 0L;
            
            // Process each page
            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                progressInfo.CurrentPage = pageIndex + 1;
                progressInfo.CurrentStep = 2 + pageIndex;
                progressInfo.CurrentOperation = $"Creating page {pageIndex + 1}...";
                progressInfo.ElapsedTime = DateTime.Now - startTime;
                
                // Estimate remaining time
                if (pageIndex > 0)
                {
                    var averageTimePerPage = progressInfo.ElapsedTime.TotalSeconds / pageIndex;
                    var remainingPages = totalPages - pageIndex;
                    progressInfo.EstimatedRemainingTime = TimeSpan.FromSeconds(averageTimePerPage * remainingPages);
                }
                
                progress?.Report(progressInfo);
                
                var page = document.AddPage();
                page.Size = GetPageSize(options.PageSize);
                page.Orientation = options.IsPortrait ? 
                    PdfSharp.PageOrientation.Portrait : 
                    PdfSharp.PageOrientation.Landscape;
                
                var gfx = XGraphics.FromPdfPage(page);
                ApplyHighDpiTransformation(gfx, options.PrintDpi);
                
                var startCardIndex = pageIndex * cardsPerPage;
                var pageCards = cards.Skip(startCardIndex).Take(cardsPerPage).ToList();
                
                DebugHelper.WriteDebug($"Page {pageIndex + 1}: Drawing {pageCards.Count} cards");
                
                // Draw cards with progress reporting
                DrawCardGridWithProgress(gfx, pageCards, options, page.Width, page.Height, 
                    pageIndex + 1, totalPages, progress, progressInfo, startTime);
                
                processedImageCount += pageCards.Count;
                gfx.Dispose();
            }
            
            // Finalize PDF
            progressInfo.CurrentStep = progressInfo.TotalSteps - 1;
            progressInfo.CurrentOperation = "Finalizing PDF...";
            progressInfo.CurrentCardName = "";
            progressInfo.ElapsedTime = DateTime.Now - startTime;
            progress?.Report(progressInfo);
            
            using var stream = new MemoryStream();
            document.Save(stream);
            document.Close();
            
            var pdfBytes = stream.ToArray();
            var endTime = DateTime.Now;
            var duration = endTime - startTime;
            
            // Report completion
            progressInfo.CurrentStep = progressInfo.TotalSteps;
            progressInfo.CurrentOperation = "PDF generation complete!";
            progressInfo.ProcessedBytes = pdfBytes.Length;
            progressInfo.ElapsedTime = duration;
            progressInfo.EstimatedRemainingTime = TimeSpan.Zero;
            progress?.Report(progressInfo);
            
            DebugHelper.WriteDebug($"=== PDF GENERATION COMPLETE ===");
            DebugHelper.WriteDebug($"Final PDF size: {pdfBytes.Length / (1024.0 * 1024.0):F2} MB");
            DebugHelper.WriteDebug($"Images processed: {processedImageCount}");
            DebugHelper.WriteDebug($"Generation time: {duration.TotalSeconds:F1} seconds");
            DebugHelper.WriteDebug($"Average per card: {duration.TotalMilliseconds / cards.Count:F1} ms");
            
            return pdfBytes;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteDebug($"Error generating PDF: {ex.Message}");
            
            // Report error
            progress?.Report(new PdfGenerationProgress
            {
                CurrentStep = 0,
                TotalSteps = 1,
                CurrentOperation = $"Error: {ex.Message}",
                CurrentCardName = ""
            });
            
            throw;
        }
    });
}

        private void DrawCardGridWithProgress(XGraphics gfx, List<Card> pageCards, PdfGenerationOptions options, 
    XUnit pageWidth, XUnit pageHeight, int currentPage, int totalPages,
    IProgress<PdfGenerationProgress>? progress, PdfGenerationProgress progressInfo, DateTime startTime)
{
    var actualCardsPerRow = options.IsPortrait ? 3 : 4;
    var actualCardsPerColumn = options.IsPortrait ? 3 : 2;
    
    var cardWidthPt = CARD_WIDTH_POINTS;
    var cardHeightPt = CARD_HEIGHT_POINTS;
    
    var totalHorizontalSpacing = (actualCardsPerRow - 1) * options.CardSpacing;
    var totalVerticalSpacing = (actualCardsPerColumn - 1) * options.CardSpacing;
    var totalGridWidthPt = actualCardsPerRow * cardWidthPt + totalHorizontalSpacing;
    var totalGridHeightPt = actualCardsPerColumn * cardHeightPt + totalVerticalSpacing;
    
    var availableWidthPt = pageWidth.Point - (options.LeftMargin + options.RightMargin);
    var availableHeightPt = pageHeight.Point - (options.TopMargin + options.BottomMargin + 50);
    
    var startXPt = options.LeftMargin + (availableWidthPt - totalGridWidthPt) / 2;
    var startYPt = options.TopMargin + 30 + (availableHeightPt - totalGridHeightPt) / 2;
    
    // Draw title
    try
    {
        var font = GetSafeFont("Arial", 14, XFontStyleEx.Bold);
        var title = totalPages > 1 
            ? $"Card Collection - Page {currentPage} of {totalPages} ({actualCardsPerRow}x{actualCardsPerColumn}) - {options.PrintDpi} DPI"
            : $"Card Collection - {pageCards.Count} cards ({actualCardsPerRow}x{actualCardsPerColumn}) - {options.PrintDpi} DPI";
            
        gfx.DrawString(title, font, XBrushes.Black,
            new XPoint(XUnit.FromPoint(options.LeftMargin), XUnit.FromPoint(options.TopMargin)));
    }
    catch (Exception ex)
    {
        DebugHelper.WriteDebug($"Error drawing title: {ex.Message}");
    }
    
    // Draw cards with individual progress updates
    for (int row = 0; row < actualCardsPerColumn; row++)
    {
        for (int col = 0; col < actualCardsPerRow; col++)
        {
            var cardIndex = row * actualCardsPerRow + col;
            
            if (cardIndex < pageCards.Count)
            {
                var card = pageCards[cardIndex];
                
                // Update progress for this card
                progressInfo.CurrentStep = 2 + totalPages + (currentPage - 1) * pageCards.Count + cardIndex;
                progressInfo.CurrentOperation = $"Processing card {cardIndex + 1} of {pageCards.Count}";
                progressInfo.CurrentCardName = card.Name;
                progressInfo.ElapsedTime = DateTime.Now - startTime;
                
                // Update estimated time based on cards processed so far
                var totalCardsProcessed = (currentPage - 1) * pageCards.Count + cardIndex;
                if (totalCardsProcessed > 0)
                {
                    var averageTimePerCard = progressInfo.ElapsedTime.TotalSeconds / totalCardsProcessed;
                    var remainingCards = progressInfo.TotalSteps - progressInfo.CurrentStep;
                    progressInfo.EstimatedRemainingTime = TimeSpan.FromSeconds(averageTimePerCard * remainingCards);
                }
                
                progress?.Report(progressInfo);
                
                var xPt = startXPt + col * (cardWidthPt + options.CardSpacing);
                var yPt = startYPt + row * (cardHeightPt + options.CardSpacing);
                
                var x = XUnit.FromPoint(xPt);
                var y = XUnit.FromPoint(yPt);
                var cardWidth = XUnit.FromPoint(cardWidthPt);
                var cardHeight = XUnit.FromPoint(cardHeightPt);
                
                DrawCard(gfx, card, options, x, y, cardWidth, cardHeight);
            }
        }
    }
    
    // Draw cutting lines
    if (options.ShowCuttingLines)
    {
        progressInfo.CurrentOperation = "Drawing cutting lines...";
        progress?.Report(progressInfo);
        
        DrawPdfGridCuttingLines(gfx, options, startXPt, startYPt, 
            actualCardsPerRow, actualCardsPerColumn, cardWidthPt, cardHeightPt);
    }
}
        public async Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    DebugHelper.WriteDebug($"Generating preview for {cards?.Count ?? 0} cards using Preview DPI: {options.PreviewDpi} (Print DPI will be: {options.PrintDpi})");
                    
                    if (options == null)
                    {
                        DebugHelper.WriteDebug("ERROR: options is null!");
                        return CreateFallbackPreview(cards, options);
                    }
                    
                    // Use preview DPI for preview generation (not print DPI)
                    var previewBitmap = CreateSimplePreview(cards, options, options.PreviewDpi);
                    
                    DebugHelper.WriteDebug($"Preview generated successfully using {options.PreviewDpi} DPI (final PDF will use {options.PrintDpi} DPI)");
                    return previewBitmap;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteDebug($"Error generating preview: {ex.Message}");
                    DebugHelper.WriteDebug($"Stack trace: {ex.StackTrace}");
                    return CreateFallbackPreview(cards, options);
                }
            });
        }

        private void ApplyHighDpiSettings(PdfDocument document, int targetDpi)
        {
            try
            {
                // Set document metadata to indicate high DPI
                document.Info.Title = $"ProxyStudio Cards - {targetDpi} DPI";
                document.Info.Creator = "ProxyStudio";
                document.Info.Subject = $"Proxy Cards at {targetDpi} DPI for high-quality printing";
                
                DebugHelper.WriteDebug($"Applied high-DPI settings: {targetDpi} DPI");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Warning: Could not apply high-DPI document settings: {ex.Message}");
            }
        }

        private void ApplyHighDpiTransformation(XGraphics gfx, int targetDpi)
        {
            // REMOVED: The graphics transformation was breaking card dimensions
            // High-DPI is now handled purely through image processing, not graphics scaling
            // This ensures cards remain exactly 2.5" × 3.5" regardless of DPI setting
            
            DebugHelper.WriteDebug($"Skipped graphics DPI transformation - handling DPI through image processing only");
            DebugHelper.WriteDebug($"Cards will maintain EXACT 2.5\" × 3.5\" dimensions at {targetDpi} DPI");
        }

        private (double Width, double Height) GetPageDimensions(string pageSize, bool isLandscape)
        {
            // Page dimensions in points (72 DPI)
            var dimensions = pageSize?.ToUpper() switch
            {
                "A3" => (841.89, 1190.55),   // A3: 297 × 420 mm
                "A4" => (595.28, 841.89),    // A4: 210 × 297 mm  
                "A5" => (419.53, 595.28),    // A5: 148 × 210 mm
                "LETTER" => (612.0, 792.0),  // Letter: 8.5 × 11 inches
                "LEGAL" => (612.0, 1008.0),  // Legal: 8.5 × 14 inches
                "TABLOID" => (792.0, 1224.0), // Tabloid: 11 × 17 inches
                _ => (595.28, 841.89) // Default to A4
            };
            
            // Swap dimensions for landscape
            if (isLandscape)
            {
                return (dimensions.Item2, dimensions.Item1);
            }
            
            return dimensions;
        }

        private PdfSharp.PageSize GetPageSize(string pageSize)
        {
            return pageSize?.ToUpper() switch
            {
                "A3" => PdfSharp.PageSize.A3,
                "A4" => PdfSharp.PageSize.A4,
                "A5" => PdfSharp.PageSize.A5,
                "LETTER" => PdfSharp.PageSize.Letter,
                "LEGAL" => PdfSharp.PageSize.Legal,
                "TABLOID" => PdfSharp.PageSize.Tabloid,
                _ => PdfSharp.PageSize.A4 // Default to A4
            };
        }

        private void DrawCardGrid(XGraphics gfx, List<Card> pageCards, PdfGenerationOptions options, XUnit pageWidth, XUnit pageHeight, int currentPage, int totalPages)
        {
            // Use user's orientation choice for layout
            var actualCardsPerRow = options.IsPortrait ? 3 : 4;
            var actualCardsPerColumn = options.IsPortrait ? 3 : 2;
            
            DebugHelper.WriteDebug($"Drawing {pageCards.Count} cards in FIXED {actualCardsPerRow}x{actualCardsPerColumn} grid ({(options.IsPortrait ? "Portrait" : "Landscape")}) with {options.CardSpacing}pt spacing (Page {currentPage} of {totalPages})");
            
            // FIXED CARD DIMENSIONS: Always exactly 2.5" x 3.5" (180 x 252 points)
            var cardWidthPt = CARD_WIDTH_POINTS;
            var cardHeightPt = CARD_HEIGHT_POINTS;
            
            DebugHelper.WriteDebug($"Card dimensions: {cardWidthPt:F3}x{cardHeightPt:F3} points (FIXED 2.5\" x 3.5\") - spacing: {options.CardSpacing}pt");
            
            // Calculate total grid size
            var totalHorizontalSpacing = (actualCardsPerRow - 1) * options.CardSpacing;
            var totalVerticalSpacing = (actualCardsPerColumn - 1) * options.CardSpacing;
            var totalGridWidthPt = actualCardsPerRow * cardWidthPt + totalHorizontalSpacing;
            var totalGridHeightPt = actualCardsPerColumn * cardHeightPt + totalVerticalSpacing;
            
            // Calculate available space
            var availableWidthPt = pageWidth.Point - (options.LeftMargin + options.RightMargin);
            var availableHeightPt = pageHeight.Point - (options.TopMargin + options.BottomMargin + 50); // Space for title
            
            // Center the grid on the page
            var startXPt = options.LeftMargin + (availableWidthPt - totalGridWidthPt) / 2;
            var startYPt = options.TopMargin + 30 + (availableHeightPt - totalGridHeightPt) / 2;
            
            DebugHelper.WriteDebug($"Grid layout: start=({startXPt:F3}, {startYPt:F3}), total size=({totalGridWidthPt:F3}x{totalGridHeightPt:F3})");
            
            // Draw title with page info
            try
            {
                var font = GetSafeFont("Arial", 14, XFontStyleEx.Bold);
                var title = totalPages > 1 
                    ? $"Card Collection - Page {currentPage} of {totalPages} ({actualCardsPerRow}x{actualCardsPerColumn})"
                    : $"Card Collection - {pageCards.Count} cards ({actualCardsPerRow}x{actualCardsPerColumn})";
                    
                gfx.DrawString(title, font, XBrushes.Black,
                    new XPoint(XUnit.FromPoint(options.LeftMargin), XUnit.FromPoint(options.TopMargin)));
                DebugHelper.WriteDebug("Drew title successfully");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error drawing title: {ex.Message}");
            }
            
            // Draw cards with precise positioning
            for (int row = 0; row < actualCardsPerColumn; row++)
            {
                for (int col = 0; col < actualCardsPerRow; col++)
                {
                    var cardIndex = row * actualCardsPerRow + col;
                    
                    if (cardIndex < pageCards.Count)
                    {
                        var card = pageCards[cardIndex];
                        
                        // Calculate position with spacing - use precise double arithmetic
                        var xPt = startXPt + col * (cardWidthPt + options.CardSpacing);
                        var yPt = startYPt + row * (cardHeightPt + options.CardSpacing);
                        
                        // Convert to XUnit only at the very end to preserve precision
                        var x = XUnit.FromPoint(xPt);
                        var y = XUnit.FromPoint(yPt);
                        var cardWidth = XUnit.FromPoint(cardWidthPt);
                        var cardHeight = XUnit.FromPoint(cardHeightPt);
                        
                        DebugHelper.WriteDebug($"Drawing card {cardIndex} ({card.Name}) at ({xPt:F3}, {yPt:F3}) - row {row}, col {col}");
                        
                        DrawCard(gfx, card, options, x, y, cardWidth, cardHeight);
                    }
                }
            }
            
            // Draw cutting lines for the entire grid (outside card areas)
            if (options.ShowCuttingLines)
            {
                DrawPdfGridCuttingLines(gfx, options, startXPt, startYPt, 
                    actualCardsPerRow, actualCardsPerColumn, cardWidthPt, cardHeightPt);
            }
        }

        private void DrawCard(XGraphics gfx, Card card, PdfGenerationOptions options, XUnit x, XUnit y, XUnit width, XUnit height)
        {
            DebugHelper.WriteDebug($"DrawCard: {card.Name} at ({x:F1}, {y:F1}) - FIXED size {CARD_WIDTH_INCHES}\" × {CARD_HEIGHT_INCHES}\" at {options.PrintDpi} DPI");
            
            try
            {
                if (card.ImageData != null && card.ImageData.Length > 0)
                {
                    // FIXED: Call the correct high-DPI processing method
                    var processedImage = ProcessImageForHighDpiPdf(card.ImageData, card.Name, options.PrintDpi);
                    if (processedImage != null)
                    {
                        using var ms = new MemoryStream(processedImage);
                        var xImage = XImage.FromStream(ms);
                        
                        // Draw the image to fill the EXACT card dimensions
                        gfx.DrawImage(xImage, new XRect(x, y, width, height));
                        
                        DebugHelper.WriteDebug($"SUCCESS: Drew high-DPI image for {card.Name} at {options.PrintDpi} DPI - processed size: {processedImage.Length} bytes");
                        
                        xImage.Dispose();
                    }
                    else
                    {
                        DebugHelper.WriteDebug($"ERROR: ProcessImageForHighDpiPdf returned null for {card.Name}");
                        DrawPlaceholder(gfx, card, x, y, width, height, $"Image Error ({options.PrintDpi} DPI)");
                    }
                }
                else
                {
                    DebugHelper.WriteDebug($"WARNING: No image data for {card.Name}");
                    DrawPlaceholder(gfx, card, x, y, width, height, $"No Image ({options.PrintDpi} DPI)");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"ERROR: Exception in DrawCard for {card.Name} at {options.PrintDpi} DPI: {ex.Message}");
                DebugHelper.WriteDebug($"Stack trace: {ex.StackTrace}");
                DrawPlaceholder(gfx, card, x, y, width, height, $"Error ({options.PrintDpi} DPI)");
            }
        }

        private byte[]? ProcessImageForHighDpiPdf(byte[] imageData, string cardName, int targetDpi)
        {
            try
            {
                DebugHelper.WriteDebug($"ProcessImageForHighDpiPdf: Processing {cardName} for {targetDpi} DPI");
                
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageData);
                var originalSize = $"{image.Width}x{image.Height}";
                var originalDataSize = imageData.Length;
                
                DebugHelper.WriteDebug($"  Source: {originalSize}, {originalDataSize} bytes");
                
                // Calculate target dimensions for exact 2.5" × 3.5" at target DPI
                var targetWidth = (int)(CARD_WIDTH_INCHES * targetDpi);
                var targetHeight = (int)(CARD_HEIGHT_INCHES * targetDpi);
                
                DebugHelper.WriteDebug($"  Target: {targetWidth}x{targetHeight} pixels for {CARD_WIDTH_INCHES}\"×{CARD_HEIGHT_INCHES}\" at {targetDpi} DPI");
                
                // Resize to exact target dimensions
                if (image.Width != targetWidth || image.Height != targetHeight)
                {
                    var scaleX = (double)targetWidth / image.Width;
                    var scaleY = (double)targetHeight / image.Height;
                    DebugHelper.WriteDebug($"  Scaling: {scaleX:F3}x horizontally, {scaleY:F3}x vertically");
                    
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(targetWidth, targetHeight),
                        Mode = ResizeMode.Stretch, // Ensure exact dimensions
                        Sampler = KnownResamplers.Lanczos3 // High-quality resampling
                    }));
                    
                    DebugHelper.WriteDebug($"  Resized to: {image.Width}x{image.Height}");
                }
                else
                {
                    DebugHelper.WriteDebug($"  No resize needed - already at target resolution");
                }
                
                // Set the DPI metadata
                image.Metadata.HorizontalResolution = targetDpi;
                image.Metadata.VerticalResolution = targetDpi;
                
                // Choose output format based on quality requirements
                MemoryStream outputStream = new MemoryStream();
                string format;
                
                if (targetDpi >= 600) // Use PNG for very high DPI to preserve quality
                {
                    var pngEncoder = new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.BestCompression,
                        ColorType = PngColorType.RgbWithAlpha
                    };
                    image.Save(outputStream, pngEncoder);
                    format = "PNG";
                }
                else // Use high-quality JPEG for reasonable file sizes
                {
                    var jpegEncoder = new JpegEncoder
                    {
                        Quality = 95 // Very high quality
                        // Note: Subsample property may not be available in all versions
                    };
                    image.Save(outputStream, jpegEncoder);
                    format = "JPEG";
                }
                
                var processedData = outputStream.ToArray();
                outputStream.Dispose();
                
                // Log the results
                var sizeRatio = (double)processedData.Length / originalDataSize;
                var pixelCount = targetWidth * targetHeight;
                
                DebugHelper.WriteDebug($"  Output: {processedData.Length} bytes as {format}");
                DebugHelper.WriteDebug($"  Size change: {originalDataSize} → {processedData.Length} bytes ({sizeRatio:F2}x)");
                DebugHelper.WriteDebug($"  Pixel count: {pixelCount:N0} pixels");
                DebugHelper.WriteDebug($"  Final DPI metadata: {image.Metadata.HorizontalResolution}x{image.Metadata.VerticalResolution}");
                DebugHelper.WriteDebug($"SUCCESS: ProcessImageForHighDpiPdf completed for {cardName}");
                
                return processedData;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"ERROR: ProcessImageForHighDpiPdf failed for {cardName}: {ex.Message}");
                DebugHelper.WriteDebug($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private Bitmap CreateSimplePreview(CardCollection cards, PdfGenerationOptions options, int previewDpi)
        {
            try
            {
                DebugHelper.WriteDebug($"CreateSimplePreview called with {cards?.Count ?? 0} cards at {previewDpi} DPI for preview (print will be {options.PrintDpi} DPI)");
                
                // Use the user's orientation choice, not automatic layout
                var actualCardsPerRow = options.IsPortrait ? 3 : 4;
                var actualCardsPerColumn = options.IsPortrait ? 3 : 2;
                var cardsPerPage = actualCardsPerRow * actualCardsPerColumn;
                var pageCards = cards?.Take(cardsPerPage).ToList() ?? new List<Card>();
                
                DebugHelper.WriteDebug($"Page cards: {pageCards.Count} in {actualCardsPerRow}x{actualCardsPerColumn} grid with {options.CardSpacing}pt spacing");
                
                // Get page dimensions based on selected page size and USER'S orientation choice
                var pageDimensions = GetPageDimensions(options.PageSize, !options.IsPortrait); // !IsPortrait = IsLandscape
                
                // Calculate preview scale to fit in a reasonable screen size
                var maxPreviewWidth = 1200;
                var maxPreviewHeight = 900;
                var scaleX = maxPreviewWidth / pageDimensions.Width;
                var scaleY = maxPreviewHeight / pageDimensions.Height;
                var pageScale = Math.Min(scaleX, scaleY);
                
                // Calculate preview dimensions maintaining page aspect ratio
                var previewWidth = (int)(pageDimensions.Width * pageScale);
                var previewHeight = (int)(pageDimensions.Height * pageScale);
                
                DebugHelper.WriteDebug($"Preview dimensions: {previewWidth}x{previewHeight} (PDF: {pageDimensions.Width:F0}x{pageDimensions.Height:F0})");
                DebugHelper.WriteDebug($"Page: {options.PageSize} {(options.IsPortrait ? "Portrait" : "Landscape")}, Scale: {pageScale:F3}");
                
                using var bitmap = new System.Drawing.Bitmap(previewWidth, previewHeight);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                
                graphics.Clear(System.Drawing.Color.White);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                
                // Draw title
                using var titleFont = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
                var titleText = $"Card Collection - {cards?.Count ?? 0} cards ({actualCardsPerRow}x{actualCardsPerColumn}) {(options.IsPortrait ? "Portrait" : "Landscape")} {options.PageSize}";
                graphics.DrawString(titleText, titleFont, System.Drawing.Brushes.Black, new System.Drawing.PointF(10, 10));
                
                DebugHelper.WriteDebug("Drew title");
                
                if (pageCards.Count == 0)
                {
                    DebugHelper.WriteDebug("No cards to draw");
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    return new Bitmap(ms);
                }
                
                // FIXED CARD DIMENSIONS: Always 2.5" x 3.5" scaled for preview
                var cardWidthPreview = (float)(CARD_WIDTH_POINTS * pageScale);
                var cardHeightPreview = (float)(CARD_HEIGHT_POINTS * pageScale);
                var spacingPreview = (float)(options.CardSpacing * pageScale);
                
                DebugHelper.WriteDebug($"FIXED Preview card layout: {cardWidthPreview:F1}x{cardHeightPreview:F1} (EXACTLY 2.5\" x 3.5\" scaled by {pageScale:F3}) - spacing: {spacingPreview:F1}");
                
                // Calculate total grid size
                var totalGridWidth = actualCardsPerRow * cardWidthPreview + (actualCardsPerRow - 1) * spacingPreview;
                var totalGridHeight = actualCardsPerColumn * cardHeightPreview + (actualCardsPerColumn - 1) * spacingPreview;
                
                // Calculate available space for grid (scaled margins)
                var marginLeftScaled = options.LeftMargin * pageScale;
                var marginRightScaled = options.RightMargin * pageScale;
                var marginTopScaled = options.TopMargin * pageScale;
                var marginBottomScaled = options.BottomMargin * pageScale;
                var titleSpaceScaled = 30 * pageScale; // Space for title
                
                var availableWidth = previewWidth - marginLeftScaled - marginRightScaled;
                var availableHeight = previewHeight - marginTopScaled - marginBottomScaled - titleSpaceScaled;
                
                // Center the grid on the page
                var gridStartX = marginLeftScaled + (availableWidth - totalGridWidth) / 2;
                var gridStartY = marginTopScaled + titleSpaceScaled + (availableHeight - totalGridHeight) / 2;
                
                DebugHelper.WriteDebug($"  Grid start: {gridStartX:F1}, {gridStartY:F1}");
                
                // Check if grid fits
                if (totalGridWidth > availableWidth || totalGridHeight > availableHeight)
                {
                    DebugHelper.WriteDebug($"WARNING: Preview grid ({totalGridWidth:F1} x {totalGridHeight:F1}) is larger than available space ({availableWidth:F1} x {availableHeight:F1})");
                }
                else
                {
                    DebugHelper.WriteDebug($"SUCCESS: Preview grid fits within page margins");
                }
                
                // Draw cards with FIXED spacing
                for (int row = 0; row < actualCardsPerColumn; row++)
                {
                    for (int col = 0; col < actualCardsPerRow; col++)
                    {
                        var cardIndex = row * actualCardsPerRow + col;
                        
                        if (cardIndex < pageCards.Count)
                        {
                            var card = pageCards[cardIndex];
                            
                            // Calculate exact position with spacing
                            var exactX = gridStartX + col * (cardWidthPreview + spacingPreview);
                            var exactY = gridStartY + row * (cardHeightPreview + spacingPreview);
                            
                            // Round for display
                            var x = (int)Math.Round(exactX);
                            var y = (int)Math.Round(exactY);
                            
                            DebugHelper.WriteDebug($"Preview card {cardIndex}: {card?.Name ?? "NULL"}");
                            DebugHelper.WriteDebug($"  Position: ({x}, {y})");
                            
                            DrawPreviewCard(graphics, card, options, x, y, (int)Math.Round(cardWidthPreview), (int)Math.Round(cardHeightPreview));
                        }
                    }
                }
                
                // Draw cutting lines for the entire grid (outside card areas)
                if (options.ShowCuttingLines)
                {
                    DrawPreviewGridCuttingLines(graphics, options, (float)gridStartX, (float)gridStartY, 
                        actualCardsPerRow, actualCardsPerColumn, cardWidthPreview, cardHeightPreview, spacingPreview);
                }
                
                DebugHelper.WriteDebug("Finished drawing cards, converting to Avalonia Bitmap");
                
                // Convert to Avalonia Bitmap
                using var outputStream = new MemoryStream();
                bitmap.Save(outputStream, System.Drawing.Imaging.ImageFormat.Png);
                outputStream.Position = 0;
                
                var avaloniaB = new Bitmap(outputStream);
                DebugHelper.WriteDebug("Successfully created Avalonia Bitmap");
                
                return avaloniaB;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error creating simple preview: {ex.Message}");
                DebugHelper.WriteDebug($"Stack trace: {ex.StackTrace}");
                return CreateFallbackPreview(cards, options);
            }
        }

        private void DrawPreviewCard(System.Drawing.Graphics graphics, Card card, PdfGenerationOptions options, int x, int y, int width, int height)
        {
            try
            {
                var rect = new System.Drawing.Rectangle(x, y, width, height);
                
                DebugHelper.WriteDebug($"Drawing preview card {card?.Name ?? "NULL"} at ({x},{y}) size {width}x{height}");
                
                if (card?.ImageData != null && card.ImageData.Length > 0)
                {
                    try
                    {
                        using var imageStream = new MemoryStream(card.ImageData);
                        using var cardImage = System.Drawing.Image.FromStream(imageStream);
                        
                        graphics.DrawImage(cardImage, rect);
                        DebugHelper.WriteDebug($"Drew preview image for {card.Name}");
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteDebug($"Error drawing card image for {card.Name}: {ex.Message}");
                        DrawPreviewPlaceholder(graphics, card, rect, "Image Error");
                    }
                }
                else
                {
                    DebugHelper.WriteDebug($"No image data for {card?.Name ?? "NULL"}");
                    DrawPreviewPlaceholder(graphics, card, rect, "No Image");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error drawing preview card {card?.Name ?? "NULL"}: {ex.Message}");
                
                try
                {
                    DrawPreviewPlaceholder(graphics, card, new System.Drawing.Rectangle(x, y, width, height), "Error");
                }
                catch (Exception ex2)
                {
                    DebugHelper.WriteDebug($"Error in fallback placeholder: {ex2.Message}");
                }
            }
        }

        private void DrawPreviewPlaceholder(System.Drawing.Graphics graphics, Card card, System.Drawing.Rectangle rect, string message)
        {
            try
            {
                // Fill background
                graphics.FillRectangle(System.Drawing.Brushes.LightGray, rect);
                graphics.DrawRectangle(System.Drawing.Pens.Gray, rect);
                
                // Draw text safely
                using var font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold);
                using var smallFont = new System.Drawing.Font("Arial", 6);
                
                var stringFormat = new System.Drawing.StringFormat
                {
                    Alignment = System.Drawing.StringAlignment.Center,
                    LineAlignment = System.Drawing.StringAlignment.Center
                };
                
                graphics.DrawString(message ?? "Error", font, System.Drawing.Brushes.Black, rect, stringFormat);
                
                var nameRect = new System.Drawing.Rectangle(rect.X, rect.Y + rect.Height * 2 / 3, rect.Width, rect.Height / 3);
                graphics.DrawString(card?.Name ?? "Unknown", smallFont, System.Drawing.Brushes.DarkGray, nameRect, stringFormat);
                
                DebugHelper.WriteDebug($"Drew placeholder for {card?.Name ?? "NULL"}: {message}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error drawing placeholder: {ex.Message}");
                try
                {
                    graphics.FillRectangle(System.Drawing.Brushes.Pink, rect);
                }
                catch
                {
                    // Give up
                }
            }
        }

        private void DrawPreviewGridCuttingLines(System.Drawing.Graphics graphics, PdfGenerationOptions options, 
            float gridStartX, float gridStartY, int cardsPerRow, int cardsPerColumn, 
            float cardWidth, float cardHeight, float spacing)
        {
            try
            {
                var color = ParseSystemDrawingColor(options.CuttingLineColor);
                using var pen = new System.Drawing.Pen(color, options.CuttingLineThickness);
                
                if (options.IsCuttingLineDashed)
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                }
                
                var extension = options.CuttingLineExtension;
                
                DebugHelper.WriteDebug($"Drawing grid cutting lines with {extension}pt extension OUTSIDE grid only");
                
                // Calculate grid boundaries
                var gridLeft = gridStartX;
                var gridRight = gridStartX + cardsPerRow * cardWidth + (cardsPerRow - 1) * spacing;
                var gridTop = gridStartY;
                var gridBottom = gridStartY + cardsPerColumn * cardHeight + (cardsPerColumn - 1) * spacing;
                
                // Draw horizontal cutting line extensions (only outside grid)
                for (int row = 0; row <= cardsPerColumn; row++)
                {
                    var y = gridStartY + row * (cardHeight + spacing);
                    if (row > 0) y -= spacing; // Adjust for spacing except at top
                    
                    // Left extension
                    graphics.DrawLine(pen, gridLeft - extension, y, gridLeft, y);
                    
                    // Right extension  
                    graphics.DrawLine(pen, gridRight, y, gridRight + extension, y);
                }
                
                // Draw vertical cutting line extensions (only outside grid)
                for (int col = 0; col <= cardsPerRow; col++)
                {
                    var x = gridStartX + col * (cardWidth + spacing);
                    if (col > 0) x -= spacing; // Adjust for spacing except at left
                    
                    // Top extension
                    graphics.DrawLine(pen, x, gridTop - extension, x, gridTop);
                    
                    // Bottom extension
                    graphics.DrawLine(pen, x, gridBottom, x, gridBottom + extension);
                }
                
                DebugHelper.WriteDebug("Completed drawing grid cutting line extensions");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error drawing grid cutting lines: {ex.Message}");
            }
        }

        private System.Drawing.Color ParseSystemDrawingColor(string colorString)
        {
            try
            {
                if (!colorString.StartsWith("#") || colorString.Length != 7) return System.Drawing.Color.Red;
                var hex = colorString.Substring(1);
                var r = Convert.ToByte(hex.Substring(0, 2), 16);
                var g = Convert.ToByte(hex.Substring(2, 2), 16);
                var b = Convert.ToByte(hex.Substring(4, 2), 16);
                return System.Drawing.Color.FromArgb(r, g, b);
            }
            catch
            {
                return System.Drawing.Color.Red;
            }
        }

        private Bitmap CreateFallbackPreview(CardCollection cards, PdfGenerationOptions options)
        {
            try
            {
                // Create a minimal fallback preview
                var width = 400;
                var height = 300;
                
                var bitmap = new WriteableBitmap(new Avalonia.PixelSize(width, height), new Avalonia.Vector(96, 96));
                
                using (var context = bitmap.Lock())
                {
                    unsafe
                    {
                        var ptr = (uint*)context.Address;
                        var pixelCount = width * height;
                        for (int i = 0; i < pixelCount; i++)
                        {
                            ptr[i] = 0xFFFFFFFF; // White
                        }
                    }
                }
                
                return bitmap;
            }
            catch
            {
                return null!;
            }
        }

        private XFont GetSafeFont(string familyName, double size, XFontStyleEx style)
        {
            try
            {
                return new XFont(familyName, size, style);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error creating font {familyName}: {ex.Message}, falling back to default");
                
                try
                {
                    return new XFont("Segoe UI", size, style);
                }
                catch
                {
                    try
                    {
                        return new XFont("Helvetica", size, style);
                    }
                    catch
                    {
                        return new XFont("Times", size, style);
                    }
                }
            }
        }

        private void DrawPlaceholder(XGraphics gfx, Card card, XUnit x, XUnit y, XUnit width, XUnit height, string message)
        {
            // Draw border
            var pen = new XPen(XColors.Gray, 1);
            gfx.DrawRectangle(pen, XBrushes.LightGray, new XRect(x, y, width, height));
            
            // Draw text with safe font handling
            try
            {
                var font = GetSafeFont("Arial", 10, XFontStyleEx.Bold);
                var textRect = new XRect(x, y, width, height);
                
                gfx.DrawString(message, font, XBrushes.Black, textRect, XStringFormats.Center);
                
                var smallFont = GetSafeFont("Arial", 8, XFontStyleEx.Regular);
                var nameRect = new XRect(x, y + height * 0.6, width, height * 0.4);
                gfx.DrawString(card.Name ?? "Unknown", smallFont, XBrushes.DarkGray, nameRect, XStringFormats.Center);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error drawing placeholder text: {ex.Message}");
            }
        }

        private void DrawPdfGridCuttingLines(XGraphics gfx, PdfGenerationOptions options, 
            double gridStartX, double gridStartY, int cardsPerRow, int cardsPerColumn, 
            double cardWidth, double cardHeight)
        {
            try
            {
                var color = ParseColor(options.CuttingLineColor);
                var pen = new XPen(color, options.CuttingLineThickness);
        
                if (options.IsCuttingLineDashed)
                {
                    pen.DashStyle = XDashStyle.Dash;
                }
        
                var extension = options.CuttingLineExtension;
                
                DebugHelper.WriteDebug($"Drawing PDF grid cutting lines with {extension}pt extension OUTSIDE grid only");
                
                // Calculate grid boundaries
                var gridLeft = gridStartX;
                var gridRight = gridStartX + cardsPerRow * cardWidth + (cardsPerRow - 1) * options.CardSpacing;
                var gridTop = gridStartY;
                var gridBottom = gridStartY + cardsPerColumn * cardHeight + (cardsPerColumn - 1) * options.CardSpacing;
                
                // Draw horizontal cutting line extensions (only outside grid)
                for (int row = 0; row <= cardsPerColumn; row++)
                {
                    var y = gridStartY + row * (cardHeight + options.CardSpacing);
                    if (row > 0) y -= options.CardSpacing; // Adjust for spacing except at top
                    
                    var yUnit = XUnit.FromPoint(y);
                    
                    // Left extension
                    gfx.DrawLine(pen, 
                        XUnit.FromPoint(gridLeft - extension), yUnit, 
                        XUnit.FromPoint(gridLeft), yUnit);
                    
                    // Right extension  
                    gfx.DrawLine(pen, 
                        XUnit.FromPoint(gridRight), yUnit, 
                        XUnit.FromPoint(gridRight + extension), yUnit);
                }
                
                // Draw vertical cutting line extensions (only outside grid)
                for (int col = 0; col <= cardsPerRow; col++)
                {
                    var x = gridStartX + col * (cardWidth + options.CardSpacing);
                    if (col > 0) x -= options.CardSpacing; // Adjust for spacing except at left
                    
                    var xUnit = XUnit.FromPoint(x);
                    
                    // Top extension
                    gfx.DrawLine(pen, 
                        xUnit, XUnit.FromPoint(gridTop - extension), 
                        xUnit, XUnit.FromPoint(gridTop));
                    
                    // Bottom extension
                    gfx.DrawLine(pen, 
                        xUnit, XUnit.FromPoint(gridBottom), 
                        xUnit, XUnit.FromPoint(gridBottom + extension));
                }
                
                DebugHelper.WriteDebug("Completed drawing PDF grid cutting line extensions");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error drawing PDF grid cutting lines: {ex.Message}");
            }
        }

        private XColor ParseColor(string colorString)
        {
            try
            {
                if (colorString.StartsWith("#"))
                {
                    var hex = colorString.Substring(1);
                    if (hex.Length == 6)
                    {
                        var r = Convert.ToByte(hex.Substring(0, 2), 16);
                        var g = Convert.ToByte(hex.Substring(2, 2), 16);
                        var b = Convert.ToByte(hex.Substring(4, 2), 16);
                        return XColor.FromArgb(r, g, b);
                    }
                }
                return XColors.Red; // Default to red
            }
            catch
            {
                return XColors.Red;
            }
        }
    }

    // Improved font resolver for PDFsharp
    public class SafeFontResolver : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            try
            {
                DebugHelper.WriteDebug($"Resolving font: {familyName}, Bold: {isBold}, Italic: {isItalic}");
                
                // Map common font names to safer alternatives
                var safeFontName = familyName.ToLower() switch
                {
                    "arial" => "Arial",
                    "helvetica" => "Arial", // Fallback to Arial
                    "times" => "Times New Roman",
                    "courier" => "Courier New",
                    _ => familyName
                };
                
                return new FontResolverInfo(safeFontName);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error in ResolveTypeface: {ex.Message}");
                return new FontResolverInfo("Arial"); // Safe fallback
            }
        }

        public byte[] GetFont(string faceName)
        {
            try
            {
                DebugHelper.WriteDebug($"GetFont called for: {faceName}");
                // Return null to use system fonts - this is the safest approach
                return null;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error in GetFont: {ex.Message}");
                return null;
            }
        }
    }
}