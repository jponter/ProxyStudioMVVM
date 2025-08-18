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

namespace ProxyStudio.Models;

public class CardSearchResult : ObservableCollection<CardSearchResult>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ImageUrl { get; set; }
    public string SetName { get; set; }
    public string SetCode { get; set; }
    public string TypeLine { get; set; }
    public string ManaCost { get; set; }
    public string Rarity { get; set; }
    public bool IsSelected { get; set; }
    // Source-specific data can be stored in a Dictionary<string, object>
    public Dictionary<string, object> SourceData { get; set; } = new();
}