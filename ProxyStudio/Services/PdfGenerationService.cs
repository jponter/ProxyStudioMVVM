using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Size = QuestPDF.Infrastructure.Size;

namespace ProxyStudio.Services
{
    public interface IPdfGenerationService
    {
        Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options);
        Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options);
    }

    public class PdfGenerationOptions
    {
        public Size PageSize { get; set; } = new Size(595, 842);
        public bool IsPortrait { get; set; } = true;
        public int CardsPerRow { get; set; } = 3;
        public int CardsPerColumn { get; set; } = 3;
        public float CardSpacing { get; set; } = 10f;
        public bool ShowCuttingLines { get; set; } = true;
        public string CuttingLineColor { get; set; } = "#000000";
        public bool IsCuttingLineDashed { get; set; } = false;
        public float CuttingLineExtension { get; set; } = 10f;
        public float CuttingLineThickness { get; set; } = 0.5f;
        public float TopMargin { get; set; } = 20f;
        public float BottomMargin { get; set; } = 20f;
        public float LeftMargin { get; set; } = 20f;
        public float RightMargin { get; set; } = 20f;
        public int PreviewDpi { get; set; } = 150;
        public int PreviewQuality { get; set; } = 85;
    }

    public class PdfGenerationService : IPdfGenerationService
    {
        public async Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    DebugHelper.WriteDebug($"Generating PDF for {cards.Count} cards");

                    var document = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(595, 842); // A4 fixed size
                            page.MarginTop(20);
                            page.MarginBottom(20);
                            page.MarginLeft(20);
                            page.MarginRight(20);

                            page.Content().Column(column =>
                            {
                                column.Item().Text($"Card Collection - {cards.Count} cards").FontSize(16).Bold();
                                column.Item().PaddingVertical(10);

                                // Create a proper card grid layout
                                CreateCardGrid(column.Item(), cards, options);
                            });
                        });
                    });

                    var pdfBytes = document.GeneratePdf();
                    DebugHelper.WriteDebug($"PDF generated successfully, size: {pdfBytes.Length} bytes");
                    return pdfBytes;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteDebug($"Error generating PDF: {ex.Message}");
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
                    DebugHelper.WriteDebug($"Generating preview for {cards.Count} cards");

                    // Create a simple preview image programmatically instead of using QuestPDF image generation
                    return CreatePreviewBitmap(cards, options);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteDebug($"Error generating preview: {ex.Message}");
                    return null!;
                }
            });
        }

        private Bitmap CreatePreviewBitmap(CardCollection cards, PdfGenerationOptions options)
        {
            try
            {
                DebugHelper.WriteDebug("Creating preview bitmap");

                // Create a simple preview bitmap using WriteableBitmap
                var width = 600;
                var height = 800;

                var bitmap = new WriteableBitmap(new Avalonia.PixelSize(width, height), new Avalonia.Vector(96, 96));

                // Don't dispose the bitmap - we need to return it for the UI to use
                using (var context = bitmap.Lock())
                {
                    // Fill with white background
                    unsafe
                    {
                        var ptr = (uint*)context.Address;
                        var pixelCount = width * height;
                        for (int i = 0; i < pixelCount; i++)
                        {
                            ptr[i] = 0xFFFFFFFF; // White (ARGB format)
                        }

                        // Draw some simple preview content
                        DrawSimplePreview(ptr, width, height, cards, options);
                    }
                }

                DebugHelper.WriteDebug("Created simple preview bitmap successfully");
                return bitmap;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error creating preview bitmap: {ex.Message}");

                // Return null so the UI can handle the missing preview gracefully
                return null!;
            }
        }

        private unsafe void DrawSimplePreview(uint* ptr, int width, int height, CardCollection cards,
            PdfGenerationOptions options)
        {
            try
            {
                // Draw a simple grid pattern to show card layout
                var cardsPerRow = Math.Min(options.CardsPerRow, 3); // Limit to 3 for preview
                var cardWidth = width / cardsPerRow;
                var cardHeight = 200;
                var cardCount = Math.Min(cards.Count, cardsPerRow * 3); // Max 9 cards for preview

                for (int i = 0; i < cardCount; i++)
                {
                    var row = i / cardsPerRow;
                    var col = i % cardsPerRow;

                    var x = col * cardWidth + 10;
                    var y = row * cardHeight + 50;

                    // Draw a simple rectangle border for each card
                    DrawRectangle(ptr, width, height, x, y, cardWidth - 20, cardHeight - 20,
                        0xFF000000); // Black border

                    // Fill with light gray
                    FillRectangle(ptr, width, height, x + 2, y + 2, cardWidth - 24, cardHeight - 24,
                        0xFFF0F0F0); // Light gray
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error drawing preview: {ex.Message}");
            }
        }

        private unsafe void DrawRectangle(uint* ptr, int width, int height, int x, int y, int w, int h, uint color)
        {
            // Draw top and bottom borders
            for (int i = 0; i < w && x + i < width; i++)
            {
                if (y >= 0 && y < height) ptr[y * width + x + i] = color;
                if (y + h >= 0 && y + h < height) ptr[(y + h) * width + x + i] = color;
            }

            // Draw left and right borders
            for (int i = 0; i < h && y + i < height; i++)
            {
                if (x >= 0 && x < width) ptr[(y + i) * width + x] = color;
                if (x + w >= 0 && x + w < width) ptr[(y + i) * width + x + w] = color;
            }
        }

        private unsafe void FillRectangle(uint* ptr, int width, int height, int x, int y, int w, int h, uint color)
        {
            for (int row = 0; row < h && y + row < height; row++)
            {
                for (int col = 0; col < w && x + col < width; col++)
                {
                    if (x + col >= 0 && x + col < width && y + row >= 0 && y + row < height)
                    {
                        ptr[(y + row) * width + (x + col)] = color;
                    }
                }
            }
        }

        private void CreateCardGrid(IContainer container, CardCollection cards, PdfGenerationOptions options)
        {
            DebugHelper.WriteDebug($"CreateCardGrid called with {cards?.Count ?? 0} cards");
            
            if (cards == null || cards.Count == 0)
            {
                container.Text("No cards to display").FontSize(12);
                return;
            }

            // FORCE 3x3 grid regardless of options
            var actualCardsPerRow = 3;
            var actualCardsPerColumn = 3;
            DebugHelper.WriteDebug($"FORCING card grid to: {actualCardsPerRow} per row, {actualCardsPerColumn} per column (was {options.CardsPerRow}x{options.CardsPerColumn})");

            var cardsPerPage = actualCardsPerRow * actualCardsPerColumn;
            var pageCards = cards.Take(cardsPerPage).ToList();
            DebugHelper.WriteDebug($"Cards for this page: {pageCards.Count}");
            
            // Calculate available space and card dimensions
            var availableWidth = 555f; // A4 width minus margins
            var availableHeight = 750f; // A4 height minus margins and title space
            
            // Calculate card dimensions with NO spacing between cards
            var cardWidth = availableWidth / actualCardsPerRow;
            var cardHeight = availableHeight / actualCardsPerColumn;
            
            // Maintain card aspect ratio
            var standardCardRatio = 2.5f / 3.5f; // Standard card ratio (width/height)
            var calculatedRatio = cardWidth / cardHeight;
            
            if (calculatedRatio > standardCardRatio)
            {
                // Too wide, reduce width
                cardWidth = cardHeight * standardCardRatio;
            }
            else
            {
                // Too tall, reduce height
                cardHeight = cardWidth / standardCardRatio;
            }
            
            DebugHelper.WriteDebug($"Card dimensions: {cardWidth}x{cardHeight} (NO spacing between cards)");
            
            // Create the card grid with exactly 3 rows, NO spacing
            container.Column(column =>
            {
                for (int row = 0; row < actualCardsPerColumn; row++) // EXACTLY 3 rows
                {
                    DebugHelper.WriteDebug($"Creating row {row + 1} of {actualCardsPerColumn}");
                    
                    column.Item().Row(rowContainer =>
                    {
                        for (int col = 0; col < actualCardsPerRow; col++) // EXACTLY 3 columns
                        {
                            var cardIndex = row * actualCardsPerRow + col;
                            
                            if (cardIndex < pageCards.Count)
                            {
                                var card = pageCards[cardIndex];
                                DebugHelper.WriteDebug($"Placing card {cardIndex + 1} at row {row + 1}, col {col + 1}: {card.Name}");
                                
                                // Use ConstantItem with exact width to prevent gaps
                                rowContainer.ConstantItem(cardWidth)
                                    .Height(cardHeight)
                                    .Element(cellContainer => CreateCardCell(cellContainer, card, options));
                            }
                            else
                            {
                                DebugHelper.WriteDebug($"Empty slot at row {row + 1}, col {col + 1}");
                                // Empty cell to maintain grid structure
                                rowContainer.ConstantItem(cardWidth).Height(cardHeight);
                            }
                        }
                    });
                    
                    // NO spacing between rows - removed the spacing code
                }
            });
            
            DebugHelper.WriteDebug("Finished creating card grid");
        }

        private void CreateCardCell(IContainer container, Card card, PdfGenerationOptions options)
        {
            DebugHelper.WriteDebug($"Creating card cell for {card.Name}");
            
            if (card.ImageData != null && card.ImageData.Length > 0)
            {
                try
                {
                    DebugHelper.WriteDebug($"Attempting to render image for {card.Name}, size: {card.ImageData.Length} bytes");
                    
                    var processedImageData = ProcessImageForPdf(card.ImageData, card.Name);
                    
                    if (processedImageData != null && processedImageData.Length > 0)
                    {
                        DebugHelper.WriteDebug($"Processed image data for {card.Name}: {processedImageData.Length} bytes");
                        
                        // Create the card with image filling the entire space
                        if (options.ShowCuttingLines)
                        {
                            DebugHelper.WriteDebug($"Adding cutting lines for {card.Name}");
                            // Add cutting lines as border around the entire card
                            container.Border(2) // Make it thicker so it's visible
                                .BorderColor(Colors.Red.Medium)
                                .Image(processedImageData)
                                .FitArea();
                        }
                        else
                        {
                            // No cutting lines, just the image filling the space
                            container.Image(processedImageData).FitArea();
                        }
                        
                        DebugHelper.WriteDebug($"Successfully rendered image for {card.Name}");
                    }
                    else
                    {
                        DebugHelper.WriteDebug($"Failed to process image for {card.Name} - processed data is null or empty");
                        CreateImagePlaceholder(container, card, options, "Image Processing Failed");
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteDebug($"Failed to render image for {card.Name}: {ex.Message}");
                    CreateImagePlaceholder(container, card, options, "Image Error");
                }
            }
            else
            {
                DebugHelper.WriteDebug($"No image data for {card.Name}");
                CreateImagePlaceholder(container, card, options, "No Image");
            }
        }

        private byte[]? ProcessImageForPdf(byte[] imageData, string cardName)
        {
            try
            {
                // Use SixLabors.ImageSharp to load and convert the image
                using var image = SixLabors.ImageSharp.Image.Load(imageData);
                DebugHelper.WriteDebug($"Loaded image for {cardName}: {image.Width}x{image.Height}");

                // Resize if too large (QuestPDF might have issues with very large images)
                if (image.Width > 1000 || image.Height > 1000)
                {
                    var maxSize = 800;
                    var ratio = Math.Min((double)maxSize / image.Width, (double)maxSize / image.Height);
                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    image.Mutate(x => x.Resize(newWidth, newHeight));
                    DebugHelper.WriteDebug($"Resized image for {cardName} to: {newWidth}x{newHeight}");
                }

                // Convert to PNG format (QuestPDF handles PNG well)
                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                var pngData = ms.ToArray();

                DebugHelper.WriteDebug($"Converted image for {cardName} to PNG: {pngData.Length} bytes");
                return pngData;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error processing image for {cardName}: {ex.Message}");
                return null;
            }
        }

        private void CreateImagePlaceholder(IContainer container, Card card, PdfGenerationOptions options, string message)
        {
            var borderColor = options.ShowCuttingLines ? Colors.Red.Medium : Colors.Grey.Medium;
            var borderThickness = options.ShowCuttingLines ? 2f : 1f; // Make cutting lines thicker
            
            container.Border(borderThickness)
                .BorderColor(borderColor)
                .Background(Colors.Grey.Lighten4)
                .Padding(10)
                .Column(column =>
                {
                    column.Item().AlignCenter().Text(message).FontSize(12).Bold();
                    column.Item().PaddingVertical(5);
                    column.Item().AlignCenter().Text(card.Name ?? "Unknown").FontSize(10);
                    column.Item().AlignCenter().Text($"ID: {card.Id ?? "N/A"}").FontSize(8);
                    
                    if (card.ImageData != null)
                    {
                        column.Item().AlignCenter().Text($"{card.ImageData.Length} bytes").FontSize(6);
                    }
                });
        }

    }
}