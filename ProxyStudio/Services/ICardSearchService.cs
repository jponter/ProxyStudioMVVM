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
using System.Threading.Tasks;
using ProxyStudio.Models;

namespace ProxyStudio.Services;

public interface ICardSearchService
{
    string SourceName { get; }
    Task<List<CardSearchResult>> SearchAsync(string query, SearchFilters filters, IProgress<SearchProgress>? progress = null);
    Task<Card> ConvertToCardAsync(CardSearchResult result);
    bool IsAvailable { get; } // For service health checks
}