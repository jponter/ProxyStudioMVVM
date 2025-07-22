using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Dialogs;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Metsys.Bson;
using Microsoft.Extensions.DependencyInjection;
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
    //di configmanager interface
    private readonly IConfigManager _configManager;
    private readonly IPdfGenerationService _pdfService;
    private static readonly HttpClient httpClient = new HttpClient();

    //public ObservableCollection<Card> Cards { get; } = new();
    public CardCollection Cards { get; private set; } = new();

    // Print ViewModel
    [ObservableProperty] private PrintViewModel? _printViewModel;

    // configuration properties
    [ObservableProperty] private bool _globalBleedEnabled;

    partial void OnGlobalBleedEnabledChanged(bool value)
    {
        _configManager.Config.GlobalBleedEnabled = value;
        DebugHelper.WriteDebug("Global bleed enabled changed to " + value);
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
    public MainViewModel(IConfigManager configManager, IPdfGenerationService pdfService)
    {
        _configManager = configManager;
        _pdfService = pdfService;

        if (Design.IsDesignMode)
        {
            // Only set up minimal data for design-time
            // Don't load config, don't load images, etc.
            Cards.AddRange(AddTestCards());
            // Create a simple PrintViewModel for design time
            PrintViewModel = new PrintViewModel(_pdfService, _configManager, Cards);
            return;
        }

#if DEBUG
        DebugHelper.WriteDebug("Creating new MainViewModel.");
#endif

#if DEBUG
        DebugHelper.WriteDebug("Set the _configManager in Mainviewmodel");
        DebugHelper.WriteDebug($"Before load: GlobalBleedEnabled = {GlobalBleedEnabled}");
#endif

        GlobalBleedEnabled = _configManager.Config.GlobalBleedEnabled;
        // Cards.AddRange(AddTestCards());
        // PrintViewModel?.RefreshCardInfo();
        
        DebugHelper.WriteDebug($"Cards loaded: {Cards.Count} cards");
        
        // Initialize PrintViewModel
        PrintViewModel = new PrintViewModel(_pdfService, _configManager, Cards);
        
        DebugHelper.WriteDebug($"PrintViewModel initialized with {Cards.Count} cards");
        DebugHelper.WriteDebug($"PrintViewModel.CardsPerRow = {PrintViewModel.CardsPerRow}");
        DebugHelper.WriteDebug($"PrintViewModel.ShowCuttingLines = {PrintViewModel.ShowCuttingLines}");
    }

    // Constructor for design-time support
    public MainViewModel(IConfigManager configManager) : this(configManager, new DesignTimePdfService())
    {
        // Design-time constructor that creates a mock PDF service
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

    // Update the AddTestCardsRelayAsync method in MainViewModel:

    [RelayCommand(CanExecute = nameof(CanAddTestCards))]
    private async Task AddTestCardsRelayAsync()
    {
        IsBusy = true;

        // do the heavy lifting off the UI thread
        var newCards = await Task.Run(() => { return AddTestCards(); });

        Cards.AddRange(newCards);
    
        // IMPORTANT: Refresh PrintViewModel after adding cards
        PrintViewModel?.RefreshCardInfo();
        //this will ensure the PrintViewModel has the latest cards
        PrintViewModel?.GeneratePreviewCommand.Execute(null);
    
        DebugHelper.WriteDebug($"Added {newCards.Count} cards, total now: {Cards.Count}");

        IsBusy = false;
    }

    [RelayCommand]
    public void EditCard(Card card)
    {
        // open an edit dialog, navigate, etc.
        // e.g. DialogService.ShowEditCard(card);
        SelectedCard = card;
    }

    // Fix 1: Update AddTestCards() in MainViewModel.cs to store high-resolution images

    private List<Card> AddTestCards()
    {
        List<Card> cards = new();

        DebugHelper.WriteDebug("Loading high-resolution images for dynamic DPI scaling...");
    
        // Load images at their native resolution
        var image = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/preacher.jpg");
        var image2 = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/vampire.jpg");

        DebugHelper.WriteDebug($"Loaded images: preacher={image.Width}x{image.Height}, vampire={image2.Width}x{image2.Height}");

        // IMPORTANT: Store images at a high base resolution instead of scaling at startup
        // We'll use 600 DPI (1500x2100) as our "source" resolution that can be scaled down
        const int baseDpi = 600;
        var baseWidth = (int)(2.5 * baseDpi);   // 1500 pixels
        var baseHeight = (int)(3.5 * baseDpi);  // 2100 pixels

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

        DebugHelper.WriteDebug($"Created high-resolution base images: {baseWidth}x{baseHeight} ({baseDpi} DPI base)");
        DebugHelper.WriteDebug($"Image sizes: preacher={buffer.Length} bytes, vampire={buffer2.Length} bytes");

        for (var i = 0; i < 2; i++)
        {
            cards.Add(new Card("Preacher of the Schism", "12345", buffer, _configManager));
            cards.Add(new Card("Vampire Token", "563726", buffer2, _configManager));
            cards.Add(new Card("Preacher of the Schism", "12345", buffer, _configManager));
            cards.Add(new Card("Vampire Token", "563726", buffer2, _configManager));
        }

        foreach (var card in cards) card.EditMeCommand = EditCardCommand;
        foreach (var card in cards) card.EnableBleed= true;
        
    
        DebugHelper.WriteDebug($"Created {cards.Count} cards with high-resolution images ready for dynamic DPI scaling");
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
            DebugHelper.WriteDebug($"Processing image file: {fileName}");
        
            IsBusy = true;
        
            var card = await Task.Run(() => CreateCardFromImage(imageData, fileName));
        
            if (card != null)
            {
                Cards.AddCard(card);
                PrintViewModel?.RefreshCardInfo();
                PrintViewModel?.GeneratePreviewCommand.Execute(null);
            
                DebugHelper.WriteDebug($"Successfully added card from image: {fileName}");
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteDebug($"Error processing image file {fileName}: {ex.Message}");
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
        const int baseDpi = 600;
        var baseWidth = (int)(2.5 * baseDpi);   // 1500 pixels
        var baseHeight = (int)(3.5 * baseDpi);  // 2100 pixels
    
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

// Add this method to your MainViewModel class

    /// <summary>
    /// Processes XML files dropped onto the application
    /// </summary>
    /// <param name="xmlContent">The XML content as string</param>
    /// <param name="fileName">The original file name</param>
    public async Task ProcessXmlFileAsync(string xmlContent, string fileName)
    {
        try
        {
            DebugHelper.WriteDebug($"Processing XML file: {fileName}");
        
            // Set busy state
            IsBusy = true;
        
            // Parse XML content - replace this with your actual XML parsing logic
            var newCards = await Task.Run(() => ParseXmlToCards(xmlContent, fileName));
        
            if (newCards.Any())
            {
                // Add cards to collection
                Cards.AddRange(newCards);
            
                // Refresh PrintViewModel
                PrintViewModel?.RefreshCardInfo();
                PrintViewModel?.GeneratePreviewCommand.Execute(null);
            
                DebugHelper.WriteDebug($"Successfully added {newCards.Count} cards from {fileName}");
            }
            else
            {
                DebugHelper.WriteDebug($"No valid cards found in {fileName}");
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteDebug($"Error processing XML file {fileName}: {ex.Message}");
            // You might want to show a user-friendly error message here
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Parse XML content to Card objects - implement your specific XML format here
    /// </summary>
    /// <param name="xmlContent">Raw XML content</param>
    /// <param name="fileName">Source file name for debugging</param>
    /// <returns>List of parsed cards</returns>
    private List<Card> ParseXmlToCards(string xmlContent, string fileName)
    {
        var cards = new List<Card>();
    
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xmlContent);
        
            // Example: MPC XML format - adjust for your specific XML structure
            var cardElements = doc.Descendants("card");
        
            foreach (var cardElement in cardElements)
            {
                try
                {
                    var name = cardElement.Element("name")?.Value ?? "Unknown Card";
                    var id = cardElement.Element("id")?.Value ?? Guid.NewGuid().ToString();
                    var query = cardElement.Element("query")?.Value ?? "";
                    var imageUrl = cardElement.Element("imageUrl")?.Value;
                
                    Card newCard;
                
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        // // Download image if URL is provided
                        // var imageData = await DownloadImageAsync(imageUrl);
                        // if (imageData != null)
                        // {
                        //     newCard = new Card(name, id, imageData, _configManager);
                        // }
                        // else
                        // {
                        //     // Fallback to query-based card if image download fails
                        //     newCard = new Card(name, id, query);
                        // }
                    }
                    else
                    {
                        // Create card with query for later image loading
                        newCard = new Card(name, id, query);
                    }
                
                    // // Set up edit command
                    // newCard.EditMeCommand = EditCardCommand;
                    //
                    // cards.Add(newCard);
                
                    DebugHelper.WriteDebug($"Parsed card: {name} (ID: {id})");
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteDebug($"Error parsing individual card in {fileName}: {ex.Message}");
                    // Continue with other cards
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteDebug($"Error parsing XML document {fileName}: {ex.Message}");
            throw; // Re-throw to be handled by calling method
        }
    
        return cards;
    }

    /// <summary>
    /// Downloads image from URL and returns as byte array
    /// </summary>
    /// <param name="imageUrl">URL to download image from</param>
    /// <returns>Image data or null if download fails</returns>
    private async Task<byte[]?> DownloadImageAsync(string imageUrl)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30); // Set reasonable timeout
        
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
        
            // Optional: Resize to your preferred resolution here
            // You could use your existing image processing logic from AddTestCards()
        
            DebugHelper.WriteDebug($"Downloaded image from {imageUrl}: {imageBytes.Length} bytes");
            return imageBytes;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteDebug($"Failed to download image from {imageUrl}: {ex.Message}");
            return null;
        }
    }

    
    // Add this RelayCommand to your MainViewModel.cs class

    [RelayCommand]
    private void DeleteAllCards()
    {
        try
        {
            DebugHelper.WriteDebug("DeleteAllCards: Starting to delete all cards");
        
            var cardCount = Cards.Count;
        
            if (cardCount == 0)
            {
                DebugHelper.WriteDebug("DeleteAllCards: No cards to delete");
                return;
            }
        
            // Clear selection first
            SelectedCard = null;
        
            // Remove all cards from collection
            Cards.RemoveAllCards();
        
            // IMPORTANT: Refresh PrintViewModel after clearing cards
            // This will automatically clear the preview since CardCount = 0
            PrintViewModel?.RefreshCardInfo();
        
            DebugHelper.WriteDebug($"Successfully deleted all {cardCount} cards and cleared preview");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteDebug($"Error deleting all cards: {ex.Message}");
        }
    }

// Optional: Add a method to check if deletion is allowed
    private bool CanDeleteAllCards()
    {
        return Cards.Count > 0 && !IsBusy;
    }
    

// Add this RelayCommand to your MainViewModel.cs

    [RelayCommand]
    private void DeleteCard(Card card)
    {
        try
        {
            if (card == null)
            {
                DebugHelper.WriteDebug("DeleteCard: Card parameter is null");
                return;
            }

            DebugHelper.WriteDebug($"Deleting card: {card.Name} (ID: {card.Id})");

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

            DebugHelper.WriteDebug($"Successfully deleted card. Remaining cards: {Cards.Count}");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteDebug($"Error deleting card {card?.Name}: {ex.Message}");
        }
    }

// Optional: Add a method to check if deletion is allowed
    private bool CanDeleteCard(Card card)
    {
        return card != null && !IsBusy;
    }
    
    
    
    public async Task ProcessMPCFillXML( String fileName)
    {
        DebugHelper.WriteDebug("Entering LoadMPCFill Button.");
        IsBusy = true;
      
        var image = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/comingsoon.jpg");
        
        // IMPORTANT: Store images at a high base resolution instead of scaling at startup
        // We'll use 600 DPI (1500x2100) as our "source" resolution that can be scaled down
        const int baseDpi = 300;
        var baseWidth = (int)(2.5 * baseDpi);   // 1500 pixels
        var baseHeight = (int)(3.5 * baseDpi);  // 2100 pixels

        // Resize to high base resolution for maximum quality
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(baseWidth, baseHeight),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3 // High-quality resampling
        }));

        DebugHelper.WriteDebug($"Loaded image: {fileName} with size {image.Width}x{image.Height}");

        // Store as high-quality PNG to preserve all detail for later scaling
        var pngEncoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
        {
            CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression,
            ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
        };

        using var ms1 = new MemoryStream();
        image.Save(ms1, pngEncoder);
        var buffer = ms1.ToArray();
        

        
        DebugHelper.WriteDebug($"Selected file: {fileName}");

        var mpcFillXML = new MpcFillXML();
        var cardsToAdd = mpcFillXML.ParseMyXML(File.ReadAllText(fileName));

        foreach (var card in cardsToAdd)
        {
            DebugHelper.WriteDebug($"Adding card: {card.Name} with ID: {card.Id} , and Query: {card.Query}");
            card.ImageDownloaded = false;
            card.ImageData = buffer; // Use the high-res image as a placeholder
            Cards.AddCard(card);
        }

        DebugHelper.WriteDebug($"Added {cardsToAdd.Count} cards from MPCFill XML.");
        //RedrawCardGrid(true);
        
       
        
        // we need to update the image data for all cards
        await UpdateCardCollectionAsync(); // Now async
        
        
        
        // Refresh PrintViewModel
        
        PrintViewModel?.RefreshCardInfo();
        DebugHelper.WriteDebug($"Refreshing PrintViewModel after loading MPCFill XML. Cards count: {Cards.Count}");
        // Regenerate preview to reflect the change
        PrintViewModel?.GeneratePreviewCommand.Execute(null);
        DebugHelper.WriteDebug($"PrintViewModel preview generated after loading MPCFill XML.");
        
        IsBusy = false;
        DebugHelper.WriteDebug($"Exiting LoadMPCFill Button. Total cards: {Cards.Count}");
        
    }
    

    private async Task UpdateCardCollectionAsync()
    {
        DebugHelper.WriteDebug($"Entering UpdateCardCollectionAsnc. Total cards: {Cards.Count}");

        var checkedCards = Cards.Count > 0 ? Cards : null;
        var checkedCards1 = Cards.Where(card => !card.ImageDownloaded).ToList();

        var tasks = Cards
            .Where(card => !card.ImageDownloaded)
            .Select(async card =>
            {
                DebugHelper.WriteDebug($"Card {card.Name} with ID {card.Id} has not downloaded the image yet.");

                await LoadImageFromCacheAsync(card); // Async load
                card.ImageDownloaded = true;
            });
        DebugHelper.WriteDebug($"UpdateCardCollectionAsync - Finished selecting cards for tasks");

        await Task.WhenAll(tasks); // Wait for all image loading tasks
    }

    public async Task LoadImageFromCacheAsync(Card card)
    {
        DebugHelper.WriteDebug("Entering LoadImageFromCacheAsync");
        byte[] imageBuffer = Array.Empty<byte>();
        string cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProxyStudio", "Cache");
        string imageFilePath = Path.Combine(cacheFolder, $"{card.Id}.jpg");

        if (!File.Exists(imageFilePath))
        {
            DebugHelper.WriteDebug($"Image for card {card.Name} with ID {card.Id} does not exist in cache. Downloading...");

            GetMPCImages helperInstance = new GetMPCImages();
            imageBuffer = await helperInstance.GetImageFromMPCFill(card.Id, httpClient);

            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            await Task.Run(() =>
            {
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageBuffer);
                var encoder = new JpegEncoder { Quality = 100 };
                image.Save(imageFilePath, encoder);
            });
        }
        else
        {
            DebugHelper.WriteDebug($"Image for card {card.Name} with ID {card.Id} found in cache.");

            imageBuffer = await Task.Run(() =>
            {
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageFilePath);
                return ImageSharpToWPFConverter.ImageToByteArray(image);
            });
        }

        card.ImageData = imageBuffer;
    }
    
}