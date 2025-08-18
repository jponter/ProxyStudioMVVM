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
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ProxyStudio.Models;

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
    
    public Task<Card> ConvertToCard(CardSearchResult result)
    {
        throw new NotImplementedException();
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

    public Task<Card> ConvertToCardAsync(CardSearchResult result)
    {
        throw new NotImplementedException();
    }

    
}