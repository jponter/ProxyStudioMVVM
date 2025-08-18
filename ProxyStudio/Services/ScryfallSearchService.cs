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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;



namespace ProxyStudio.Services;

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
    //[JsonPropertyName("card_faces")] public ScryfallCardFace[] CardFaces { get; set; } // For double-faced cards
}

public class ScryfallImageUris
{
    [JsonPropertyName("small")] public string Small { get; set; }
    [JsonPropertyName("normal")] public string Normal { get; set; }
    [JsonPropertyName("large")] public string Large { get; set; }
    [JsonPropertyName("art_crop")] public string ArtCrop { get; set; }
}

public class ScryfallSearchService : ICardSearchService
{
    public string SourceName { get; } = "Scryfall";
    public bool IsAvailable { get; } = true;
    
    private readonly HttpClient _httpClient;
    private readonly IConfigManager _configManager;
    private readonly ILogger<ScryfallSearchService> _logger;
    private readonly IErrorHandlingService _errorHandler;
    private readonly string _cacheFolder;
    
    
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
        
        Directory.CreateDirectory(_cacheFolder);
    }
    
    
    public async Task<Card> ConvertToCardAsync(CardSearchResult result)
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

    
    private async Task<byte[]> GetCardImageDataAsync(CardSearchResult result)
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
        // Create a simple test image without text for now
        const int width = 1500;   // 600 DPI base resolution
        const int height = 2100;
    
        using var image = new SixLabors.ImageSharp.Image<Rgba32>(width, height);
    
        // Set background color based on card name hash (different colors for different cards)
        var hash = cardName.GetHashCode();
        var r = (byte)(Math.Abs(hash) % 100 + 155);  // Light colors
        var g = (byte)(Math.Abs(hash >> 8) % 100 + 155);
        var b = (byte)(Math.Abs(hash >> 16) % 100 + 155);
    
        image.Mutate(x => x.BackgroundColor(SixLabors.ImageSharp.Color.FromRgb(r, g, b)));
    
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
    
    public async Task<List<CardSearchResult>> SearchAsync(string query, SearchFilters filters, IProgress<SearchProgress>? progress = null)
    {
        progress?.Report(new SearchProgress { Status = "Searching...", CurrentStep = 1, TotalSteps = 2 });
        await Task.Delay(1000); // Simulate API call
    
        var results = new List<CardSearchResult>
        {
            new() { Id = "1", Name = $"Lightning Bolt (search: {query})", Rarity = "Common", SetName = "Test Set" },
            new() { Id = "2", Name = $"Black Lotus (search: {query})", Rarity = "Rare", SetName = "Test Set" },
            new() { Id = "3", Name = $"Ancestral Recall (search: {query})", Rarity = "Rare", SetName = "Test Set" }
        };
    
        progress?.Report(new SearchProgress { Status = "Complete", CurrentStep = 2, TotalSteps = 2 });
        return results;
    }

   

    
}