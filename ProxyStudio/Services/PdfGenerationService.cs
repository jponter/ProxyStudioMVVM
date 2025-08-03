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
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ProxyStudio.Services
{
    /// <summary>
    /// Service for generating PDFs from card collections with high DPI support.
    /// </summary>
    /// <remarks>
    /// This service handles both PDF generation and preview image creation,
    /// ensuring cards are rendered at exact dimensions regardless of DPI settings.
    /// 
    /// <seealso cref="IPdfGenerationService"/>
    /// <seealso cref="PdfGenerationOptions"/>
    /// <seealso cref="PdfGenerationProgress"/>
    /// <seealso cref="CardCollection"/>
    /// <seealso cref="Card"/>
    ///
    /// </remarks>

    public interface IPdfGenerationService
    {
        Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options,
            IProgress<PdfGenerationProgress>? progress = null);

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
        public int PreviewDpi { get; set; } = 150; // Lower for performance
        public int PrintDpi { get; set; } = 300; // High quality for print
        public int PreviewQuality { get; set; } = 85;
        public string PageSize { get; set; } = "A4";
    }

    public class PdfGenerationService : IPdfGenerationService
    {
        // Card dimensions in points (72 DPI standard) - FIXED at exactly 2.5" x 3.5"
        // UPDATED: Card dimensions in points (72 DPI standard) - FIXED at exactly 63mm × 88mm
        private const double CARD_WIDTH_MM = 63.0;
        private const double CARD_HEIGHT_MM = 88.0;

        // Convert mm to inches, then to points (1 inch = 25.4mm, 1 inch = 72 points)
        private const double CARD_WIDTH_INCHES = CARD_WIDTH_MM / 25.4; // 2.480 inches
        private const double CARD_HEIGHT_INCHES = CARD_HEIGHT_MM / 25.4; // 3.465 inches

        private const double CARD_WIDTH_POINTS = CARD_WIDTH_INCHES * 72; // 178.583 points
        private const double CARD_HEIGHT_POINTS = CARD_HEIGHT_INCHES * 72; // 249.449 points

        private readonly ILogger<PdfGenerationService> _logger;
        private readonly IErrorHandlingService _errorHandler;


        public PdfGenerationService(ILogger<PdfGenerationService> logger, IErrorHandlingService errorHandler)
        {
            _logger = logger;
            _errorHandler = errorHandler;

            // Set up PDFsharp with better font handling
            try
            {
                GlobalFontSettings.FontResolver = new SafeFontResolver();
                _logger.LogInformation("PDFsharp font resolver set up successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up PDFsharp font resolver");
            }
        }

        // Add this helper method to convert problematic images to PNG
        private byte[]? ConvertToPng(byte[] imageData, string cardName)
        {
            try
            {
                _logger.LogDebug($"Converting {cardName} to PNG for PDFsharp compatibility...");

                using var image = Image.Load<Rgba32>(imageData);

                var pngEncoder = new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestCompression,
                    ColorType = PngColorType.RgbWithAlpha
                };

                using var outputStream = new MemoryStream();
                image.Save(outputStream, pngEncoder);

                var pngData = outputStream.ToArray();
                _logger.LogDebug($"Successfully converted {cardName} to PNG: {pngData.Length} bytes");

                return pngData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add test cards");
                _errorHandler.HandleExceptionAsync(ex, "Failed to add test cards", "AddTestCards");

                return null;
            }
        }

        public async Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options,
            IProgress<PdfGenerationProgress>? progress = null)
        {
            using var scope = _logger.BeginScope("Generate PDF Async");
            _logger.LogInformation("Starting PDF generation: {CardCount} cards at {PrintDpi} DPI", cards.Count,
                options.PrintDpi);

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
                    progressInfo.CurrentOperation = "Pre-processing all images in parallel...";

                    progress?.Report(progressInfo);

                    _logger.LogDebug($"=== PDF GENERATION START (PARALLEL) ===");
                    _logger.LogDebug($"Target DPI: {options.PrintDpi}");
                    _logger.LogDebug($"Cards: {cards.Count}, Pages: {totalPages}");

                    // ✅ NEW: PRE-PROCESS ALL IMAGES IN PARALLEL
                    var processedImages =
                        PreProcessAllImagesParallel(cards, options, progress, progressInfo, startTime);

                    // Create PDF document
                    progressInfo.CurrentStep = cards.Count + 1;
                    progressInfo.CurrentOperation = "Creating PDF document...";
                    progress?.Report(progressInfo);

                    var document = new PdfDocument();
                    ApplyHighDpiSettings(document, options.PrintDpi);

                    _logger.LogDebug($"Parallel pre-processing complete, now drawing {totalPages} pages sequentially");
                    _logger.LogDebug($"Successfully processed {processedImages.Count} images in parallel");

                    // Process each page (sequential due to PDFsharp thread-safety)
                    for (var pageIndex = 0; pageIndex < totalPages; pageIndex++)
                    {
                        progressInfo.CurrentPage = pageIndex + 1;
                        progressInfo.CurrentStep = cards.Count + 2 + pageIndex;
                        progressInfo.CurrentOperation = $"Drawing page {pageIndex + 1} of {totalPages}...";
                        progressInfo.ElapsedTime = DateTime.Now - startTime;

                        // Estimate remaining time for page drawing phase
                        if (pageIndex > 0)
                        {
                            var averageTimePerPage = (DateTime.Now - startTime).TotalSeconds / (pageIndex + 1);
                            var remainingPages = totalPages - pageIndex;
                            progressInfo.EstimatedRemainingTime =
                                TimeSpan.FromSeconds(averageTimePerPage * remainingPages);
                        }

                        progress?.Report(progressInfo);

                        var page = document.AddPage();
                        page.Size = GetPageSize(options.PageSize);
                        page.Orientation = options.IsPortrait
                            ? PdfSharp.PageOrientation.Portrait
                            : PdfSharp.PageOrientation.Landscape;

                        var gfx = XGraphics.FromPdfPage(page);
                        ApplyHighDpiTransformation(gfx, options.PrintDpi);

                        var startCardIndex = pageIndex * cardsPerPage;
                        var pageCards = cards.Skip(startCardIndex).Take(cardsPerPage).ToList();

                        _logger.LogDebug(
                            $"Page {pageIndex + 1}: Drawing {pageCards.Count} cards using pre-processed images");

                        // ✅ FAST DRAWING: Use pre-processed images (no processing during drawing!)
                        DrawCardGridWithPreProcessedImages(gfx, pageCards, processedImages, options,
                            page.Width, page.Height, pageIndex + 1, totalPages);

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

                    _logger.LogInformation($"=== PDF GENERATION COMPLETE (PARALLEL) ===");
                    _logger.LogDebug($"Final PDF size: {pdfBytes.Length / (1024.0 * 1024.0):F2} MB");
                    _logger.LogDebug($"Total generation time: {duration.TotalSeconds:F1} seconds");
                    _logger.LogDebug($"Average per card: {duration.TotalMilliseconds / cards.Count:F1} ms");
                    _logger.LogDebug("Parallel processing saved significant time during image processing phase");

                    return pdfBytes;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error generating PDF: {ex.Message}");

                    // Report error
                    progress?.Report(new PdfGenerationProgress
                    {
                        CurrentStep = 0,
                        TotalSteps = 1,
                        CurrentOperation = $"Error: {ex.Message}",
                        CurrentCardName = ""
                    });
                    _errorHandler.HandleExceptionAsync(ex, "PDF Generation Error", "GeneratePdfAsync");
                    throw;
                }
            });
        }

        private Dictionary<string, byte[]> PreProcessAllImagesParallel(CardCollection cards,
            PdfGenerationOptions options,
            IProgress<PdfGenerationProgress>? progress, PdfGenerationProgress progressInfo, DateTime startTime)
        {
            var processedImages = new ConcurrentDictionary<string, byte[]>();
            var completedCount = 0;

            // Use ParallelOptions to control parallel execution
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2)
            };

            _logger.LogDebug(
                $"Pre-processing {cards.Count} images with {parallelOptions.MaxDegreeOfParallelism} max parallel threads using Parallel.ForEach");
            _logger.LogDebug($"System has {Environment.ProcessorCount} logical processors");

            var parallelStartTime = DateTime.Now;

            // Use Parallel.ForEach which guarantees parallel execution for CPU-bound work
            try
            {
                Parallel.ForEach(cards, parallelOptions, card =>
                {
                    var threadId = Thread.CurrentThread.ManagedThreadId;

                    try
                    {
                        if (card.ImageData != null && card.ImageData.Length > 0)
                        {
                            _logger.LogDebug($"PARALLEL PDF: Processing {card.Name} on thread {threadId}");

                            var cardStartTime = DateTime.Now;
                            var processedImage = ProcessImageForHighDpiPdf(card.ImageData, card.Name, options.PrintDpi,
                                card.EnableBleed);
                            var cardProcessTime = DateTime.Now - cardStartTime;

                            if (processedImage != null)
                            {
                                processedImages[card.Id] = processedImage;
                                _logger.LogDebug(
                                    $"PARALLEL PDF SUCCESS: {card.Name} ({processedImage.Length} bytes) on thread {threadId} in {cardProcessTime.TotalSeconds:F1}s");
                            }
                            else
                            {
                                _logger.LogWarning(
                                    $"PARALLEL PDF WARNING: {card.Name} processing returned null on thread {threadId}");
                            }
                        }
                        else
                        {
                            _logger.LogError($"PARALLEL PDF SKIP: {card.Name} has no image data on thread {threadId}");
                        }

                        // Thread-safe progress update
                        var newCompletedCount = Interlocked.Increment(ref completedCount);

                        // Update progress (this should be thread-safe)
                        var tempProgressInfo = new PdfGenerationProgress
                        {
                            TotalSteps = progressInfo.TotalSteps,
                            CurrentStep = newCompletedCount,
                            CurrentCardName = card.Name,
                            CurrentOperation =
                                $"Pre-processed {newCompletedCount}/{cards.Count} images (Thread {threadId})",
                            ElapsedTime = DateTime.Now - startTime
                        };

                        // Estimate remaining time for parallel processing phase
                        if (newCompletedCount > 1)
                        {
                            var averageTimePerCard = tempProgressInfo.ElapsedTime.TotalSeconds / newCompletedCount;
                            var remainingCards = cards.Count - newCompletedCount;
                            tempProgressInfo.EstimatedRemainingTime =
                                TimeSpan.FromSeconds(averageTimePerCard * remainingCards);
                        }

                        progress?.Report(tempProgressInfo);

                        _logger.LogDebug(
                            $"PROGRESS UPDATE: Completed {newCompletedCount}/{cards.Count} images, thread {threadId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            $"ERROR: Processing {card?.Name ?? "unknown"} failed on thread {threadId}: {ex.Message}");

                        // Still increment counter for failed cards
                        Interlocked.Increment(ref completedCount);
                    }
                });

                var parallelEndTime = DateTime.Now;
                var parallelDuration = parallelEndTime - parallelStartTime;

                _logger.LogDebug($"=== PARALLEL PROCESSING COMPLETE ===");
                _logger.LogDebug($"Processed {processedImages.Count}/{cards.Count} images successfully");
                _logger.LogDebug($"Parallel processing time: {parallelDuration.TotalSeconds:F1} seconds");
                _logger.LogDebug($"Average per image: {parallelDuration.TotalMilliseconds / cards.Count:F0} ms");

                if (cards.Count > 1)
                {
                    var theoreticalSequentialTime = cards.Count * 3.7; // Based on your previous 3.7s per card
                    var speedupRatio = theoreticalSequentialTime / parallelDuration.TotalSeconds;
                    _logger.LogDebug($"Estimated speedup: {speedupRatio:F1}x faster than sequential processing");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"ERROR: Parallel.ForEach failed: {ex.Message}");
                _logger.LogCritical($"Stack trace: {ex.StackTrace}");
                throw;
            }

            return new Dictionary<string, byte[]>(processedImages);
        }

// ✅ NEW: FAST DRAWING METHOD USING PRE-PROCESSED IMAGES
        private void DrawCardGridWithPreProcessedImages(XGraphics gfx, List<Card> pageCards,
            Dictionary<string, byte[]> processedImages, PdfGenerationOptions options,
            XUnit pageWidth, XUnit pageHeight, int currentPage, int totalPages)
        {
            // Use fixed layout based on orientation
            var actualCardsPerRow = options.IsPortrait ? 3 : 4;
            var actualCardsPerColumn = options.IsPortrait ? 3 : 2;

            var cardWidthPt = CARD_WIDTH_POINTS;
            var cardHeightPt = CARD_HEIGHT_POINTS;

            var totalHorizontalSpacing = (actualCardsPerRow - 1) * options.CardSpacing;
            var totalVerticalSpacing = (actualCardsPerColumn - 1) * options.CardSpacing;
            var totalGridWidthPt = actualCardsPerRow * cardWidthPt + totalHorizontalSpacing;
            var totalGridHeightPt = actualCardsPerColumn * cardHeightPt + totalVerticalSpacing;

            var availableWidthPt = pageWidth.Point - (options.LeftMargin + options.RightMargin);
            var availableHeightPt = pageHeight.Point - (options.TopMargin + options.BottomMargin);

            var startXPt = options.LeftMargin + (availableWidthPt - totalGridWidthPt) / 2;
            var startYPt = options.TopMargin + (availableHeightPt - totalGridHeightPt) / 2;

            // // Draw title
            // try
            // {
            //     var font = GetSafeFont("Arial", 14, XFontStyleEx.Bold);
            //     var title = totalPages > 1 
            //         ? $"Card Collection - Page {currentPage} of {totalPages} ({actualCardsPerRow}x{actualCardsPerColumn}) - {options.PrintDpi} DPI (Parallel)"
            //         : $"Card Collection - {pageCards.Count} cards ({actualCardsPerRow}x{actualCardsPerColumn}) - {options.PrintDpi} DPI (Parallel)";
            //
            //     gfx.DrawString(title, font, XBrushes.Black,
            //         new XPoint(XUnit.FromPoint(options.LeftMargin), XUnit.FromPoint(options.TopMargin)));
            // }
            // catch (Exception ex)
            // {
            //     _logger.LogError($"Error drawing title: {ex.Message}");
            // }

            // Draw cards using pre-processed images - this is now FAST!
            for (var row = 0; row < actualCardsPerColumn; row++)
            for (var col = 0; col < actualCardsPerRow; col++)
            {
                var cardIndex = row * actualCardsPerRow + col;

                if (cardIndex < pageCards.Count)
                {
                    var card = pageCards[cardIndex];

                    var xPt = startXPt + col * (cardWidthPt + options.CardSpacing);
                    var yPt = startYPt + row * (cardHeightPt + options.CardSpacing);

                    var x = XUnit.FromPoint(xPt);
                    var y = XUnit.FromPoint(yPt);
                    var cardWidth = XUnit.FromPoint(cardWidthPt);
                    var cardHeight = XUnit.FromPoint(cardHeightPt);

                    // ✅ FAST: Use pre-processed image (no processing during drawing!)
                    DrawCardWithPreProcessedImage(gfx, card, processedImages, x, y, cardWidth, cardHeight);
                }
            }

            // Draw cutting lines
            if (options.ShowCuttingLines)
                DrawPdfGridCuttingLines(gfx, options, startXPt, startYPt,
                    actualCardsPerRow, actualCardsPerColumn, cardWidthPt, cardHeightPt);
        }

// ✅ NEW: DRAW CARD USING PRE-PROCESSED IMAGE (FAST!)
        private void DrawCardWithPreProcessedImage(XGraphics gfx, Card card, Dictionary<string, byte[]> processedImages,
            XUnit x, XUnit y, XUnit width, XUnit height)
        {
            try
            {
                // Use pre-processed image (no processing time!)
                if (processedImages.TryGetValue(card.Id, out var processedImageData))
                {
                    XImage? xImage = null;
                    try
                    {
                        using var ms = new MemoryStream(processedImageData);

                        try
                        {
                            xImage = XImage.FromStream(ms);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                $"Failed to create XImage for {card.Name}: {ex.Message}, converting to PNG...");

                            // Fallback: convert to PNG
                            var pngData = ConvertToPng(processedImageData, card.Name);
                            if (pngData != null)
                            {
                                using var pngMs = new MemoryStream(pngData);
                                xImage = XImage.FromStream(pngMs);
                            }
                        }

                        if (xImage != null)
                        {
                            // Draw the pre-processed image - FAST!
                            gfx.DrawImage(xImage, new XRect(x.Point, y.Point, width.Point, height.Point));
                            _logger.LogDebug($"FAST DRAW: Drew pre-processed image for {card.Name}");
                        }
                        else
                        {
                            _logger.LogDebug($"ERROR: Failed to create XImage for {card.Name}");
                            DrawPlaceholder(gfx, card, x, y, width, height, "Image Load Error");
                        }
                    }
                    finally
                    {
                        xImage?.Dispose();
                    }
                }
                else
                {
                    _logger.LogWarning($"WARNING: No pre-processed image found for {card.Name}");
                    DrawPlaceholder(gfx, card, x, y, width, height, card.EnableBleed ? "No Image (Bleed)" : "No Image");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: Exception in DrawCardWithPreProcessedImage for {card.Name}: {ex.Message}");
                DrawPlaceholder(gfx, card, x, y, width, height, "Error");
            }
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
                _logger.LogError($"Error drawing title: {ex.Message}");
            }

            // Draw cards with individual progress updates
            for (var row = 0; row < actualCardsPerColumn; row++)
            for (var col = 0; col < actualCardsPerRow; col++)
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
                        progressInfo.EstimatedRemainingTime =
                            TimeSpan.FromSeconds(averageTimePerCard * remainingCards);
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
                    _logger.LogDebug(
                        $"Generating preview for {cards?.Count ?? 0} cards using Preview DPI: {options.PreviewDpi} (Print DPI will be: {options.PrintDpi})");

                    if (options == null)
                    {
                        _logger.LogError("ERROR: options is null!");
                        return CreateFallbackPreview(cards, options);
                    }

                    // Use preview DPI for preview generation (not print DPI)
                    var previewBitmap = CreateSimplePreview(cards, options, options.PreviewDpi);

                    _logger.LogDebug(
                        $"Preview generated successfully using {options.PreviewDpi} DPI (final PDF will use {options.PrintDpi} DPI)");
                    return previewBitmap;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error generating preview: {ex.Message}");
                    _logger.LogError($"Stack trace: {ex.StackTrace}");
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

                _logger.LogDebug($"Applied high-DPI settings: {targetDpi} DPI");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Warning: Could not apply high-DPI document settings: {ex.Message}");
            }
        }

        private void ApplyHighDpiTransformation(XGraphics gfx, int targetDpi)
        {
            // REMOVED: The graphics transformation was breaking card dimensions
            // High-DPI is now handled purely through image processing, not graphics scaling
            // This ensures cards remain exactly 2.5" × 3.5" regardless of DPI setting

            _logger.LogDebug("Skipped graphics DPI transformation - handling DPI through image processing only");
            _logger.LogDebug($"Cards will maintain EXACT  dimensions at {targetDpi} DPI");
        }

        private (double Width, double Height) GetPageDimensions(string pageSize, bool isLandscape)
        {
            // Page dimensions in points (72 DPI)
            var dimensions = pageSize?.ToUpper() switch
            {
                "A3" => (841.89, 1190.55), // A3: 297 × 420 mm
                "A4" => (595.28, 841.89), // A4: 210 × 297 mm  
                "A5" => (419.53, 595.28), // A5: 148 × 210 mm
                "LETTER" => (612.0, 792.0), // Letter: 8.5 × 11 inches
                "LEGAL" => (612.0, 1008.0), // Legal: 8.5 × 14 inches
                "TABLOID" => (792.0, 1224.0), // Tabloid: 11 × 17 inches
                _ => (595.28, 841.89) // Default to A4
            };

            // Swap dimensions for landscape
            if (isLandscape) return (dimensions.Item2, dimensions.Item1);

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

        private void DrawCardGrid(XGraphics gfx, List<Card> pageCards, PdfGenerationOptions options, XUnit pageWidth,
            XUnit pageHeight, int currentPage, int totalPages)
        {
            // Use user's orientation choice for layout
            var actualCardsPerRow = options.IsPortrait ? 3 : 4;
            var actualCardsPerColumn = options.IsPortrait ? 3 : 2;

            _logger.LogDebug(
                $"Drawing {pageCards.Count} cards in FIXED {actualCardsPerRow}x{actualCardsPerColumn} grid ({(options.IsPortrait ? "Portrait" : "Landscape")}) with {options.CardSpacing}pt spacing (Page {currentPage} of {totalPages})");

            // FIXED CARD DIMENSIONS: Always exactly 2.5" x 3.5" (180 x 252 points)
            var cardWidthPt = CARD_WIDTH_POINTS;
            var cardHeightPt = CARD_HEIGHT_POINTS;

            _logger.LogDebug(
                $"Card dimensions: {cardWidthPt:F3}x{cardHeightPt:F3} points (FIXED 2.5\" x 3.5\") - spacing: {options.CardSpacing}pt");

            // Calculate total grid size
            var totalHorizontalSpacing = (actualCardsPerRow - 1) * options.CardSpacing;
            var totalVerticalSpacing = (actualCardsPerColumn - 1) * options.CardSpacing;
            var totalGridWidthPt = actualCardsPerRow * cardWidthPt + totalHorizontalSpacing;
            var totalGridHeightPt = actualCardsPerColumn * cardHeightPt + totalVerticalSpacing;

            // Calculate available space
            var availableWidthPt = pageWidth.Point - (options.LeftMargin + options.RightMargin);
            var availableHeightPt =
                pageHeight.Point - (options.TopMargin + options.BottomMargin + 50); // Space for title

            // Center the grid on the page
            var startXPt = options.LeftMargin + (availableWidthPt - totalGridWidthPt) / 2;
            var startYPt = options.TopMargin + 30 + (availableHeightPt - totalGridHeightPt) / 2;

            _logger.LogDebug(
                $"Grid layout: start=({startXPt:F3}, {startYPt:F3}), total size=({totalGridWidthPt:F3}x{totalGridHeightPt:F3})");

            // Draw title with page info
            try
            {
                var font = GetSafeFont("Arial", 14, XFontStyleEx.Bold);
                var title = totalPages > 1
                    ? $"Card Collection - Page {currentPage} of {totalPages} ({actualCardsPerRow}x{actualCardsPerColumn})"
                    : $"Card Collection - {pageCards.Count} cards ({actualCardsPerRow}x{actualCardsPerColumn})";

                gfx.DrawString(title, font, XBrushes.Black,
                    new XPoint(XUnit.FromPoint(options.LeftMargin), XUnit.FromPoint(options.TopMargin)));
                _logger.LogDebug("Drew title successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error drawing title: {ex.Message}");
            }

            // Draw cards with precise positioning
            for (var row = 0; row < actualCardsPerColumn; row++)
            for (var col = 0; col < actualCardsPerRow; col++)
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

                    _logger.LogDebug(
                        $"Drawing card {cardIndex} ({card.Name}) at ({xPt:F3}, {yPt:F3}) - row {row}, col {col}");

                    DrawCard(gfx, card, options, x, y, cardWidth, cardHeight);
                }
            }

            // Draw cutting lines for the entire grid (outside card areas)
            if (options.ShowCuttingLines)
                DrawPdfGridCuttingLines(gfx, options, startXPt, startYPt,
                    actualCardsPerRow, actualCardsPerColumn, cardWidthPt, cardHeightPt);
        }

        // Update DrawCard method to pass bleed flag and fix image loading issue
        private void DrawCard(XGraphics gfx, Card card, PdfGenerationOptions options, XUnit x, XUnit y, XUnit width,
            XUnit height)
        {
            _logger.LogDebug($"DrawCard: {card.Name} - EnableBleed: {card.EnableBleed}");

            try
            {
                if (card.ImageData != null && card.ImageData.Length > 0)
                {
                    // Pass the bleed flag to image processing
                    var processedImage =
                        ProcessImageForHighDpiPdf(card.ImageData, card.Name, options.PrintDpi, card.EnableBleed);
                    if (processedImage != null)
                    {
                        XImage? xImage = null;
                        try
                        {
                            // FIX: Create memory stream that stays alive during XImage creation
                            using var ms = new MemoryStream(processedImage);

                            // Try to create XImage - if it fails, convert to PNG first
                            try
                            {
                                xImage = XImage.FromStream(ms);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(
                                    $"Failed to create XImage directly: {ex.Message}, converting to PNG...");

                                // Convert to PNG as fallback - PDFsharp handles PNG better
                                var pngData = ConvertToPng(processedImage, card.Name);
                                if (pngData != null)
                                {
                                    using var pngMs = new MemoryStream(pngData);
                                    xImage = XImage.FromStream(pngMs);
                                }
                            }

                            if (xImage != null)
                            {
                                // Draw at full card size - the image is already processed for bleed
                                gfx.DrawImage(xImage, new XRect(x.Point, y.Point, width.Point, height.Point));
                                _logger.LogDebug(
                                    $"SUCCESS: Drew {(card.EnableBleed ? "bleed-cropped" : "full")} image for {card.Name}");
                            }
                            else
                            {
                                _logger.LogError($"ERROR: Failed to create XImage for {card.Name}");
                                DrawPlaceholder(gfx, card, x, y, width, height, "Image Load Error");
                            }
                        }
                        finally
                        {
                            xImage?.Dispose();
                        }
                    }
                }
                else
                {
                    DrawPlaceholder(gfx, card, x, y, width, height, card.EnableBleed ? "No Image (Bleed)" : "No Image");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: Exception in DrawCard for {card.Name}: {ex.Message}");
                DrawPlaceholder(gfx, card, x, y, width, height, "Error");
            }
        }

        // Update ProcessImageForHighDpiPdf method in PdfGenerationService.cs to handle bleed
        private byte[]? ProcessImageForHighDpiPdf(byte[] imageData, string cardName, int targetDpi,
            bool enableBleed = false)
        {
            try
            {
                _logger.LogDebug(
                    $"ProcessImageForHighDpiPdf: Processing {cardName} for {targetDpi} DPI - EnableBleed: {enableBleed}");

                using var image = Image.Load<Rgba32>(imageData);
                var originalSize = $"{image.Width}x{image.Height}";

                _logger.LogDebug($"{cardName}:  Source: {originalSize}");

                // Calculate target dimensions for exact 2.48" × 3.46" at target DPI
                var targetWidth = (int)(CARD_WIDTH_INCHES * targetDpi);
                var targetHeight = (int)(CARD_HEIGHT_INCHES * targetDpi);

                // If bleed is enabled, crop 3mm from all sides of source image
                if (enableBleed)
                {
                    // Calculate 3mm in pixels based on the current image resolution
                    // Assume source image represents a 63mm × 88mm card with bleed
                    var sourcePixelsPerMm = Math.Min(image.Width / CARD_WIDTH_MM, image.Height / CARD_HEIGHT_MM);
                    var cropPixelsFloat = (3.0 * sourcePixelsPerMm); // 3mm in pixels
                    var cropPixels = (int) Math.Ceiling( cropPixelsFloat); // Round up to ensure we remove enough bleed
                    

                    _logger.LogDebug($"{cardName}:  Bleed crop: {cropPixels} pixels from each edge");

                    // Crop the image (remove 3mm bleed from all sides)
                    var cropRect = new Rectangle(
                        cropPixels,
                        cropPixels,
                        image.Width - cropPixels * 2,
                        image.Height - cropPixels * 2
                    );

                    image.Mutate(x => x.Crop(cropRect));
                    _logger.LogDebug($"{cardName}:  After crop: {image.Width}x{image.Height}");
                }

                _logger.LogDebug(
                    $"  Target: {targetWidth}x{targetHeight} pixels for {CARD_WIDTH_MM}mm × {CARD_HEIGHT_MM}mm at {targetDpi} DPI");

                // Resize to exact target dimensions (this stretches the cropped image to full card size)
                if (image.Width != targetWidth || image.Height != targetHeight)
                {
                    var scaleX = (double)targetWidth / image.Width;
                    var scaleY = (double)targetHeight / image.Height;
                    _logger.LogDebug($"{cardName}:  Scaling: {scaleX:F3}x horizontally, {scaleY:F3}x vertically");

                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(targetWidth, targetHeight),
                        Mode = ResizeMode.Stretch, // Stretch cropped image to fill full card
                        Sampler = KnownResamplers.Lanczos3
                    }));
                }

                // Set DPI metadata
                image.Metadata.HorizontalResolution = targetDpi;
                image.Metadata.VerticalResolution = targetDpi;

                // Choose output format
                var outputStream = new MemoryStream();
                string format;

                if (targetDpi >= 600)
                {
                    var pngEncoder = new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.BestCompression,
                        ColorType = PngColorType.RgbWithAlpha
                    };
                    image.Save(outputStream, pngEncoder);
                    format = "PNG";
                }
                else
                {
                    var jpegEncoder = new JpegEncoder
                    {
                        Quality = 95
                    };
                    image.Save(outputStream, jpegEncoder);
                    format = "JPEG";
                }

                var processedData = outputStream.ToArray();
                outputStream.Dispose();

                _logger.LogDebug($"{cardName}:  Output: {processedData.Length} bytes as {format}");
                _logger.LogDebug(
                    $"SUCCESS: ProcessImageForHighDpiPdf completed for {cardName} - Bleed {(enableBleed ? "removed" : "preserved")}");

                return processedData;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: ProcessImageForHighDpiPdf failed for {cardName}: {ex.Message}");
                return null;
            }
        }

        // private Bitmap CreateSimplePreview(CardCollection cards, PdfGenerationOptions options, int previewDpi)
        // {
        //     _logger.BeginScope("CreateSimplePreview");
        //     try
        //     {
        //         
        //         _logger.LogDebug($"CreateSimplePreview called with {cards?.Count ?? 0} cards at {previewDpi} DPI for preview (print will be {options.PrintDpi} DPI)");
        //
        //         // Use the user's orientation choice, not automatic layout
        //         var actualCardsPerRow = options.IsPortrait ? 3 : 4;
        //         var actualCardsPerColumn = options.IsPortrait ? 3 : 2;
        //         var cardsPerPage = actualCardsPerRow * actualCardsPerColumn;
        //         var pageCards = cards?.Take(cardsPerPage).ToList() ?? new List<Card>();
        //
        //         
        //         _logger.LogDebug($"Page cards: {pageCards.Count} in {actualCardsPerRow}x{actualCardsPerColumn} grid with {options.CardSpacing}pt spacing");
        //
        //         // Get page dimensions based on selected page size and USER'S orientation choice
        //         var pageDimensions =
        //             GetPageDimensions(options.PageSize, !options.IsPortrait); // !IsPortrait = IsLandscape
        //
        //         // Calculate preview scale to fit in a reasonable screen size
        //         var maxPreviewWidth = 1200;
        //         var maxPreviewHeight = 900;
        //         var scaleX = maxPreviewWidth / pageDimensions.Width;
        //         var scaleY = maxPreviewHeight / pageDimensions.Height;
        //         var pageScale = Math.Min(scaleX, scaleY);
        //
        //         // Calculate preview dimensions maintaining page aspect ratio
        //         var previewWidth = (int)(pageDimensions.Width * pageScale);
        //         var previewHeight = (int)(pageDimensions.Height * pageScale);
        //
        //         
        //         _logger.LogDebug($"Preview dimensions: {previewWidth}x{previewHeight} (PDF: {pageDimensions.Width:F0}x{pageDimensions.Height:F0})");
        //         // Ensure we have a valid page scale
        //         _logger.LogDebug($"Page: {options.PageSize} {(options.IsPortrait ? "Portrait" : "Landscape")}, Scale: {pageScale:F3}");
        //
        //         using var bitmap = new System.Drawing.Bitmap(previewWidth, previewHeight);
        //         using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        //
        //         graphics.Clear(System.Drawing.Color.White);
        //         graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        //
        //         // // Draw title
        //         // using var titleFont = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
        //         // var titleText =
        //         //     $"Card Collection - {cards?.Count ?? 0} cards ({actualCardsPerRow}x{actualCardsPerColumn}) {(options.IsPortrait ? "Portrait" : "Landscape")} {options.PageSize}";
        //         // graphics.DrawString(titleText, titleFont, System.Drawing.Brushes.Black,
        //         //     new System.Drawing.PointF(10, 10));
        //         //
        //         //
        //         // _logger.LogDebug($"Drew title: {titleText}");
        //
        //         if (pageCards.Count == 0)
        //         {
        //             
        //             _logger.LogDebug("No cards to draw, returning empty preview");
        //             using var ms = new MemoryStream();
        //             bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        //             ms.Position = 0;
        //             return new Bitmap(ms);
        //         }
        //
        //         // FIXED CARD DIMENSIONS: Always in mm, but scaled for preview
        //         var cardWidthPreview = (float)(CARD_WIDTH_POINTS * pageScale);
        //         var cardHeightPreview = (float)(CARD_HEIGHT_POINTS * pageScale);
        //         var spacingPreview = (float)(options.CardSpacing * pageScale);
        //
        //         
        //         _logger.LogDebug($"FIXED Preview card layout: {cardWidthPreview:F1}x{cardHeightPreview:F1} (EXACTLY 2.5\" x 3.5\" scaled by {pageScale:F3}) - spacing: {spacingPreview:F1}");
        //
        //         // Calculate total grid size
        //         var totalGridWidth = actualCardsPerRow * cardWidthPreview + (actualCardsPerRow - 1) * spacingPreview;
        //         var totalGridHeight = actualCardsPerColumn * cardHeightPreview +
        //                               (actualCardsPerColumn - 1) * spacingPreview;
        //
        //         // Calculate available space for grid (scaled margins)
        //         var marginLeftScaled = options.LeftMargin * pageScale;
        //         var marginRightScaled = options.RightMargin * pageScale;
        //         var marginTopScaled = options.TopMargin * pageScale;
        //         var marginBottomScaled = options.BottomMargin * pageScale;
        //         //var titleSpaceScaled = 30 * pageScale; // Space for title
        //
        //         var availableWidth = previewWidth - marginLeftScaled - marginRightScaled;
        //         var availableHeight = previewHeight - marginTopScaled - marginBottomScaled ;
        //
        //         // Center the grid on the page
        //         var gridStartX = marginLeftScaled + (availableWidth - totalGridWidth) / 2;
        //         var gridStartY = marginTopScaled +  (availableHeight - totalGridHeight) / 2;
        //
        //         
        //         _logger.LogDebug($"  Grid start: {gridStartX:F1}, {gridStartY:F1}");
        //
        //         // Check if grid fits
        //         if (totalGridWidth > availableWidth || totalGridHeight > availableHeight)
        //         {
        //             
        //             _logger.LogWarning($"WARNING: Preview grid ({totalGridWidth:F1} x {totalGridHeight:F1}) is larger than available space ({availableWidth:F1} x {availableHeight:F1})");
        //         }
        //         else
        //         {
        //             
        //             _logger.LogDebug("SUCCESS: Preview grid fits within page margins");
        //         }
        //
        //         // Draw cards with FIXED spacing
        //         for (int row = 0; row < actualCardsPerColumn; row++)
        //         {
        //             for (int col = 0; col < actualCardsPerRow; col++)
        //             {
        //                 var cardIndex = row * actualCardsPerRow + col;
        //
        //                 if (cardIndex < pageCards.Count)
        //                 {
        //                     var card = pageCards[cardIndex];
        //
        //                     // Calculate exact position with spacing
        //                     var exactX = gridStartX + col * (cardWidthPreview + spacingPreview);
        //                     var exactY = gridStartY + row * (cardHeightPreview + spacingPreview);
        //
        //                     // Round for display
        //                     var x = (int)Math.Round(exactX);
        //                     var y = (int)Math.Round(exactY);
        //
        //                     
        //                     _logger.LogDebug($"Preview card {cardIndex}: {card?.Name ?? "NULL"}");
        //                     
        //                     _logger.LogDebug($"  Position: ({x}, {y})");
        //
        //                     DrawPreviewCard(graphics, card, options, x, y, (int)Math.Round(cardWidthPreview),
        //                         (int)Math.Round(cardHeightPreview));
        //                 }
        //             }
        //         }
        //
        //         // Draw cutting lines for the entire grid (outside card areas)
        //         if (options.ShowCuttingLines)
        //         {
        //             DrawPreviewGridCuttingLines(graphics, options, (float)gridStartX, (float)gridStartY,
        //                 actualCardsPerRow, actualCardsPerColumn, cardWidthPreview, cardHeightPreview, spacingPreview);
        //         }
        //
        //         
        //         _logger.LogDebug($"Finished drawing cards, converting to Avalonia Bitmap");
        //
        //         // Convert to Avalonia Bitmap
        //         using var outputStream = new MemoryStream();
        //         bitmap.Save(outputStream, System.Drawing.Imaging.ImageFormat.Png);
        //         outputStream.Position = 0;
        //
        //         var avaloniaB = new Bitmap(outputStream);
        //         
        //         _logger.LogDebug($"Successfully created Avalonia Bitmap");
        //
        //         return avaloniaB;
        //     }
        //     catch (Exception ex)
        //     {
        //         
        //         _logger.LogError(ex,$"Error creating simple preview.");
        //         return CreateFallbackPreview(cards, options);
        //     }
        // }

        // Update preview drawing to handle bleed

        private Bitmap CreateSimplePreview(CardCollection cards, PdfGenerationOptions options, int previewDpi)
        {
            _logger.BeginScope("CreateSimplePreview");
            try
            {
                _logger.LogDebug(
                    $"CreateSimplePreview called with {cards?.Count ?? 0} cards at {previewDpi} DPI for preview (print will be {options.PrintDpi} DPI)");

                // ✅ NEW: Log DPI impact analysis
                LogPreviewDpiImpact(options, cards);

                // Use the user's orientation choice, not automatic layout
                var actualCardsPerRow = options.IsPortrait ? 3 : 4;
                var actualCardsPerColumn = options.IsPortrait ? 3 : 2;
                var cardsPerPage = actualCardsPerRow * actualCardsPerColumn;
                var pageCards = cards?.Take(cardsPerPage).ToList() ?? new List<Card>();

                _logger.LogDebug(
                    $"Page cards: {pageCards.Count} in {actualCardsPerRow}x{actualCardsPerColumn} grid with {options.CardSpacing}pt spacing");

                // Get page dimensions based on selected page size and USER'S orientation choice
                var pageDimensions =
                    GetPageDimensions(options.PageSize, !options.IsPortrait); // !IsPortrait = IsLandscape

                // ✅ FIXED SCALING LOGIC: Maintain exact aspect ratio
                var targetPreviewSize = CalculateOptimalPreviewSize(pageDimensions.Width, pageDimensions.Height);
                var previewWidth = targetPreviewSize.Width;
                var previewHeight = targetPreviewSize.Height;
                var pageScale = targetPreviewSize.Scale;

                _logger.LogDebug($"✅ FIXED SCALING:");
                _logger.LogDebug($"  Page dimensions: {pageDimensions.Width:F0}×{pageDimensions.Height:F0} pt");
                _logger.LogDebug($"  Preview dimensions: {previewWidth}×{previewHeight} px");
                _logger.LogDebug($"  Scale factor: {pageScale:F3}");
                _logger.LogDebug(
                    $"  Aspect ratio preserved: {Math.Abs((double)previewWidth / previewHeight - pageDimensions.Width / pageDimensions.Height) < 0.001}");

                using var bitmap = new System.Drawing.Bitmap(previewWidth, previewHeight);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);

                graphics.Clear(System.Drawing.Color.White);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                if (pageCards.Count == 0)
                {
                    _logger.LogDebug("No cards to draw, returning empty preview");
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    return new Bitmap(ms);
                }

                // FIXED CARD DIMENSIONS: Always 63mm x 88mm scaled for preview
                var cardWidthPreview = (float)(CARD_WIDTH_POINTS * pageScale);
                var cardHeightPreview = (float)(CARD_HEIGHT_POINTS * pageScale);
                var spacingPreview = (float)(options.CardSpacing * pageScale);

                _logger.LogDebug(
                    $"FIXED Preview card layout: {cardWidthPreview:F1}×{cardHeightPreview:F1} (EXACTLY 63mm x 88mm scaled by {pageScale:F3}) - spacing: {spacingPreview:F1}");

                // Calculate total grid size
                var totalGridWidth = actualCardsPerRow * cardWidthPreview + (actualCardsPerRow - 1) * spacingPreview;
                var totalGridHeight = actualCardsPerColumn * cardHeightPreview +
                                      (actualCardsPerColumn - 1) * spacingPreview;

                // Use ONLY print margins, no title space
                var marginLeftScaled = options.LeftMargin * pageScale;
                var marginRightScaled = options.RightMargin * pageScale;
                var marginTopScaled = options.TopMargin * pageScale;
                var marginBottomScaled = options.BottomMargin * pageScale;

                var availableWidth = previewWidth - marginLeftScaled - marginRightScaled;
                var availableHeight = previewHeight - marginTopScaled - marginBottomScaled;

                // Center the grid on the page using ONLY the configured margins
                var gridStartX = marginLeftScaled + (availableWidth - totalGridWidth) / 2;
                var gridStartY = marginTopScaled + (availableHeight - totalGridHeight) / 2;

                _logger.LogDebug($"Grid positioning:");
                _logger.LogDebug($"  Available space: {availableWidth:F1}×{availableHeight:F1}");
                _logger.LogDebug($"  Grid size: {totalGridWidth:F1}×{totalGridHeight:F1}");
                _logger.LogDebug($"  Grid start: ({gridStartX:F1}, {gridStartY:F1})");

                // Check if grid fits (this should always pass now with proper scaling)
                if (totalGridWidth > availableWidth || totalGridHeight > availableHeight)
                {
                    _logger.LogWarning(
                        $"WARNING: Preview grid ({totalGridWidth:F1}×{totalGridHeight:F1}) is larger than available space ({availableWidth:F1}×{availableHeight:F1})");
                }
                else
                {
                    _logger.LogDebug("✅ SUCCESS: Preview grid fits perfectly within page margins");
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

                            _logger.LogDebug(
                                $"Preview card {cardIndex}: {card?.Name ?? "NULL"} at ({x}, {y}) using {options.PreviewDpi} DPI");

                            DrawPreviewCard(graphics, card, options, x, y, (int)Math.Round(cardWidthPreview),
                                (int)Math.Round(cardHeightPreview));
                        }
                    }
                }

                // Draw cutting lines with proper boundaries
                if (options.ShowCuttingLines)
                {
                    DrawPreviewGridCuttingLines(graphics, options, (float)gridStartX, (float)gridStartY,
                        actualCardsPerRow, actualCardsPerColumn, cardWidthPreview, cardHeightPreview, spacingPreview);
                }

                _logger.LogDebug($"Finished drawing cards, converting to Avalonia Bitmap");

                // Convert to Avalonia Bitmap
                using var outputStream = new MemoryStream();
                bitmap.Save(outputStream, System.Drawing.Imaging.ImageFormat.Png);
                outputStream.Position = 0;

                var avaloniaB = new Bitmap(outputStream);
                _logger.LogDebug($"✅ Successfully created Avalonia Bitmap with proper aspect ratio");

                return avaloniaB;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating simple preview.");
                return CreateFallbackPreview(cards, options);
            }
        }

        /// <summary>
        /// Calculate preview file size estimate (for UI display)
        /// </summary>
        public string GetPreviewSizeEstimate(CardCollection cards, PdfGenerationOptions options)
        {
            if (cards == null || cards.Count == 0) return "0 KB";

            // Rough estimate based on preview DPI
            var cardCount = Math.Min(cards.Count, 9); // Max cards in preview
            var pixelsPerCard = CARD_WIDTH_INCHES * CARD_HEIGHT_INCHES * options.PreviewDpi * options.PreviewDpi;
            var bytesPerPixel = options.PreviewDpi >= 200 ? 4 : 3; // PNG vs JPEG
            var compressionRatio = options.PreviewDpi >= 200 ? 0.7 : options.PreviewQuality / 100.0;

            var estimatedBytes = cardCount * pixelsPerCard * bytesPerPixel * compressionRatio;

            if (estimatedBytes < 1024) return $"{estimatedBytes:F0} B";
            if (estimatedBytes < 1024 * 1024) return $"{estimatedBytes / 1024:F1} KB";
            return $"{estimatedBytes / (1024 * 1024):F1} MB";
        }

// ============================================================================
// Add logging to show the DPI difference impact:

        private void LogPreviewDpiImpact(PdfGenerationOptions options, CardCollection cards)
        {
            _logger.LogDebug($"=== PREVIEW DPI IMPACT ANALYSIS ===");
            _logger.LogDebug($"Preview DPI: {options.PreviewDpi} (for screen display)");
            _logger.LogDebug($"Print DPI: {options.PrintDpi} (for PDF generation)");

            var previewCardPixels = (int)(CARD_WIDTH_INCHES * options.PreviewDpi) *
                                    (int)(CARD_HEIGHT_INCHES * options.PreviewDpi);
            var printCardPixels =
                (int)(CARD_WIDTH_INCHES * options.PrintDpi) * (int)(CARD_HEIGHT_INCHES * options.PrintDpi);

            _logger.LogDebug(
                $"Preview card resolution: {(int)(CARD_WIDTH_INCHES * options.PreviewDpi)}×{(int)(CARD_HEIGHT_INCHES * options.PreviewDpi)} = {previewCardPixels:N0} pixels");
            _logger.LogDebug(
                $"Print card resolution: {(int)(CARD_WIDTH_INCHES * options.PrintDpi)}×{(int)(CARD_HEIGHT_INCHES * options.PrintDpi)} = {printCardPixels:N0} pixels");

            var pixelRatio = (double)printCardPixels / previewCardPixels;
            _logger.LogDebug($"Print has {pixelRatio:F1}x more pixels than preview");

            var previewSizeEstimate = GetPreviewSizeEstimate(cards, options);
            _logger.LogDebug($"Estimated preview memory usage: {previewSizeEstimate}");

            // Performance recommendations
            if (options.PreviewDpi > 200)
                _logger.LogDebug($"💡 Preview DPI ({options.PreviewDpi}) is high - may impact UI performance");
            else if (options.PreviewDpi < 100)
                _logger.LogDebug($"⚠️  Preview DPI ({options.PreviewDpi}) is low - preview may appear pixelated");
            else
                _logger.LogDebug($"✅ Preview DPI ({options.PreviewDpi}) is optimal for screen display");

            _logger.LogDebug($"=== END PREVIEW DPI ANALYSIS ===");
        }


        /// <summary>
        /// Calculates optimal preview dimensions that maintain exact aspect ratio
        /// while fitting within reasonable screen bounds
        /// </summary>
        private (int Width, int Height, double Scale) CalculateOptimalPreviewSize(double pageWidthPt,
            double pageHeightPt)
        {
            // Define reasonable preview size limits
            const int minPreviewDimension = 400; // Minimum for readability
            const int maxPreviewDimension = 1400; // Maximum for screen fit
            const int idealMinDimension = 800; // Preferred minimum

            _logger.LogDebug($"Calculating optimal preview size for page: {pageWidthPt:F0}×{pageHeightPt:F0} pt");

            // Calculate page aspect ratio
            var pageAspectRatio = pageWidthPt / pageHeightPt;
            _logger.LogDebug(
                $"Page aspect ratio: {pageAspectRatio:F3} ({(pageAspectRatio > 1 ? "landscape" : "portrait")})");

            int previewWidth, previewHeight;
            double scale;

            // Strategy: Size the longer dimension to idealMinDimension, 
            // then check if shorter dimension fits within maxPreviewDimension
            if (pageWidthPt >= pageHeightPt)
            {
                // Landscape or square - width is longer
                previewWidth = idealMinDimension;
                previewHeight = (int)Math.Round(idealMinDimension / pageAspectRatio);

                // Check if height exceeds maximum
                if (previewHeight > maxPreviewDimension)
                {
                    previewHeight = maxPreviewDimension;
                    previewWidth = (int)Math.Round(maxPreviewDimension * pageAspectRatio);
                }

                scale = (double)previewWidth / pageWidthPt;
            }
            else
            {
                // Portrait - height is longer
                previewHeight = idealMinDimension;
                previewWidth = (int)Math.Round(idealMinDimension * pageAspectRatio);

                // Check if width exceeds maximum
                if (previewWidth > maxPreviewDimension)
                {
                    previewWidth = maxPreviewDimension;
                    previewHeight = (int)Math.Round(maxPreviewDimension / pageAspectRatio);
                }

                scale = (double)previewHeight / pageHeightPt;
            }

            // Ensure minimum dimensions for usability
            if (previewWidth < minPreviewDimension)
            {
                var adjustmentFactor = (double)minPreviewDimension / previewWidth;
                previewWidth = minPreviewDimension;
                previewHeight = (int)Math.Round(previewHeight * adjustmentFactor);
                scale *= adjustmentFactor;
            }

            if (previewHeight < minPreviewDimension)
            {
                var adjustmentFactor = (double)minPreviewDimension / previewHeight;
                previewHeight = minPreviewDimension;
                previewWidth = (int)Math.Round(previewWidth * adjustmentFactor);
                scale *= adjustmentFactor;
            }

            // Verify aspect ratio is maintained (within rounding tolerance)
            var previewAspectRatio = (double)previewWidth / previewHeight;
            var aspectRatioError = Math.Abs(previewAspectRatio - pageAspectRatio);

            _logger.LogDebug($"Preview calculation results:");
            _logger.LogDebug($"  Preview size: {previewWidth}×{previewHeight} px");
            _logger.LogDebug($"  Scale factor: {scale:F4}");
            _logger.LogDebug($"  Preview aspect ratio: {previewAspectRatio:F3}");
            _logger.LogDebug($"  Aspect ratio error: {aspectRatioError:F6} (should be < 0.001)");

            if (aspectRatioError > 0.001)
                _logger.LogWarning($"⚠️  Aspect ratio error {aspectRatioError:F6} exceeds tolerance!");
            else
                _logger.LogDebug($"✅ Aspect ratio perfectly preserved");

            return (previewWidth, previewHeight, scale);
        }

        /// <summary>
        /// Alternative method: Fixed maximum dimensions with perfect aspect ratio preservation
        /// Use this if you prefer consistent maximum sizes
        /// </summary>
        private (int Width, int Height, double Scale) CalculateOptimalPreviewSizeFixed(double pageWidthPt,
            double pageHeightPt)
        {
            // Fixed maximum dimensions
            const int maxWidth = 1200;
            const int maxHeight = 900;

            // Calculate scales for each dimension
            var scaleX = (double)maxWidth / pageWidthPt;
            var scaleY = (double)maxHeight / pageHeightPt;

            // Use the smaller scale to ensure both dimensions fit
            var scale = Math.Min(scaleX, scaleY);

            // Calculate actual preview dimensions (will be smaller than max in one dimension)
            var previewWidth = (int)Math.Round(pageWidthPt * scale);
            var previewHeight = (int)Math.Round(pageHeightPt * scale);

            _logger.LogDebug($"Fixed scaling calculation:");
            _logger.LogDebug($"  Page: {pageWidthPt:F0}×{pageHeightPt:F0} pt");
            _logger.LogDebug($"  Max bounds: {maxWidth}×{maxHeight} px");
            _logger.LogDebug($"  Scale factors: X={scaleX:F3}, Y={scaleY:F3}, chosen={scale:F3}");
            _logger.LogDebug($"  Result: {previewWidth}×{previewHeight} px");
            _logger.LogDebug(
                $"  Utilization: {(double)previewWidth / maxWidth * 100:F1}% width, {(double)previewHeight / maxHeight * 100:F1}% height");

            return (previewWidth, previewHeight, scale);
        }

        private void DrawPreviewCard(System.Drawing.Graphics graphics, Card card, PdfGenerationOptions options,
            int x, int y, int width, int height)
        {
            try
            {
                var rect = new System.Drawing.Rectangle(x, y, width, height);

                _logger.LogDebug(
                    $"Drawing preview card {card?.Name ?? "NULL"} - EnableBleed: {card?.EnableBleed} at {options.PreviewDpi} DPI");

                if (card?.ImageData != null && card.ImageData.Length > 0)
                    try
                    {
                        // ✅ NEW: Process image at preview DPI instead of using raw image data
                        var processedImageData = ProcessImageForPreviewDpi(card.ImageData, card.Name,
                            options.PreviewDpi, card.EnableBleed, width, height);

                        using var imageStream = new MemoryStream(processedImageData);
                        using var processedImage = System.Drawing.Image.FromStream(imageStream);

                        if (card.EnableBleed)
                        {
                            // For bleed cards, the processed image is already cropped
                            graphics.DrawImage(processedImage, rect);
                            _logger.LogDebug($"Drew preview with bleed (pre-processed): {card.Name}");
                        }
                        else
                        {
                            // For non-bleed cards, draw the processed image directly
                            graphics.DrawImage(processedImage, rect);
                            _logger.LogDebug($"Drew preview without bleed (pre-processed): {card.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error drawing preview image for {card.Name}");
                        DrawPreviewPlaceholder(graphics, card, rect, "Image Error");
                    }
                else
                    DrawPreviewPlaceholder(graphics, card, rect,
                        card?.EnableBleed == true ? "No Image (Bleed)" : "No Image");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error drawing preview card {card?.Name ?? "NULL"}");
            }
        }

        /// <summary>
        /// Process image specifically for preview display at the specified DPI and screen dimensions
        /// This is separate from PDF processing and optimized for preview performance
        /// </summary>
        private byte[] ProcessImageForPreviewDpi(byte[] imageData, string cardName, int previewDpi,
            bool enableBleed, int targetWidthPx, int targetHeightPx)
        {
            try
            {
                _logger.LogDebug(
                    $"Processing preview image: {cardName} at {previewDpi} DPI for {targetWidthPx}×{targetHeightPx}px display");

                using var image = Image.Load<Rgba32>(imageData);
                var originalSize = $"{image.Width}×{image.Height}";

                _logger.LogDebug($"  Source: {originalSize}");

                // Apply bleed cropping if enabled
                if (enableBleed)
                {
                    // Calculate 3mm crop in pixels based on current image resolution
                    // Assume source image represents a 63mm × 88mm card with bleed
                    var sourcePixelsPerMm = Math.Min(image.Width / CARD_WIDTH_MM, image.Height / CARD_HEIGHT_MM);
                    var cropPixels = (int)Math.Ceiling((3.0 * sourcePixelsPerMm)); // 3mm in pixels

                    _logger.LogDebug($"  Bleed crop: {cropPixels} pixels from each edge");

                    // Crop the image (remove 3mm bleed from all sides)
                    var cropRect = new Rectangle(
                        cropPixels,
                        cropPixels,
                        image.Width - cropPixels * 2,
                        image.Height - cropPixels * 2
                    );

                    image.Mutate(x => x.Crop(cropRect));
                    _logger.LogDebug($"  After crop: {image.Width}×{image.Height}");
                }

                // ✅ KEY CHANGE: Resize based on preview DPI and target display size
                // The target size should match the card dimensions at preview DPI
                var previewCardWidthPx = (int)(CARD_WIDTH_INCHES * previewDpi);
                var previewCardHeightPx = (int)(CARD_HEIGHT_INCHES * previewDpi);

                _logger.LogDebug(
                    $"  Preview DPI sizing: {previewCardWidthPx}×{previewCardHeightPx}px at {previewDpi} DPI");
                _logger.LogDebug($"  Display target: {targetWidthPx}×{targetHeightPx}px");

                // For preview, we want good quality but not excessive - resize to preview DPI
                if (image.Width != previewCardWidthPx || image.Height != previewCardHeightPx)
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(previewCardWidthPx, previewCardHeightPx),
                        Mode = ResizeMode.Stretch,
                        Sampler = KnownResamplers.Lanczos3 // High quality for preview
                    }));

                // Set DPI metadata for the preview
                image.Metadata.HorizontalResolution = previewDpi;
                image.Metadata.VerticalResolution = previewDpi;

                // Choose output format based on preview DPI
                var outputStream = new MemoryStream();
                string format;

                if (previewDpi >= 200)
                {
                    // Higher DPI previews: use PNG for quality
                    var pngEncoder = new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.DefaultCompression,
                        ColorType = PngColorType.RgbWithAlpha
                    };
                    image.Save(outputStream, pngEncoder);
                    format = "PNG";
                }
                else
                {
                    // Lower DPI previews: use JPEG for smaller file size
                    var jpegEncoder = new JpegEncoder
                    {
                        Quality = 50 // Use the preview quality setting
                    };
                    image.Save(outputStream, jpegEncoder);
                    format = "JPEG";
                }

                var processedData = outputStream.ToArray();
                outputStream.Dispose();

                _logger.LogDebug($"  Preview output: {processedData.Length} bytes as {format} at {previewDpi} DPI");
                _logger.LogDebug($"SUCCESS: Preview processing completed for {cardName}");

                return processedData;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: Preview processing failed for {cardName}: {ex.Message}");
                // Return original data as fallback
                return imageData;
            }
        }

        private void DrawPreviewPlaceholder(System.Drawing.Graphics graphics, Card card, System.Drawing.Rectangle rect,
            string message)
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

                var nameRect =
                    new System.Drawing.Rectangle(rect.X, rect.Y + rect.Height * 2 / 3, rect.Width, rect.Height / 3);
                graphics.DrawString(card?.Name ?? "Unknown", smallFont, System.Drawing.Brushes.DarkGray, nameRect,
                    stringFormat);

                _logger.LogDebug($"Drew placeholder for {card?.Name ?? "NULL"}: {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error drawing placeholder: {ex.Message}");
                try
                {
                    graphics.FillRectangle(System.Drawing.Brushes.Pink, rect);
                }
                catch
                {
                    // Give up
                    //todo add a throw here
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

                if (options.IsCuttingLineDashed) pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

                var extension = options.CuttingLineExtension;

                _logger.LogDebug($"Drawing grid cutting lines with {extension}pt extension OUTSIDE grid only");

                // Calculate grid boundaries
                var gridLeft = gridStartX;
                var gridRight = gridStartX + cardsPerRow * cardWidth + (cardsPerRow - 1) * spacing;
                var gridTop = gridStartY;
                var gridBottom = gridStartY + cardsPerColumn * cardHeight + (cardsPerColumn - 1) * spacing;

                // Draw horizontal cutting line extensions (only outside grid)
                for (var row = 0; row <= cardsPerColumn; row++)
                {
                    var y = gridStartY + row * (cardHeight + spacing);
                    if (row > 0) y -= spacing; // Adjust for spacing except at top

                    // Left extension
                    graphics.DrawLine(pen, gridLeft - extension, y, gridLeft, y);

                    // Right extension  
                    graphics.DrawLine(pen, gridRight, y, gridRight + extension, y);
                }

                // Draw vertical cutting line extensions (only outside grid)
                for (var col = 0; col <= cardsPerRow; col++)
                {
                    var x = gridStartX + col * (cardWidth + spacing);
                    if (col > 0) x -= spacing; // Adjust for spacing except at left

                    // Top extension
                    graphics.DrawLine(pen, x, gridTop - extension, x, gridTop);

                    // Bottom extension
                    graphics.DrawLine(pen, x, gridBottom, x, gridBottom + extension);
                }

                _logger.LogDebug("Completed drawing grid cutting line extensions");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error drawing grid cutting lines: {ex.Message}");
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
                        for (var i = 0; i < pixelCount; i++) ptr[i] = 0xFFFFFFFF; // White
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
                _logger.LogError($"Error creating font {familyName}: {ex.Message}, falling back to default");

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

        private void DrawPlaceholder(XGraphics gfx, Card card, XUnit x, XUnit y, XUnit width, XUnit height,
            string message)
        {
            // Draw border
            var pen = new XPen(XColors.Gray, 1);
            gfx.DrawRectangle(pen, XBrushes.LightGray, new XRect(x.Point, y.Point, width.Point, height.Point));

            // Draw text with safe font handling
            try
            {
                var font = GetSafeFont("Arial", 10, XFontStyleEx.Bold);
                var textRect = new XRect(x.Point, y.Point, width.Point, height.Point);

                gfx.DrawString(message, font, XBrushes.Black, textRect, XStringFormats.Center);

                var smallFont = GetSafeFont("Arial", 8, XFontStyleEx.Regular);
                var nameRect = new XRect(x, y + height * 0.6, width, height * 0.4);
                gfx.DrawString(card.Name ?? "Unknown", smallFont, XBrushes.DarkGray, nameRect, XStringFormats.Center);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error drawing placeholder text: {ex.Message}");
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

                if (options.IsCuttingLineDashed) pen.DashStyle = XDashStyle.Dash;

                var extension = options.CuttingLineExtension;

                _logger.LogDebug(
                    $"Drawing PDF grid cutting lines with {extension}pt extension OUTSIDE grid only");

                // Calculate grid boundaries
                var gridLeft = gridStartX;
                var gridRight = gridStartX + cardsPerRow * cardWidth + (cardsPerRow - 1) * options.CardSpacing;
                var gridTop = gridStartY;
                var gridBottom = gridStartY + cardsPerColumn * cardHeight + (cardsPerColumn - 1) * options.CardSpacing;

                // Draw horizontal cutting line extensions (only outside grid)
                for (var row = 0; row <= cardsPerColumn; row++)
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
                for (var col = 0; col <= cardsPerRow; col++)
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

                _logger.LogDebug("Completed drawing PDF grid cutting line extensions");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error drawing PDF grid cutting lines: {ex.Message}");
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
                return null!;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error in GetFont: {ex.Message}");
                return null!; // Return null to use system fonts
            }
        }
    }
}