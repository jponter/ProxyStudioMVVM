using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ProxyStudio.Services
{
    public class TestPdfService : IPdfGenerationService
    {
        public async Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    DebugHelper.WriteDebug("TestPdfService: Starting PDF generation");
                    
                    var document = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(595, 842); // A4
                            page.Margin(20);
                            
                            page.Content().Column(column =>
                            {
                                column.Item().Text("TEST PDF GENERATION").FontSize(20).Bold();
                                column.Item().PaddingVertical(10);
                                column.Item().Text($"Cards in collection: {cards.Count}").FontSize(12);
                                
                                for (int i = 0; i < Math.Min(cards.Count, 5); i++)
                                {
                                    var card = cards[i];
                                    column.Item().Text($"Card {i+1}: {card.Name}").FontSize(10);
                                    column.Item().Text($"  - ID: {card.Id}").FontSize(8);
                                    column.Item().Text($"  - Has Image: {card.ImageData != null && card.ImageData.Length > 0}").FontSize(8);
                                    if (card.ImageData != null)
                                    {
                                        column.Item().Text($"  - Image Size: {card.ImageData.Length} bytes").FontSize(8);
                                    }
                                    column.Item().PaddingVertical(5);
                                }
                            });
                        });
                    });
                    
                    var pdfBytes = document.GeneratePdf();
                    DebugHelper.WriteDebug($"TestPdfService: PDF generated successfully, size: {pdfBytes.Length} bytes");
                    return pdfBytes;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteDebug($"TestPdfService: Error generating PDF: {ex.Message}");
                    DebugHelper.WriteDebug($"TestPdfService: Stack trace: {ex.StackTrace}");
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
                    DebugHelper.WriteDebug("TestPdfService: Starting preview generation");
                    
                    var document = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(595, 842); // A4
                            page.Margin(20);
                            
                            page.Content().Column(column =>
                            {
                                column.Item().Text("TEST PREVIEW GENERATION").FontSize(20).Bold();
                                column.Item().PaddingVertical(10);
                                column.Item().Text($"Cards in collection: {cards.Count}").FontSize(12);
                                
                                for (int i = 0; i < Math.Min(cards.Count, 3); i++)
                                {
                                    var card = cards[i];
                                    column.Item().Text($"Card {i+1}: {card.Name}").FontSize(10);
                                }
                            });
                        });
                    });
                    
                    var images = document.GenerateImages(new ImageGenerationSettings
                    {
                        RasterDpi = 150,
                        ImageFormat = ImageFormat.Png
                    });

                    if (images.Any())
                    {
                        var imageBytes = images.First();
                        using var stream = new MemoryStream(imageBytes);
                        var bitmap = new Bitmap(stream);
                        DebugHelper.WriteDebug($"TestPdfService: Preview generated successfully");
                        return bitmap;
                    }
                    else
                    {
                        DebugHelper.WriteDebug("TestPdfService: No images generated");
                        return null!;
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteDebug($"TestPdfService: Error generating preview: {ex.Message}");
                    DebugHelper.WriteDebug($"TestPdfService: Stack trace: {ex.StackTrace}");
                    throw;
                }
            });
        }
    }
}