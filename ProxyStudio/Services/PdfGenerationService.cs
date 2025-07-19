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

namespace ProxyStudio.Services
{
    public interface IPdfGenerationService
    {
        Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options);
        Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options);
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
        public int PreviewDpi { get; set; } = 150;
        public int PreviewQuality { get; set; } = 85;
    }

    public class PdfGenerationService : IPdfGenerationService
    {
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

        public async Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    DebugHelper.WriteDebug($"Generating PDF for {cards.Count} cards using PDFsharp");
                    
                    // Create PDF document
                    var document = new PdfDocument();
                    
                    // Calculate how many cards fit per page
                    var cardsPerPage = options.CardsPerRow * options.CardsPerColumn;
                    var totalPages = (int)Math.Ceiling((double)cards.Count / cardsPerPage);
                    
                    DebugHelper.WriteDebug($"Creating {totalPages} pages for {cards.Count} cards ({cardsPerPage} cards per page)");
                    
                    // Create pages and draw cards
                    for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
                    {
                        var page = document.AddPage();
                        page.Size = PdfSharp.PageSize.A4;
                        
                        var gfx = XGraphics.FromPdfPage(page);
                        
                        // Get cards for this page
                        var startCardIndex = pageIndex * cardsPerPage;
                        var pageCards = cards.Skip(startCardIndex).Take(cardsPerPage).ToList();
                        
                        DebugHelper.WriteDebug($"Page {pageIndex + 1}: Drawing {pageCards.Count} cards (starting from card {startCardIndex})");
                        
                        DrawCardGrid(gfx, pageCards, options, page.Width, page.Height, pageIndex + 1, totalPages);
                        
                        gfx.Dispose();
                    }
                    
                    // Save to memory stream
                    using var stream = new MemoryStream();
                    document.Save(stream);
                    document.Close();
                    
                    var pdfBytes = stream.ToArray();
                    DebugHelper.WriteDebug($"PDF generated successfully, size: {pdfBytes.Length} bytes");
                    return pdfBytes;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteDebug($"Error generating PDF: {ex.Message}");
                    DebugHelper.WriteDebug($"Stack trace: {ex.StackTrace}");
                    throw;
                }
            });
        }

        public async Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    DebugHelper.WriteDebug($"Generating preview for {cards?.Count ?? 0} cards using PDFsharp");
                    DebugHelper.WriteDebug($"Options: ShowCuttingLines={options?.ShowCuttingLines ?? false}, CardsPerRow={options?.CardsPerRow ?? 0}");
                    
                    if (options == null)
                    {
                        DebugHelper.WriteDebug("ERROR: options is null!");
                        return CreateFallbackPreview(cards, options);
                    }
                    
                    // Create a simple preview bitmap manually
                    var previewBitmap = CreateSimplePreview(cards, options);
                    
                    DebugHelper.WriteDebug($"Preview generated successfully");
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

        private Bitmap CreateSimplePreview(CardCollection cards, PdfGenerationOptions options)
        {
            try
            {
                DebugHelper.WriteDebug($"CreateSimplePreview called with {cards?.Count ?? 0} cards");
                
                var previewWidth = 600;
                var previewHeight = 800;
                
                using var bitmap = new System.Drawing.Bitmap(previewWidth, previewHeight);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                
                DebugHelper.WriteDebug("Created bitmap and graphics");
                
                graphics.Clear(System.Drawing.Color.White);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                
                // Draw title
                using var titleFont = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
                graphics.DrawString($"Card Collection - {cards?.Count ?? 0} cards ({options.CardsPerRow}x{options.CardsPerColumn})", titleFont, 
                    System.Drawing.Brushes.Black, new System.Drawing.PointF(10, 10));
                
                DebugHelper.WriteDebug("Drew title");
                
                if (cards == null || cards.Count == 0)
                {
                    DebugHelper.WriteDebug("No cards to draw");
                    // Convert to Avalonia Bitmap
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    
                    return new Bitmap(ms);
                }
                
                // USE THE OPTIONS VALUES instead of hardcoded 3x3!
                var actualCardsPerRow = options.CardsPerRow;
                var actualCardsPerColumn = options.CardsPerColumn;
                var cardsPerPage = actualCardsPerRow * actualCardsPerColumn;
                var pageCards = cards.Take(cardsPerPage).ToList();
                
                DebugHelper.WriteDebug($"Page cards: {pageCards.Count} in {actualCardsPerRow}x{actualCardsPerColumn} grid with {options.CardSpacing}pt spacing");
                
                var startX = 20;
                var startY = 40;
                var availableWidth = previewWidth - 40;
                var availableHeight = previewHeight - 60;
                
                // Account for spacing between cards
                var totalSpacingWidth = options.CardSpacing * (actualCardsPerRow - 1);
                var totalSpacingHeight = options.CardSpacing * (actualCardsPerColumn - 1);
                
                // Calculate card dimensions WITH spacing
                var cardWidth = (availableWidth - totalSpacingWidth) / actualCardsPerRow;
                var cardHeight = (availableHeight - totalSpacingHeight) / actualCardsPerColumn;
                
                // Maintain aspect ratio
                var standardCardRatio = 2.5f / 3.5f;
                var calculatedRatio = (float)cardWidth / cardHeight;
                
                if (calculatedRatio > standardCardRatio)
                {
                    cardWidth = (int)(cardHeight * standardCardRatio);
                }
                else
                {
                    cardHeight = (int)(cardWidth / standardCardRatio);
                }
                
                DebugHelper.WriteDebug($"Card layout: {cardWidth}x{cardHeight} (spacing: {options.CardSpacing})");
                
                // Draw cards WITH PROPER SPACING
                for (int row = 0; row < actualCardsPerColumn; row++)
                {
                    for (int col = 0; col < actualCardsPerRow; col++)
                    {
                        var cardIndex = row * actualCardsPerRow + col;
                        
                        if (cardIndex < pageCards.Count)
                        {
                            var card = pageCards[cardIndex];
                            
                            // Calculate position WITH spacing
                            var x = (int)(startX + col * (cardWidth + options.CardSpacing));
                            var y = (int)(startY + row * (cardHeight + options.CardSpacing));
                            
                            DebugHelper.WriteDebug($"Drawing card {cardIndex}: {card?.Name ?? "NULL"} at ({x},{y}) - row {row}, col {col}");
                            
                            DrawPreviewCard(graphics, card, options, x, y, (int)cardWidth, (int)cardHeight);
                        }
                    }
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
                        // Try to draw the actual image
                        using var imageStream = new MemoryStream(card.ImageData);
                        using var cardImage = System.Drawing.Image.FromStream(imageStream);
                        
                        graphics.DrawImage(cardImage, rect);
                        DebugHelper.WriteDebug($"Drew preview image for {card.Name}");
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteDebug($"Error drawing card image for {card.Name}: {ex.Message}");
                        // Fallback to placeholder
                        DrawPreviewPlaceholder(graphics, card, rect, "Image Error");
                    }
                }
                else
                {
                    DebugHelper.WriteDebug($"No image data for {card?.Name ?? "NULL"}");
                    DrawPreviewPlaceholder(graphics, card, rect, "No Image");
                }
                
                // Draw cutting lines if enabled
                if (options.ShowCuttingLines)
                {
                    var color = ParseSystemDrawingColor(options.CuttingLineColor);
                    using var pen = new System.Drawing.Pen(color, options.CuttingLineThickness);
                    
                    if (options.IsCuttingLineDashed)
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    }
                    
                    graphics.DrawRectangle(pen, rect);
                    DebugHelper.WriteDebug($"Drew cutting lines for {card?.Name ?? "NULL"}");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error drawing preview card {card?.Name ?? "NULL"}: {ex.Message}");
                DebugHelper.WriteDebug($"Stack trace: {ex.StackTrace}");
                
                // Safe fallback
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
                // Ultimate fallback - just draw a rectangle
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

        private System.Drawing.Color ParseSystemDrawingColor(string colorString)
        {
            try
            {
                if (colorString.StartsWith("#") && colorString.Length == 7)
                {
                    var hex = colorString.Substring(1);
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return System.Drawing.Color.FromArgb(r, g, b);
                }
                return System.Drawing.Color.Red;
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

        private void DrawCardGrid(XGraphics gfx, List<Card> pageCards, PdfGenerationOptions options, XUnit pageWidth, XUnit pageHeight, int currentPage, int totalPages)
        {
            // USE THE OPTIONS VALUES instead of hardcoded 3x3!
            var actualCardsPerRow = options.CardsPerRow;
            var actualCardsPerColumn = options.CardsPerColumn;
            
            DebugHelper.WriteDebug($"Drawing {pageCards.Count} cards in {actualCardsPerRow}x{actualCardsPerColumn} grid with {options.CardSpacing}pt spacing (Page {currentPage} of {totalPages})");
            
            // Calculate available space
            var availableWidth = pageWidth - XUnit.FromPoint(options.LeftMargin + options.RightMargin);
            var availableHeight = pageHeight - XUnit.FromPoint(options.TopMargin + options.BottomMargin + 50); // Space for title
            
            // Account for spacing between cards
            var totalSpacingWidth = XUnit.FromPoint(options.CardSpacing * (actualCardsPerRow - 1));
            var totalSpacingHeight = XUnit.FromPoint(options.CardSpacing * (actualCardsPerColumn - 1));
            
            // Calculate card dimensions WITH spacing
            var cardWidth = (availableWidth - totalSpacingWidth) / actualCardsPerRow;
            var cardHeight = (availableHeight - totalSpacingHeight) / actualCardsPerColumn;
            
            // Maintain card aspect ratio
            var standardCardRatio = 2.5 / 3.5;
            var calculatedRatio = cardWidth / cardHeight;
            
            if (calculatedRatio > standardCardRatio)
            {
                cardWidth = cardHeight * standardCardRatio;
            }
            else
            {
                cardHeight = cardWidth / standardCardRatio;
            }
            
            DebugHelper.WriteDebug($"Card dimensions: {cardWidth:F1}x{cardHeight:F1} points (spacing: {options.CardSpacing}pt)");
            
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
                // Continue without title
            }
            
            // Draw cards WITH PROPER SPACING
            var startX = XUnit.FromPoint(options.LeftMargin);
            var startY = XUnit.FromPoint(options.TopMargin + 30);
            
            for (int row = 0; row < actualCardsPerColumn; row++)
            {
                for (int col = 0; col < actualCardsPerRow; col++)
                {
                    var cardIndex = row * actualCardsPerRow + col;
                    
                    if (cardIndex < pageCards.Count)
                    {
                        var card = pageCards[cardIndex];
                        
                        // Calculate position WITH spacing
                        var x = startX + col * (cardWidth + XUnit.FromPoint(options.CardSpacing));
                        var y = startY + row * (cardHeight + XUnit.FromPoint(options.CardSpacing));
                        
                        DebugHelper.WriteDebug($"Drawing card {cardIndex} at ({x:F1}, {y:F1}) - row {row}, col {col}");
                        
                        DrawCard(gfx, card, options, x, y, cardWidth, cardHeight);
                    }
                }
            }
        }

        private XFont GetSafeFont(string familyName, double size, XFontStyleEx style)
        {
            try
            {
                // Try the requested font first
                return new XFont(familyName, size, style);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error creating font {familyName}: {ex.Message}, falling back to default");
                
                try
                {
                    // Fall back to a more generic font
                    return new XFont("Segoe UI", size, style);
                }
                catch
                {
                    try
                    {
                        // Last resort - use any available font
                        return new XFont("Helvetica", size, style);
                    }
                    catch
                    {
                        // Absolute last resort
                        return new XFont("Times", size, style);
                    }
                }
            }
        }

        private void DrawCard(XGraphics gfx, Card card, PdfGenerationOptions options, XUnit x, XUnit y, XUnit width, XUnit height)
        {
            DebugHelper.WriteDebug($"Drawing card: {card.Name} at ({x:F1}, {y:F1})");
            
            try
            {
                if (card.ImageData != null && card.ImageData.Length > 0)
                {
                    // Process and draw the image
                    var processedImage = ProcessImageForPdf(card.ImageData, card.Name);
                    if (processedImage != null)
                    {
                        using var ms = new MemoryStream(processedImage);
                        var xImage = XImage.FromStream(ms);
                        
                        // Draw the image to fill the card
                        gfx.DrawImage(xImage, new XRect(x, y, width, height));
                        
                        xImage.Dispose();
                        DebugHelper.WriteDebug($"Successfully drew image for {card.Name}");
                    }
                    else
                    {
                        DrawPlaceholder(gfx, card, x, y, width, height, "Image Error");
                    }
                }
                else
                {
                    DrawPlaceholder(gfx, card, x, y, width, height, "No Image");
                }
                
                // Draw cutting lines if enabled
                if (options.ShowCuttingLines)
                {
                    DrawCuttingLines(gfx, options, x, y, width, height);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error drawing card {card.Name}: {ex.Message}");
                DrawPlaceholder(gfx, card, x, y, width, height, "Error");
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

        private void DrawCuttingLines(XGraphics gfx, PdfGenerationOptions options, XUnit x, XUnit y, XUnit width, XUnit height)
        {
            var color = ParseColor(options.CuttingLineColor);
            var pen = new XPen(color, options.CuttingLineThickness);
            
            if (options.IsCuttingLineDashed)
            {
                pen.DashStyle = XDashStyle.Dash;
            }
            
            // Draw border around the card
            gfx.DrawRectangle(pen, new XRect(x, y, width, height));
            
            DebugHelper.WriteDebug($"Drew cutting lines around card");
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

        private byte[]? ProcessImageForPdf(byte[] imageData, string cardName)
        {
            try
            {
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageData);
                DebugHelper.WriteDebug($"Loaded image for {cardName}: {image.Width}x{image.Height}");
                
                // Resize if too large
                if (image.Width > 800 || image.Height > 800)
                {
                    var maxSize = 600;
                    var ratio = Math.Min((double)maxSize / image.Width, (double)maxSize / image.Height);
                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);
                    
                    image.Mutate(x => x.Resize(newWidth, newHeight));
                    DebugHelper.WriteDebug($"Resized image for {cardName} to: {newWidth}x{newHeight}");
                }
                
                // Convert to PNG
                using var pngStream = new MemoryStream();
                image.SaveAsPng(pngStream);
                var pngData = pngStream.ToArray();
                
                DebugHelper.WriteDebug($"Converted image for {cardName} to PNG: {pngData.Length} bytes");
                return pngData;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error processing image for {cardName}: {ex.Message}");
                return null;
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