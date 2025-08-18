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
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using ProxyStudio.Services;
using System.Linq;

namespace ProxyStudio.ViewModels;

public partial class CardSearchViewModel: ViewModelBase
{
    private readonly ICardSearchService _searchService;
    private readonly CardCollection _cardCollection; // Shared with MainViewModel
    private readonly IErrorHandlingService _errorHandler;
    private readonly ILogger<CardSearchViewModel> _logger;
    private readonly IConfigManager _configManager;
    private readonly IRelayCommand _editCardCommand; // Command to edit cards, passed from MainViewModel
    
    // Observable properties for UI binding
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private ObservableCollection<CardSearchResult> _searchResults = new();
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _searchStatus = "";
    [ObservableProperty] private string _rarityFilter = "";
    [ObservableProperty] private string _typeFilter = "";
    [ObservableProperty] private string _colorFilter = "";
    [ObservableProperty] private string _setFilter = "";
    [ObservableProperty] private int _selectedCount = 0;
    [ObservableProperty] private bool _isSelected = false;
    [ObservableProperty] private bool _hasSelection = false;
    [ObservableProperty] private bool _hasResults = false;

    [ObservableProperty] private ObservableCollection<List<string>> _sortOption = new();
    [ObservableProperty] private CardSearchResult? _selectedSearchResult = null;
    

    
    
    public CardSearchViewModel( IConfigManager config, 
        CardCollection cards, 
        ICardSearchService cardSearchService, 
        ILogger<CardSearchViewModel> logger, 
        IErrorHandlingService errorHandler,
        IRelayCommand editCardCommand)
    {
        _searchService = cardSearchService;
        _cardCollection = cards;
        _errorHandler = errorHandler;
        _logger = logger;
        _configManager = config;
        _editCardCommand = editCardCommand;

        // Initialize sort options
        SortOption.Add(new List<string> { "Name", "Set", "Rarity", "Type" });
        
       
    }

  
    
    
    // Commands following your RelayCommand pattern
    [RelayCommand] private async Task SearchAsync()
    {
       try
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchStatus = "Please enter a search query";
            return;
        }

        // Set loading state
        IsSearching = true;
        SearchStatus = "Searching...";
        SearchResults.Clear();
        HasResults = false;
        HasSelection = false;
        SelectedCount = 0;

        _logger.LogInformation("Starting card search for query: {Query}", SearchQuery);

        // Create progress reporter
        var progress = new Progress<SearchProgress>(progressInfo =>
        {
            // Update UI on main thread
            SearchStatus = progressInfo.Status;
            _logger.LogDebug("Search progress: {Status} ({Percentage:F0}%)", 
                progressInfo.Status, progressInfo.PercentageComplete);
        });

        // Build search filters from UI
        var filters = new SearchFilters
        {
            SetCode = SetFilter?.Trim() ?? "",
            Colors = ColorFilter?.Trim() ?? "",
            Type = TypeFilter?.Trim() ?? "",
            Rarity = RarityFilter?.Trim() ?? ""
        };

        // Perform the search
        var results = await _searchService.SearchAsync(SearchQuery.Trim(), filters, progress);

        // Update UI with results
        SearchResults.Clear();
        foreach (var result in results)
        {
            SearchResults.Add(result);
        }

        // Update status and flags
        HasResults = SearchResults.Count > 0;
        SearchStatus = HasResults 
            ? $"Found {SearchResults.Count} cards" 
            : "No cards found";

        _logger.LogInformation("Search completed: {ResultCount} cards found", SearchResults.Count);
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "Network error during card search");
        SearchStatus = "Network error - check your connection";
        await _errorHandler.ShowErrorAsync(
            "Search Failed", 
            "Unable to connect to card database. Please check your internet connection.", 
            ErrorSeverity.Warning, ex);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during card search");
        SearchStatus = "Search failed";
        await _errorHandler.HandleExceptionAsync(ex, 
            "An error occurred while searching for cards. Please try again.", 
            "CardSearch.SearchAsync");
    }
    finally
    {
        // Always cleanup loading state
        IsSearching = false;
        _logger.LogDebug("Search operation completed, IsSearching set to false");
    }
    }

    [RelayCommand] private async Task AddSelectedToCollectionAsync()
    {
        try
    {
        var selectedResults = SearchResults.Where(r => r.IsSelected).ToList();
        
        if (!selectedResults.Any())
        {
            SearchStatus = "No cards selected";
            return;
        }

        IsSearching = true; // Reuse loading state
        SearchStatus = $"Adding {selectedResults.Count} cards to collection...";
        
        _logger.LogInformation("Adding {Count} selected cards to collection", selectedResults.Count);

        var addedCards = new List<Card>();
        
        foreach (var result in selectedResults)
        {
            try
            {
                // Convert search result to full Card object
                var card = await _searchService.ConvertToCardAsync(result);
                
                // Add EditMeCommand (like in your MPC Fill service)
                card.EditMeCommand = _editCardCommand; // You'll need to pass this from MainViewModel
                
                // Add to collection
                _cardCollection.AddCard(card);
                addedCards.Add(card);
                
                _logger.LogDebug("Added card to collection: {CardName}", card.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add card {CardName} to collection", result.Name);
                // Continue with other cards, don't fail entire operation
            }
        }

        // Clear selection of successfully added cards
        foreach (var result in selectedResults.Where(r => addedCards.Any(c => c.Id == r.Id)))
        {
            result.IsSelected = false;
        }
        
        UpdateSelectionCount();
        SearchStatus = $"Successfully added {addedCards.Count} cards to collection";
        
        _logger.LogInformation("Successfully added {Count} cards to collection", addedCards.Count);
        
        // Optional: Show success message
        if (addedCards.Count > 0)
        {
            await _errorHandler.ShowErrorAsync(
                "Cards Added", 
                $"Successfully added {addedCards.Count} card(s) to your collection.", 
                ErrorSeverity.Information);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error adding selected cards to collection");
        SearchStatus = "Failed to add cards to collection";
        await _errorHandler.HandleExceptionAsync(ex, 
            "An error occurred while adding cards to your collection.", 
            "CardSearch.AddSelectedToCollection");
    }
    finally
    {
        IsSearching = false;
    }
    }

    [RelayCommand]
    private async Task QuickSearch()
    {
        throw new System.NotImplementedException();
    }
    
    [RelayCommand]
    private async Task SelectAllResults()
    {
        foreach (var result in SearchResults)
        {
            result.IsSelected = true;
        }
        UpdateSelectionCount();
        _logger.LogDebug("Selected all {Count} search results", SearchResults.Count);
    }
    
    [RelayCommand]
    private async Task AddSingleCard()
    {
        throw new System.NotImplementedException();
    }
    
    [RelayCommand]
    private async Task ClearSelection()
    {
        foreach (var result in SearchResults)
        {
            result.IsSelected = false;
        }
        UpdateSelectionCount();
        _logger.LogDebug("Cleared all selections");
    }
    
    // Helper method to update selection counters
    private void UpdateSelectionCount()
    {
        SelectedCount = SearchResults.Count(r => r.IsSelected);
        HasSelection = SelectedCount > 0;
        _logger.LogDebug("Selection updated: {SelectedCount} of {TotalCount} cards selected", 
            SelectedCount, SearchResults.Count);
    }
    
    // Call this when individual checkboxes change
    [RelayCommand]
    public void UpdateSelection()
    {
        UpdateSelectionCount();
    }
}