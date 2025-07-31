using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<MpcFillService> _logger;
        private readonly string _cacheFolder;
        // NEW: Use SemaphoreSlim per cardId for thread-safe downloads
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _downloadSemaphores = new();

        // Add this to your constructor for debugging
        public MpcFillService(HttpClient httpClient, IConfigManager configManager, ILogger<MpcFillService> logger)  
        {
            _httpClient = httpClient;
            _configManager = configManager;
            _logger = logger;
            _cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProxyStudio",
                "Cache");
        
            // DEBUG: Log cache folder info
            logger.LogDebug($"Cache folder: {_cacheFolder}");
            logger.LogDebug($"Cache folder exists: {Directory.Exists(_cacheFolder)}");
    
            // Ensure cache directory exists
            try
            {
                Directory.CreateDirectory(_cacheFolder);
                logger.LogDebug($"Cache directory created/verified successfully");
            }
            catch (Exception ex)
            {
                logger.LogCritical($"Error creating cache directory: {ex.Message}");
            }
        }
        
        

        private class CardMetadata
        {
            public string Name { get; set; } = "";
            public string Id { get; set; } = "";
            public string Description { get; set; } = "";
            public string Query { get; set; } = "";
            public bool EnableBleed { get; set; } = true;
            public int SlotPosition { get; set; }  // NEW: Track which slot this represents
            public string OriginalCardId { get; set; } = "";  // NEW: Track original card for deduplication
        }

        public async Task<List<Card>> LoadCardsFromXmlAsync(string xmlFilePath,
            IProgress<MpcFillProgress>? progress = null)
        {
            if (!File.Exists(xmlFilePath))
                throw new FileNotFoundException($"XML file not found: {xmlFilePath}");

            var xmlContent = await File.ReadAllTextAsync(xmlFilePath);
            return await ProcessXmlContentAsync(xmlContent, progress);
        }


        
        
        
        // PARALLEL PROCESSING: Process multiple cards simultaneously
        //var semaphore = new SemaphoreSlim(3, 3); // Process 3 cards at once (adjust as needed)
        // Auto-detect based on CPU cores
        //var maxConcurrency = Math.Max(2, Environment.ProcessorCount / 2);
        //var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
// Replace your ProcessXmlContentAsync method with this enhanced debugging version:

public async Task<List<Card>> ProcessXmlContentAsync(string xmlContent, IProgress<MpcFillProgress>? progress = null)
{
    try
    {
        var cardMetadata = ParseXmlToCardMetadata(xmlContent);
        
        var progressInfo = new MpcFillProgress
        {
            TotalSteps = cardMetadata.Count + 1,
            CurrentStep = 1,
            CurrentOperation = "Parsing XML file..."
        };
        progress?.Report(progressInfo);

        // ✅ FIXED: Run Parallel.ForEach on background thread to avoid UI blocking
        return await Task.Run(() =>
        {
            // ✅ Use array to preserve order
            var completedCards = new Card[cardMetadata.Count];
            var completedCount = 0;

            // ✅ BALANCED APPROACH: Determine optimal concurrency
            var maxConcurrency = Math.Max(2, Environment.ProcessorCount / 2);
            
            // ✅ PARALLEL.FOREACH: Guaranteed parallel execution
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency
            };

           _logger.LogDebug($"=== MPC FILL PARALLEL PROCESSING START ===");
           _logger.LogDebug($"Cards to process: {cardMetadata.Count}");
           _logger.LogDebug($"System has {Environment.ProcessorCount} logical processors");
           _logger.LogDebug($"Using {maxConcurrency} max concurrent threads (balanced approach)");
           _logger.LogDebug($"Using Parallel.ForEach on background thread (non-blocking UI)");
           _logger.LogDebug($"Order preservation: Using fixed array at original XML indices");

            var parallelStartTime = DateTime.Now;

            // ✅ FIXED: Parallel.ForEach on background thread
            try
            {
                Parallel.ForEach(
                    cardMetadata.Select((metadata, index) => new { Metadata = metadata, Index = index }),
                    parallelOptions,
                    item =>
                    {
                        var threadId = Thread.CurrentThread.ManagedThreadId;
                        var metadata = item.Metadata;
                        var index = item.Index;
                        
                        try
                        {
                            _logger.LogDebug($"PARALLEL MPC: Processing {metadata.Name} (XML index {index}) on thread {threadId}");
                            
                            var cardStartTime = DateTime.Now;
                            Card card;
                            try
                            {
                                var imageData = LoadImageWithCacheSync(metadata.Id, metadata.Name);
                                card = CreateCardFromMetadata(metadata, imageData);
                                
                                var cardProcessTime = DateTime.Now - cardStartTime;
                                _logger.LogDebug($"PARALLEL MPC SUCCESS: {metadata.Name} (index {index}) on thread {threadId} in {cardProcessTime.TotalSeconds:F1}s");
                            }
                            catch (Exception ex)
                            {
                                var cardProcessTime = DateTime.Now - cardStartTime;
                                _logger.LogCritical($"PARALLEL MPC ERROR: {metadata.Name} (index {index}) on thread {threadId} after {cardProcessTime.TotalSeconds:F1}s - {ex.Message}");
                                var placeholderImage = CreatePlaceholderImage();
                                card = CreateCardFromMetadata(metadata, placeholderImage);
                            }

                            // ✅ Store card at its original index to preserve order
                            completedCards[index] = card;
                            
                            // Update progress
                            var newCompletedCount = Interlocked.Increment(ref completedCount);
                            
                            var tempProgressInfo = new MpcFillProgress
                            {
                                TotalSteps = cardMetadata.Count + 1,
                                CurrentStep = newCompletedCount + 1,
                                CurrentOperation = $"Completed {newCompletedCount}/{cardMetadata.Count} cards (Thread {threadId})",
                                CurrentCardName = metadata.Name
                            };
                            progress?.Report(tempProgressInfo);

                            _logger.LogDebug($"PROGRESS UPDATE: Stored {metadata.Name} at index {index}, completed {newCompletedCount}/{cardMetadata.Count}, thread {threadId}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogCritical($"ERROR: Processing {metadata?.Name ?? "unknown"} (index {index}) failed on thread {threadId}: {ex.Message}");
                            
                            // Still increment counter and store placeholder for failed cards
                            var placeholderImage = CreatePlaceholderImage();
                            var errorCard = CreateCardFromMetadata(metadata, placeholderImage);  
                            completedCards[index] = errorCard;
                            Interlocked.Increment(ref completedCount);
                        }
                    });
                    
                var parallelEndTime = DateTime.Now;
                var parallelDuration = parallelEndTime - parallelStartTime;
                
                // ✅ DETAILED PERFORMANCE ANALYSIS
                _logger.LogDebug($"=== MPC FILL PARALLEL PROCESSING COMPLETE ===");
                _logger.LogDebug($"Processed {cardMetadata.Count} cards successfully");
                _logger.LogDebug($"Parallel processing time: {parallelDuration.TotalSeconds:F1} seconds");
                _logger.LogDebug($"Average per card: {parallelDuration.TotalMilliseconds / cardMetadata.Count:F0} ms");
                
                if (cardMetadata.Count > 1)
                {
                    var estimatedSequentialTime = cardMetadata.Count * 5.0; // Assume 5s per card sequential
                    var speedupRatio = estimatedSequentialTime / parallelDuration.TotalSeconds;
                    _logger.LogDebug($"Estimated speedup: {speedupRatio:F1}x faster than sequential processing");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"ERROR: Parallel.ForEach failed: {ex.Message}");
                _logger.LogCritical($"Stack trace: {ex.StackTrace}");
                throw;
            }
            
            // ✅ Convert array to list - maintains original XML order
            var orderedResult = completedCards.ToList();
            
            // ✅ VERIFY ORDER PRESERVATION
            _logger.LogDebug($"=== ORDER PRESERVATION VERIFICATION ===");
            var orderCorrect = true;
            for (int i = 0; i < Math.Min(orderedResult.Count, cardMetadata.Count); i++)
            {
                var expectedName = cardMetadata[i].Name;
                var actualName = orderedResult[i]?.Name ?? "NULL";
                if (expectedName != actualName)
                {
                    _logger.LogCritical($"❌ ORDER MISMATCH at index {i}: expected '{expectedName}', got '{actualName}'");
                    orderCorrect = false;
                }
                else
                {
                    _logger.LogDebug($"✅ ORDER CORRECT at index {i}: '{actualName}'");
                }
            }
            
            if (orderCorrect)
            {
                _logger.LogDebug($"✅ ALL CARDS IN CORRECT XML ORDER - Parallel processing with order preservation successful!");
            }
            else
            {
                _logger.LogCritical($"❌ ORDER PRESERVATION FAILED - Check array indexing logic");
            }
            
            //clean up the semaphores
            _logger.LogDebug("Cleaning up semaphores...");
            CleanupSemaphores();
            
            
            _logger.LogDebug($"PARALLEL MPC: Returning {orderedResult.Count} cards in original XML order");
            
            return orderedResult;
        }); // ✅ End of Task.Run - this keeps UI responsive!
    }
    catch (Exception ex)
    {
        _logger.LogDebug("Cleaning up Semaphores before throwing exception...");
        CleanupSemaphores();
        _logger.LogCritical($"Error in parallel MPC Fill processing: {ex.Message}");
        _logger.LogCritical($"Stack trace: {ex.StackTrace}");
        throw new InvalidOperationException($"Failed to process MPC Fill XML: {ex.Message}", ex);
    }
}

// FIXED: Thread-safe caching using SemaphoreSlim
    // FIXED: Thread-safe caching using SemaphoreSlim with proper disposal handling
    private byte[] LoadImageWithCacheSync(string cardId, string cardName)
    {
        var cacheFilePath = Path.Combine(_cacheFolder, $"{cardId}.png");
        var threadId = Thread.CurrentThread.ManagedThreadId;
        
        _logger.LogDebug($"THREAD {threadId}: Requesting {cardName} (cardId: {cardId})");

        // Quick cache check first - if file exists and is valid, return immediately
        if (File.Exists(cacheFilePath))
        {
            try
            {
                _logger.LogDebug($"THREAD {threadId}: CACHE HIT - Loading {cardName} from cache");
                return LoadImageFromFileSync(cacheFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"THREAD {threadId}: CACHE ERROR for {cardName}: {ex.Message}");
                // File exists but is corrupted, delete it so we re-download
                try
                {
                    File.Delete(cacheFilePath);
                    _logger.LogDebug($"THREAD {threadId}: Deleted corrupted cache file for {cardName}");
                }
                catch { /* Ignore deletion errors */ }
            }
        }

        // Use semaphore to ensure only one download per cardId
        var semaphore = _downloadSemaphores.GetOrAdd(cardId, _ => new SemaphoreSlim(1, 1));
        
        _logger.LogDebug($"THREAD {threadId}: Acquiring semaphore for {cardName}");
        
        try
        {
            semaphore.Wait(); // Block until we can proceed
        }
        catch (ObjectDisposedException)
        {
            // If semaphore was disposed, create a new one and try again
            _logger.LogDebug($"THREAD {threadId}: Semaphore was disposed, creating new one for {cardName}");
            semaphore = _downloadSemaphores.AddOrUpdate(cardId, _ => new SemaphoreSlim(1, 1), (_, _) => new SemaphoreSlim(1, 1));
            semaphore.Wait();
        }
        
        try
        {
            // Double-check: another thread might have downloaded while we waited
            if (File.Exists(cacheFilePath))
            {
                _logger.LogDebug($"THREAD {threadId}: CACHE HIT (after wait) - {cardName} was downloaded by another thread");
                return LoadImageFromFileSync(cacheFilePath);
            }

            // We're the first thread - do the download
            _logger.LogDebug($"THREAD {threadId}: DOWNLOADING {cardName} from MPC Fill API (thread-safe)");
            var imageData = DownloadCardImageSync(cardId);
            
            _logger.LogDebug($"THREAD {threadId}: CACHING {cardName} to {Path.GetFileName(cacheFilePath)}");
            SaveImageToCacheSync(imageData, cacheFilePath);

            // Load the cached version we just saved
            if (File.Exists(cacheFilePath))
            {
                _logger.LogDebug($"THREAD {threadId}: Successfully cached and loading {cardName}");
                return LoadImageFromFileSync(cacheFilePath);
            }
            
            _logger.LogDebug($"THREAD {threadId}: Cache save failed for {cardName}, processing in memory");
            return ProcessImageToHighResolution(imageData);
        }
        catch (Exception ex)
        {
            _logger.LogCritical($"THREAD {threadId}: Download/cache failed for {cardName}: {ex.Message}");
            return CreatePlaceholderImage();
        }
        finally
        {
            try
            {
                semaphore.Release();
                _logger.LogDebug($"THREAD {threadId}: Released semaphore for {cardName}");
            }
            catch (ObjectDisposedException)
            {
                _logger.LogDebug($"THREAD {threadId}: Semaphore was already disposed for {cardName}");
            }
            catch (SemaphoreFullException)
            {
                _logger.LogDebug($"THREAD {threadId}: Semaphore was already at full count for {cardName}");
            }
        }
    }
    
    private void CleanupSemaphores()
    {
        var disposedCount = 0;
        foreach (var kvp in _downloadSemaphores)
        {
            try
            {
                if (_downloadSemaphores.TryRemove(kvp.Key, out var semaphore))
                {
                    semaphore.Dispose();
                    disposedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error disposing semaphore for {kvp.Key}: {ex.Message}");
            }
        }
        
        if (disposedCount > 0)
        {
            _logger.LogDebug($"Cleaned up {disposedCount} semaphores");
        }
    }

// ✅ NEW: Synchronous helper methods for Parallel.ForEach
private byte[] LoadImageFromFileSync(string filePath)
{
    using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(filePath);
    return ImageSharpToWPFConverter.ImageToByteArray(image);
}

private byte[] DownloadCardImageSync(string cardId)
{
    const string baseUrl = "https://script.google.com/macros/s/AKfycbw8laScKBfxda2Wb0g63gkYDBdy8NWNxINoC4xDOwnCQ3JMFdruam1MdmNmN4wI5k4/exec";
    
    var queryParams = new Dictionary<string, string> { { "id", cardId } };
    var fullUrl = QueryStringHelper.BuildUrlWithQueryStringUsingStringConcat(baseUrl, queryParams);

    try
    {
        // Use synchronous HTTP call for Parallel.ForEach
        var base64String = _httpClient.GetStringAsync(fullUrl).Result;
        return Convert.FromBase64String(base64String);
    }
    catch (HttpRequestException ex)
    {
        throw new InvalidOperationException($"Failed to download image for card ID {cardId}: {ex.Message}", ex);
    }
}

private void SaveImageToCacheSync(byte[] imageData, string cacheFilePath)
{
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath)!);
        
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

        // Save as high-quality PNG
        var pngEncoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
        {
            CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression,
            ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
        };
        
        // Use a temporary file and then move to avoid corruption
        var tempFilePath = cacheFilePath + ".tmp";
        image.Save(tempFilePath, pngEncoder);
            
        // Atomic move operation
        if (File.Exists(cacheFilePath))
        {
            File.Delete(cacheFilePath);
        }
        File.Move(tempFilePath, cacheFilePath);

        _logger.LogDebug($"Cached HIGH-RES image: {Path.GetFileName(cacheFilePath)}");
    }
    catch (Exception ex)
    {
        _logger.LogCritical($"Failed to cache image {cacheFilePath}: {ex.Message}");
            
        // Clean up temp file if it exists
        var tempFilePath = cacheFilePath + ".tmp";
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch { /* Ignore cleanup errors */ }
            
        throw; // Re-throw so caller can handle
    }
}


        // private async Task<byte[]> LoadImageWithCacheAsync(string cardId, string cardName)
        // {
        //     // FIXED: Look for PNG cache files (high-res)
        //     var cacheFilePath = Path.Combine(_cacheFolder, $"{cardId}.png");
        //
        //     DebugHelper.WriteDebug($"Checking cache for {cardName} at: {cacheFilePath}");
        //
        //     if (File.Exists(cacheFilePath))
        //     {
        //         try
        //         {
        //             DebugHelper.WriteDebug($"CACHE HIT: Loading {cardName} from HIGH-RES cache");
        //             var cachedData = await LoadImageFromFileAsync(cacheFilePath);
        //             DebugHelper.WriteDebug($"CACHE SUCCESS: Loaded {cachedData.Length} bytes for {cardName} (already high-res)");
        //             return cachedData;
        //         }
        //         catch (Exception ex)
        //         {
        //             DebugHelper.WriteDebug($"CACHE ERROR: {ex.Message}");
        //         }
        //     }
        //
        //     // Download, process, and cache
        //     DebugHelper.WriteDebug($"DOWNLOADING: {cardName} from MPC Fill API");
        //     var imageData = await DownloadCardImageAsync(cardId);
        //
        //     // Save to cache (this will process to high-res)
        //     await SaveImageToCacheAsync(imageData, cacheFilePath);
        //
        //     // Load the cached high-res version we just saved
        //     if (File.Exists(cacheFilePath))
        //     {
        //         return await LoadImageFromFileAsync(cacheFilePath);
        //     }
        //
        //     // Fallback: process in memory if caching failed
        //     return ProcessImageToHighResolution(imageData);
        // }

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
                    var baseCardData = new CardMetadata
                    {
                        Name = cardElement.Element("name")?.Value ?? "Unknown Card",
                        Id = cardElement.Element("id")?.Value ?? Guid.NewGuid().ToString(),
                        Description = cardElement.Element("description")?.Value ?? "No Description",
                        Query = cardElement.Element("query")?.Value ?? "",
                        EnableBleed = bool.Parse(cardElement.Element("bleedchecked")?.Value ?? "true")
                    };
                    //dont add yet
                    //cards.Add(metadata);
                    
                    //parse slots
                    // Parse slots - this is the key change
                    var slotsValue = cardElement.Element("slots")?.Value;
                    if (string.IsNullOrEmpty(slotsValue))
                    {
                        // No slots specified, treat as single card (slot 0)
                        cards.Add(new CardMetadata
                        {
                            Name = baseCardData.Name,
                            Id = baseCardData.Id,
                            Description = baseCardData.Description,
                            Query = baseCardData.Query,
                            EnableBleed = baseCardData.EnableBleed,
                            SlotPosition = 0,
                            OriginalCardId = baseCardData.Id
                        });
                    }
                    else
                    {
                        // Parse comma-separated slots and create separate entries
                        var slots = slotsValue.Split(',')
                            .Select(s => s.Trim())
                            .Where(s => int.TryParse(s, out _))
                            .Select(int.Parse)
                            .ToList();

                        foreach (var slotPosition in slots)
                        {
                            cards.Add(new CardMetadata
                            {
                                Name = $"{baseCardData.Name} (Slot {slotPosition})", // Differentiate in name
                                Id = baseCardData.Id, // Same image ID
                                Description = baseCardData.Description,
                                Query = baseCardData.Query,
                                EnableBleed = baseCardData.EnableBleed,
                                SlotPosition = slotPosition,
                                OriginalCardId = baseCardData.Id
                            });
                        }
                    }
                }

                // ADD THIS LINE: Sort by slot position to ensure index matches slot number
                cards = cards.OrderBy(c => c.SlotPosition).ToList();

                _logger.LogDebug($"Parsed {cards.Count} card slots from XML (expanded from multi-slot cards)");
        
                // Debug: Log first few cards to verify sorting
                for (int i = 0; i < Math.Min(5, cards.Count); i++)
                {
                    _logger.LogDebug($"Card at index {i}: {cards[i].Name} (SlotPosition: {cards[i].SlotPosition})");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse MPC Fill XML: {ex.Message}", ex);
            }

            return cards;
        }

        private Card CreateCardFromMetadata(CardMetadata metadata, byte[] imageData)
        {
            var card = new Card(metadata.Name, $"{metadata.OriginalCardId}_slot_{metadata.SlotPosition}", imageData, _configManager)
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


        // public List<Card> ParseMyXML(string xmlContent)
        // {
        //
        //
        //
        //     if (string.IsNullOrEmpty(xmlContent))
        //     {
        //         throw new ArgumentException("XML content cannot be null or empty.", nameof(xmlContent));
        //     }
        //     // Parse the XML content and populate the _cards list
        //     // This is a placeholder for actual XML parsing logic
        //     // You would typically use an XML parser like System.Xml.Linq or System.Xml.Serialization here
        //
        //     XDocument doc = XDocument.Parse(xmlContent);
        //     XElement? order = doc.Element("order");
        //
        //     var details = order?.Element("details");
        //     var fronts = order?.Element("fronts");
        //
        //     Order parsed = new Order
        //     {
        //         Quantity = int.Parse(order?.Element("quantity")?.Value ?? "0"),
        //         Bracket = int.Parse(order?.Element("bracket")?.Value ?? "0"),
        //         Stock = order?.Element("stock")?.Value ?? "Unknown",
        //         Foil = bool.Parse(order?.Element("foil")?.Value ?? "false"),
        //         CardBack = order?.Element("cardback")?.Value ?? "DefaultCardBack"
        //
        //     };
        //
        //     foreach (XElement card in fronts.Elements("card"))
        //     {
        //         string name = card.Element("name")?.Value ?? "Unknown";
        //         string id = card.Element("id")?.Value ?? "Unknown";
        //         string description = card.Element("description")?.Value ?? "No Description";
        //         string query = card.Element("query")?.Value ?? "Default Query";
        //
        //
        //         Card newCard = new Card(name, id, query)
        //         {
        //             Query = description,
        //             // _Width = int.Parse(card.Element("width")?.Value ?? "83"),
        //             // _Height = int.Parse(card.Element("height")?.Value ?? "118"),
        //             EnableBleed = bool.Parse(card.Element("bleedchecked")?.Value ?? "true")
        //         };
        //         parsed.Cards.Add(newCard);
        //     }
        //
        //
        //
        //
        //     return parsed.Cards;
        //
        // }

// Optional: Call this method at the end of ProcessXmlContentAsync for cleanup
        public void PerformMaintenance()
        {
            var remainingSemaphores = _downloadSemaphores.Count;
            if (remainingSemaphores > 0)
            {
                _logger.LogDebug($"Cache maintenance: {remainingSemaphores} semaphores still active");
            }
            else
            {
                _logger.LogDebug("Cache maintenance completed - all semaphores cleaned up");
            }
        }



    }

    public class Order
    {
        public int Quantity { get; set; }
        public int Bracket { get; set; }
        public string? Stock { get; set; }
        public bool Foil { get; set; }
        public List<Card> Cards { get; set; } = new();
        public string? CardBack { get; set; }
    }
}