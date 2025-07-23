using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;




namespace ProxyStudio.Services
{
    public interface IMpcFillService
    {
        Task<List<Card>> LoadCardsFromXmlAsync(string xmlFilePath, IProgress<MpcFillProgress>? progress = null);
        Task<byte[]> DownloadCardImageAsync(string cardId);
        Task<List<Card>> ProcessXmlContentAsync(string xmlContent, IProgress<MpcFillProgress>? progress = null);
    }

    public class MpcFillProgress
    {
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public string CurrentOperation { get; set; } = "";
        public string CurrentCardName { get; set; } = "";
        public double PercentageComplete => TotalSteps > 0 ? (double)CurrentStep / TotalSteps * 100 : 0;
    }


    
    

    public class MpcFillService : IMpcFillService
    {
        /// This class is to obtain and parse xml from mpcfill.com
        /// 
        private readonly HttpClient _httpClient;

        private readonly IConfigManager _configManager;
        private readonly string _cacheFolder;

        // Add this to your constructor for debugging
        public MpcFillService(HttpClient httpClient, IConfigManager configManager)
        {
            _httpClient = httpClient;
            _configManager = configManager;
            _cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProxyStudio",
                "Cache");
        
            // DEBUG: Log cache folder info
            DebugHelper.WriteDebug($"Cache folder: {_cacheFolder}");
            DebugHelper.WriteDebug($"Cache folder exists: {Directory.Exists(_cacheFolder)}");
    
            // Ensure cache directory exists
            try
            {
                Directory.CreateDirectory(_cacheFolder);
                DebugHelper.WriteDebug($"Cache directory created/verified successfully");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error creating cache directory: {ex.Message}");
            }
        }
        
        

        private class CardMetadata
        {
            public string Name { get; set; } = "";
            public string Id { get; set; } = "";
            public string Description { get; set; } = "";
            public string Query { get; set; } = "";
            public bool EnableBleed { get; set; } = true;
        }

        public async Task<List<Card>> LoadCardsFromXmlAsync(string xmlFilePath,
            IProgress<MpcFillProgress>? progress = null)
        {
            if (!File.Exists(xmlFilePath))
                throw new FileNotFoundException($"XML file not found: {xmlFilePath}");

            var xmlContent = await File.ReadAllTextAsync(xmlFilePath);
            return await ProcessXmlContentAsync(xmlContent, progress);
        }


        public async Task<List<Card>> ProcessXmlContentAsync(string xmlContent,
            IProgress<MpcFillProgress>? progress = null)
        {
            try
            {
                // Parse XML to get card metadata
                var cardMetadata = ParseXmlToCardMetadata(xmlContent);

                var progressInfo = new MpcFillProgress
                {
                    TotalSteps = cardMetadata.Count + 1, // +1 for parsing step
                    CurrentStep = 1,
                    CurrentOperation = "Parsing XML file..."
                };
                progress?.Report(progressInfo);

                var completedCards = new List<Card>();

                // Process each card with progress reporting
                for (int i = 0; i < cardMetadata.Count; i++)
                {
                    var metadata = cardMetadata[i];

                    progressInfo.CurrentStep = i + 2; // +2 because step 1 was parsing
                    progressInfo.CurrentCardName = metadata.Name;
                    progressInfo.CurrentOperation = $"Loading image for {metadata.Name}...";
                    progress?.Report(progressInfo);

                    try
                    {
                        var imageData = await LoadImageWithCacheAsync(metadata.Id, metadata.Name);
                        var card = CreateCardFromMetadata(metadata, imageData);
                        completedCards.Add(card);

                        DebugHelper.WriteDebug($"Successfully processed card: {metadata.Name}");
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteDebug($"Failed to process card {metadata.Name}: {ex.Message}");

                        // Create card with placeholder image
                        var placeholderImage = CreatePlaceholderImage();
                        var card = CreateCardFromMetadata(metadata, placeholderImage);
                        completedCards.Add(card);
                    }
                }

                return completedCards;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error processing MPC Fill XML: {ex.Message}");
                throw new InvalidOperationException($"Failed to process MPC Fill XML: {ex.Message}", ex);
            }
        }

        private async Task<byte[]> LoadImageWithCacheAsync(string cardId, string cardName)
        {
            // FIXED: Look for PNG cache files (high-res)
            var cacheFilePath = Path.Combine(_cacheFolder, $"{cardId}.png");
    
            DebugHelper.WriteDebug($"Checking cache for {cardName} at: {cacheFilePath}");

            if (File.Exists(cacheFilePath))
            {
                try
                {
                    DebugHelper.WriteDebug($"CACHE HIT: Loading {cardName} from HIGH-RES cache");
                    var cachedData = await LoadImageFromFileAsync(cacheFilePath);
                    DebugHelper.WriteDebug($"CACHE SUCCESS: Loaded {cachedData.Length} bytes for {cardName} (already high-res)");
                    return cachedData;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteDebug($"CACHE ERROR: {ex.Message}");
                }
            }

            // Download, process, and cache
            DebugHelper.WriteDebug($"DOWNLOADING: {cardName} from MPC Fill API");
            var imageData = await DownloadCardImageAsync(cardId);
    
            // Save to cache (this will process to high-res)
            await SaveImageToCacheAsync(imageData, cacheFilePath);

            // Load the cached high-res version we just saved
            if (File.Exists(cacheFilePath))
            {
                return await LoadImageFromFileAsync(cacheFilePath);
            }
    
            // Fallback: process in memory if caching failed
            return ProcessImageToHighResolution(imageData);
        }

        private async Task<byte[]> LoadImageFromFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                // FAST: Cached images are already high-res, just load them
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(filePath);
                return ImageSharpToWPFConverter.ImageToByteArray(image);
            });
        }

        private async Task SaveImageToCacheAsync(byte[] imageData, string cacheFilePath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath)!);
        
                await Task.Run(() =>
                {
                    // FIXED: Save the high-resolution processed image to cache
                    // This way we don't need to reprocess when loading from cache
                    using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageData);
            
                    // Process to high resolution ONCE during caching
                    const int baseDpi = 600;
                    var baseWidth = (int)(2.5 * baseDpi);   // 1500 pixels
                    var baseHeight = (int)(3.5 * baseDpi);  // 2100 pixels

                    image.Mutate<Rgba32>(x => x.Resize(new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(baseWidth, baseHeight),
                        Mode = ResizeMode.Stretch,
                        Sampler = KnownResamplers.Lanczos3
                    }));

                    // Save as high-quality PNG to preserve all detail
                    var pngEncoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
                    {
                        CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression,
                        ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
                    };
            
                    // Change extension to .png since we're saving PNG now
                    var pngCacheFilePath = Path.ChangeExtension(cacheFilePath, ".png");
                    image.Save(pngCacheFilePath, pngEncoder);
                });

                DebugHelper.WriteDebug($"Cached HIGH-RES image: {Path.GetFileName(cacheFilePath)}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Failed to cache image {cacheFilePath}: {ex.Message}");
            }
        }


        private byte[] ProcessImageToHighResolution(byte[] imageData)
        {
            const int baseDpi = 600;
            var baseWidth = (int)(2.5 * baseDpi); // 1500 pixels
            var baseHeight = (int)(3.5 * baseDpi); // 2100 pixels

            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageData);

            // FIXED: Explicitly specify the generic type to resolve ambiguity
            image.Mutate<Rgba32>(x => x.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(baseWidth, baseHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            }));

            var pngEncoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
            {
                CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression,
                ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
            };

            using var ms = new MemoryStream();
            image.Save(ms, pngEncoder);
            return ms.ToArray();
        }

        private byte[] CreatePlaceholderImage()
        {
            // Create a simple placeholder image for failed downloads
            const int width = 1500;
            const int height = 2100;
            
            using var image = new SixLabors.ImageSharp.Image<Rgba32>(width, height);
            
            // FIXED: Explicitly specify the generic type to resolve ambiguity
            image.Mutate<Rgba32>(x => x.BackgroundColor(SixLabors.ImageSharp.Color.LightGray));
            
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        private List<CardMetadata> ParseXmlToCardMetadata(string xmlContent)
        {
            var cards = new List<CardMetadata>();

            try
            {
                var doc = XDocument.Parse(xmlContent);
                var order = doc.Element("order");
                var fronts = order?.Element("fronts");

                if (fronts == null)
                    throw new InvalidOperationException("Invalid MPC Fill XML format: missing 'fronts' element");

                foreach (var cardElement in fronts.Elements("card"))
                {
                    var metadata = new CardMetadata
                    {
                        Name = cardElement.Element("name")?.Value ?? "Unknown Card",
                        Id = cardElement.Element("id")?.Value ?? Guid.NewGuid().ToString(),
                        Description = cardElement.Element("description")?.Value ?? "No Description",
                        Query = cardElement.Element("query")?.Value ?? "",
                        EnableBleed = bool.Parse(cardElement.Element("bleedchecked")?.Value ?? "true")
                    };

                    cards.Add(metadata);
                }

                DebugHelper.WriteDebug($"Parsed {cards.Count} cards from XML");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse MPC Fill XML: {ex.Message}", ex);
            }

            return cards;
        }

        private Card CreateCardFromMetadata(CardMetadata metadata, byte[] imageData)
        {
            var card = new Card(metadata.Name, metadata.Id, imageData, _configManager)
            {
                Query = metadata.Description,
                EnableBleed = metadata.EnableBleed,
                ImageDownloaded = true
            };

            return card;
        }

        public async Task<byte[]> DownloadCardImageAsync(string cardId)
        {
            const string baseUrl =
                "https://script.google.com/macros/s/AKfycbw8laScKBfxda2Wb0g63gkYDBdy8NWNxINoC4xDOwnCQ3JMFdruam1MdmNmN4wI5k4/exec";

            var queryParams = new Dictionary<string, string> { { "id", cardId } };
            var fullUrl = QueryStringHelper.BuildUrlWithQueryStringUsingStringConcat(baseUrl, queryParams);

            try
            {
                var base64String = await _httpClient.GetStringAsync(fullUrl);
                return Convert.FromBase64String(base64String);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"Failed to download image for card ID {cardId}: {ex.Message}", ex);
            }
        }


        public List<Card> ParseMyXML(string xmlContent)
        {



            if (string.IsNullOrEmpty(xmlContent))
            {
                throw new ArgumentException("XML content cannot be null or empty.", nameof(xmlContent));
            }
            // Parse the XML content and populate the _cards list
            // This is a placeholder for actual XML parsing logic
            // You would typically use an XML parser like System.Xml.Linq or System.Xml.Serialization here

            XDocument doc = XDocument.Parse(xmlContent);
            XElement? order = doc.Element("order");

            var details = order?.Element("details");
            var fronts = order?.Element("fronts");

            Order parsed = new Order
            {
                Quantity = int.Parse(order?.Element("quantity")?.Value ?? "0"),
                Bracket = int.Parse(order?.Element("bracket")?.Value ?? "0"),
                Stock = order?.Element("stock")?.Value ?? "Unknown",
                Foil = bool.Parse(order?.Element("foil")?.Value ?? "false"),
                CardBack = order?.Element("cardback")?.Value ?? "DefaultCardBack"

            };

            foreach (XElement card in fronts.Elements("card"))
            {
                string name = card.Element("name")?.Value ?? "Unknown";
                string id = card.Element("id")?.Value ?? "Unknown";
                string description = card.Element("description")?.Value ?? "No Description";
                string query = card.Element("query")?.Value ?? "Default Query";


                Card newCard = new Card(name, id, query)
                {
                    Query = description,
                    // _Width = int.Parse(card.Element("width")?.Value ?? "83"),
                    // _Height = int.Parse(card.Element("height")?.Value ?? "118"),
                    EnableBleed = bool.Parse(card.Element("bleedchecked")?.Value ?? "true")
                };
                parsed.Cards.Add(newCard);
            }




            return parsed.Cards;

        }





    }

    public class Order
    {
        public int Quantity { get; set; }
        public int Bracket { get; set; }
        public string Stock { get; set; }
        public bool Foil { get; set; }
        public List<Card> Cards { get; set; } = new();
        public string CardBack { get; set; }
    }
}