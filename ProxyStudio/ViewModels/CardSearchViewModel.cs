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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using ProxyStudio.Services;

namespace ProxyStudio.ViewModels;

public partial class CardSearchViewModel: ViewModelBase
{
    private readonly ICardSearchService _searchService;
    private readonly CardCollection _cardCollection; // Shared with MainViewModel
    private readonly IErrorHandlingService _errorHandler;
    private readonly ILogger<CardSearchViewModel> _logger;
    private readonly IConfigManager _configManager;
    
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
    

    
    
    public CardSearchViewModel( IConfigManager config, CardCollection cards, ICardSearchService cardSearchService, ILogger<CardSearchViewModel> logger, IErrorHandlingService errorHandler)
    {
        _searchService = cardSearchService;
        _cardCollection = cards;
        _errorHandler = errorHandler;
        _logger = logger;
        _configManager = config;

        // Initialize sort options
        SortOption.Add(new List<string> { "Name", "Set", "Rarity", "Type" });
        
       
    }

  
    
    
    // Commands following your RelayCommand pattern
    [RelayCommand] private async Task SearchAsync()
    {
       
    }

    [RelayCommand] private async Task AddSelectedToCollectionAsync()
    {
        throw new System.NotImplementedException();
    }

    [RelayCommand]
    private async Task QuickSearch()
    {
        throw new System.NotImplementedException();
    }
    
    [RelayCommand]
    private async Task SelectAllResults()
    {
        throw new System.NotImplementedException();
    }
    
    [RelayCommand]
    private async Task AddSingleCard()
    {
        throw new System.NotImplementedException();
    }
    
    [RelayCommand]
    private async Task ClearSelection()
    {
        throw new System.NotImplementedException();
    }
    
}