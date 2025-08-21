// ProxyStudio - A cross-platform proxy management application.
// Copyright (C) 2025 James Ponter
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.
// 
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;



namespace ProxyStudio.Services;

#region Scryfall API Response Models

public class ScryfallSearchResponse
{
    [JsonPropertyName("object")] public string Object { get; set; }
    [JsonPropertyName("total_cards")] public int TotalCards { get; set; }
    [JsonPropertyName("has_more")] public bool HasMore { get; set; }
    [JsonPropertyName("next_page")] public string NextPage { get; set; }
    [JsonPropertyName("data")] public ScryfallCardResponse[] Data { get; set; }
}

public class ScryfallCardResponse
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("type_line")] public string TypeLine { get; set; }
    [JsonPropertyName("mana_cost")] public string ManaCost { get; set; }
    [JsonPropertyName("rarity")] public string Rarity { get; set; }
    [JsonPropertyName("set_name")] public string SetName { get; set; }
    [JsonPropertyName("set")] public string SetCode { get; set; }
    [JsonPropertyName("image_uris")] public ScryfallImageUris ImageUris { get; set; }
    [JsonPropertyName("card_faces")] public ScryfallCardFace[] CardFaces { get; set; } // For double-faced cards
}

public class ScryfallImageUris
{
    [JsonPropertyName("small")] public string Small { get; set; }
    [JsonPropertyName("normal")] public string Normal { get; set; }
    [JsonPropertyName("large")] public string Large { get; set; }
    [JsonPropertyName("art_crop")] public string ArtCrop { get; set; }
    [JsonPropertyName("border_crop")] public string BorderCrop { get; set; }
}

public class ScryfallCardFace
{
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("mana_cost")] public string ManaCost { get; set; }
    [JsonPropertyName("type_line")] public string TypeLine { get; set; }
    [JsonPropertyName("image_uris")] public ScryfallImageUris ImageUris { get; set; }
}

#endregion

public class ScryfallSearchService : ICardSearchService
{
    public string SourceName { get; } = "Scryfall";
    public bool IsAvailable { get; } = true;
    
    private readonly HttpClient _httpClient;
    private readonly IConfigManager _configManager;
    private readonly ILogger<ScryfallSearchService> _logger;
    private readonly IErrorHandlingService _errorHandler;
    private readonly string _cacheFolder;
    private readonly string _imageCacheFolder;
    
    // Rate limiting - Scryfall requests 50-100ms between requests
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private const int MinDelayMs = 75; // 75ms = ~13 requests/second (under 10/sec limit)
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = ScryfallJsonContext.Default
    };
    
    public ScryfallSearchService(
        HttpClient httpClient, 
        IConfigManager configManager, 
        ILogger<ScryfallSearchService> logger, 
        IErrorHandlingService errorHandler)
    {
        _httpClient = httpClient;
        _configManager = configManager;
        _logger = logger;
        _errorHandler = errorHandler;
        
        // Set up cache folder (similar to MpcFillService)
        _cacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProxyStudio",
            "ScryfallCache");
        
        
        
        _imageCacheFolder = Path.Combine(_cacheFolder, "Images");
        
        Directory.CreateDirectory(_cacheFolder);
        Directory.CreateDirectory(_imageCacheFolder);
        
        // Configure HttpClient with proper headers for Scryfall API
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ProxyStudio/1.0 (MTG Proxy Application)");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }
    
    
    public async Task<List<CardSearchResult>> SearchAsync(string query, SearchFilters filters, IProgress<SearchProgress>? progress = null)
    {
        try
        {
            progress?.Report(new SearchProgress { Status = "Connecting to Scryfall...", CurrentStep = 1, TotalSteps = 4 });
            
            _logger.LogInformation("Starting Scryfall search for query: {Query}", query);
            
            // Build search query with filters
            var searchQuery = BuildSearchQuery(query, filters);
            var results = new List<CardSearchResult>();
            var currentPage = 1;
            var maxPages = 3; // Limit to first 3 pages to avoid overwhelming results
            
            progress?.Report(new SearchProgress { Status = "Searching cards...", CurrentStep = 2, TotalSteps = 4 });
            
            do
            {
                var searchUrl = $"https://api.scryfall.com/cards/search?q={Uri.EscapeDataString(searchQuery)}&page={currentPage}";
                
                _logger.LogDebug("Requesting Scryfall API: {Url}", searchUrl);
                
                // Apply rate limiting
                await ApplyRateLimitAsync();
                
                var response = await _httpClient.GetAsync(searchUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogInformation("No cards found for query: {Query}", searchQuery);
                        break; // No results found
                    }
                    
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Scryfall API error {StatusCode}: {Content}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"Scryfall API returned {response.StatusCode}: {errorContent}");
                }
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize(jsonContent, ScryfallJsonContext.Default.ScryfallSearchResponse);
                
                if (searchResponse?.Data != null)
                {
                    foreach (var card in searchResponse.Data)
                    {
                        results.Add(ConvertToSearchResult(card));
                    }
                    
                    _logger.LogDebug("Retrieved {Count} cards from page {Page}", searchResponse.Data.Length, currentPage);
                    
                    // Check if we should continue to next page
                    if (!searchResponse.HasMore || currentPage >= maxPages)
                        break;
                }
                else
                {
                    break;
                }
                
                currentPage++;
                
            } while (true);
            
            progress?.Report(new SearchProgress { Status = "Processing results...", CurrentStep = 3, TotalSteps = 4 });
            
            // Start loading images for all results in parallel
            var imageTasks = results.Select(async result =>
            {
                try
                {
                    await result.LoadImageAsync(_httpClient);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load image for card: {CardName}", result.Name);
                }
            });
            
            // Don't wait for all images to load - let them load in background
            _ = Task.Run(async () =>
            {
                await Task.WhenAll(imageTasks);
                _logger.LogDebug("Completed loading preview images for search results");
            });
            
            _logger.LogInformation("Scryfall search completed: {ResultCount} cards found", results.Count);
            
            progress?.Report(new SearchProgress { Status = "Complete", CurrentStep = 4, TotalSteps = 4 });
            
            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during Scryfall search");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error during Scryfall search");
            throw new InvalidOperationException("Failed to parse Scryfall API response", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Scryfall search");
            throw;
        }
    }
    
    private static string BuildSearchQuery(string query, SearchFilters filters)
    {
        var queryParts = new List<string> { query };
        
        // Add filter constraints using Scryfall search syntax
        if (!string.IsNullOrWhiteSpace(filters.SetCode))
        {
            queryParts.Add($"set:{filters.SetCode.ToLower()}");
        }
        
        if (!string.IsNullOrWhiteSpace(filters.Colors))
        {
            queryParts.Add($"color:{filters.Colors.ToLower()}");
        }
        
        if (!string.IsNullOrWhiteSpace(filters.Type))
        {
            queryParts.Add($"type:{filters.Type.ToLower()}");
        }
        
        if (!string.IsNullOrWhiteSpace(filters.Rarity))
        {
            queryParts.Add($"rarity:{filters.Rarity.ToLower()}");
        }

        if (filters.IncludeExtras)
        {
            queryParts.Add("include:extras");
        }

        if (filters.UniquePrintings)
        {
            queryParts.Add("unique:printings");
        }
        
        return string.Join(" ", queryParts);
    }
    
    private static CardSearchResult ConvertToSearchResult(ScryfallCardResponse card)
    {
        // Handle double-faced cards by using the front face
        var cardName = card.Name;
        var manaCost = card.ManaCost ?? "";
        var typeLine = card.TypeLine ?? "";
        var imageUris = card.ImageUris;
        
        // If this is a double-faced card, use the front face
        if (card.CardFaces?.Length > 0)
        {
            var frontFace = card.CardFaces[0];
            cardName = frontFace.Name ?? card.Name;
            manaCost = frontFace.ManaCost ?? card.ManaCost ?? "";
            typeLine = frontFace.TypeLine ?? card.TypeLine ?? "";
            imageUris = frontFace.ImageUris ?? card.ImageUris;
        }
        
        return new CardSearchResult
        {
            Id = card.Id,
            Name = cardName,
            ImageUrl = imageUris?.Large ?? imageUris?.Normal ?? imageUris?.Small,
            SetName = card.SetName ?? "Unknown Set",
            SetCode = card.SetCode ?? "",
            TypeLine = typeLine,
            ManaCost = manaCost,
            Rarity = card.Rarity ?? "Unknown",
            SourceData = new Dictionary<string, object>
            {
                ["scryfall_data"] = card
            }
        };
    }
    
    private static async Task ApplyRateLimitAsync()
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var remainingDelay = TimeSpan.FromMilliseconds(MinDelayMs) - timeSinceLastRequest;
            
            if (remainingDelay > TimeSpan.Zero)
            {
                await Task.Delay(remainingDelay);
            }
            
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
    
    public async Task<Card> ConvertToCardAsync(CardSearchResult result)
    {
        try
        {
            _logger.LogDebug("Converting search result to card: {CardName}", result.Name);
            
            // Download and cache the card image
            var imageData = await GetCardImageDataAsync(result);
            
            // Create Card using your existing constructor pattern
            var card = new Card(result.Name, result.Id, imageData, _configManager)
            {
                Query = $"{result.SetName} - {result.TypeLine}",
                EnableBleed = false, // Scryfall cards typically don't have bleed
                ImageDownloaded = true
            };
            
            _logger.LogDebug("Successfully converted {CardName} to Card object", result.Name);
            return card;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert search result to card: {CardName}", result.Name);
            
            // Create placeholder card with error image if conversion fails
            var placeholderImage = CreatePlaceholderImage();
            return new Card($"{result.Name} (Error)", result.Id, placeholderImage, _configManager)
            {
                Query = "Failed to load card data",
                EnableBleed = true
            };
        }
    }
    
    private async Task<byte[]> GetCardImageDataAsync(CardSearchResult result)
    {
        if (string.IsNullOrEmpty(result.ImageUrl))
        {
            _logger.LogWarning("No image URL available for card: {CardName}", result.Name);
            return CreatePlaceholderImage();
        }
        
        try
        {
            // Generate cache key from image URL
            var cacheKey = GenerateCacheKey(result.ImageUrl);
            var cacheFilePath = Path.Combine(_imageCacheFolder, $"{cacheKey}.jpg");
            
            // Check if image is already cached and not stale (24 hours)
            if (File.Exists(cacheFilePath))
            {
                var cacheAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFilePath);
                if (cacheAge < TimeSpan.FromHours(24))
                {
                    _logger.LogDebug("Using cached image for card: {CardName}", result.Name);
                    return await File.ReadAllBytesAsync(cacheFilePath);
                }
            }
            
            _logger.LogDebug("Downloading image for card: {CardName} from {Url}", result.Name, result.ImageUrl);
            
            // Apply rate limiting for image downloads
            await ApplyRateLimitAsync();
            
            // Download the image
            var imageResponse = await _httpClient.GetAsync(result.ImageUrl);
            imageResponse.EnsureSuccessStatusCode();
            
            var imageData = await imageResponse.Content.ReadAsByteArrayAsync();
            
            // Cache the image
            await File.WriteAllBytesAsync(cacheFilePath, imageData);
            
            _logger.LogDebug("Successfully downloaded and cached image for: {CardName}", result.Name);
            return imageData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download image for card: {CardName}", result.Name);
            return CreateTestCardImage(result.Name);
        }
    }
    
    private static string GenerateCacheKey(string imageUrl)
    {
        // Generate a stable hash from the image URL for caching
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(imageUrl));
        return Convert.ToHexString(hashBytes)[..16]; // Use first 16 chars for filename
    }
    
    public async Task<Card> ConvertToCardAsyncTest(CardSearchResult result)
    {
        try
        {
            _logger.LogDebug("Converting search result to card: {CardName}", result.Name);
            
            // For now, use test image data (we'll add real image downloading later)
            var imageData = await GetCardImageDataAsync(result);
            
            // Create Card using your existing constructor pattern
            var card = new Card(result.Name, result.Id, imageData, _configManager)
            {
                Query = $"{result.SetName} - {result.TypeLine}",
                EnableBleed = true, // Default to enabled like MPC Fill
                ImageDownloaded = true
            };
            
            _logger.LogDebug("Successfully converted {CardName} to Card object", result.Name);
            return card;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert search result to card: {CardName}", result.Name);
            
            // Create placeholder card with error image if conversion fails
            var placeholderImage = CreatePlaceholderImage();
            return new Card($"{result.Name} (Error)", result.Id, placeholderImage, _configManager)
            {
                Query = "Failed to load card data",
                EnableBleed = true
            };
        }
    }

    
    private async Task<byte[]> GetCardImageDataAsyncTest(CardSearchResult result)
    {
        // For now, return test image data
        // Later we'll implement real Scryfall image downloading
        
        if (!string.IsNullOrEmpty(result.ImageUrl))
        {
            // TODO: Download actual image from result.ImageUrl
            return CreateTestCardImage(result.Name);
        }
        
        return CreatePlaceholderImage();
    }

    private byte[] CreateTestCardImage(string cardName)
    {
        const int width = 1500;
        const int height = 2100;
    
        using var image = new SixLabors.ImageSharp.Image<Rgba32>(width, height);
    
        // Different colors for different cards
        var colors = new[]
        {
            SixLabors.ImageSharp.Color.LightBlue,
            SixLabors.ImageSharp.Color.LightGreen,
            SixLabors.ImageSharp.Color.LightCoral,
            SixLabors.ImageSharp.Color.LightYellow,
            SixLabors.ImageSharp.Color.LightPink
        };
    
        var colorIndex = Math.Abs(cardName.GetHashCode()) % colors.Length;
        image.Mutate(x => x.BackgroundColor(colors[colorIndex]));
    
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private byte[] CreatePlaceholderImage()
    {
        // Create a simple placeholder image (similar to your MpcFillService pattern)
        const int width = 1500;
        const int height = 2100;
        
        using var image = new SixLabors.ImageSharp.Image<Rgba32>(width, height);
        image.Mutate(x => x.BackgroundColor(SixLabors.ImageSharp.Color.LightGray));
        
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
    
    public async Task<List<CardSearchResult>> SearchAsyncTest(string query, SearchFilters filters, IProgress<SearchProgress>? progress = null)
    {
        progress?.Report(new SearchProgress { Status = "Searching...", CurrentStep = 1, TotalSteps = 2 });
        await Task.Delay(1000); // Simulate API call
    
        var results = new List<CardSearchResult>
        {
            new() { Id = "1", Name = $"Lightning Bolt (search: {query})", Rarity = "Common", SetName = "Test Set", ImageUrl = "https://example.com/lightning-bolt.png" },
            new() { Id = "2", Name = $"Black Lotus (search: {query})", Rarity = "Rare", SetName = "Test Set", ImageUrl = "https://example.com/black-lotus.png" },
            new() { Id = "3", Name = $"Ancestral Recall (search: {query})", Rarity = "Rare", SetName = "Test Set" , ImageUrl = "https://example.com/ancestral-recall.png" }
        };
    
        progress?.Report(new SearchProgress { Status = "Complete", CurrentStep = 2, TotalSteps = 2 });
        return results;
    }

   

    
}