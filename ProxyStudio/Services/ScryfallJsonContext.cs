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

using System.Text.Json.Serialization;

namespace ProxyStudio.Services;

/// <summary>
/// Source-generated JSON context for Scryfall API responses
/// This is the recommended approach for .NET 9 to avoid reflection-based serialization
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ScryfallSearchResponse))]
[JsonSerializable(typeof(ScryfallCardResponse))]
[JsonSerializable(typeof(ScryfallImageUris))]
[JsonSerializable(typeof(ScryfallCardFace))]
[JsonSerializable(typeof(ScryfallCardResponse[]))]
public partial class ScryfallJsonContext : JsonSerializerContext
{
    // This class is intentionally left empty.
    // It serves as a context for JSON serialization and deserialization
    // of Scryfall API responses using source generation.
}