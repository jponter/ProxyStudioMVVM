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
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ProxyStudio.Models;

public class CardSearchResult : INotifyPropertyChanged
{public string Id { get; set; }
    public string Name { get; set; }
    public string ImageUrl { get; set; }
    public string SetName { get; set; }
    public string SetCode { get; set; }
    public string TypeLine { get; set; }
    public string ManaCost { get; set; }
    public string Rarity { get; set; }
    
    private bool _isSelected;
    public bool IsSelected 
    { 
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }
    
    private Bitmap? _imageSource;
    public Bitmap? ImageSource
    {
        get => _imageSource;
        private set { _imageSource = value; OnPropertyChanged(); }
    }
    
    private bool _isImageLoading;
    public bool IsImageLoading
    {
        get => _isImageLoading;
        private set { _isImageLoading = value; OnPropertyChanged(); }
    }
    
    private bool _imageLoadFailed;
    public bool ImageLoadFailed
    {
        get => _imageLoadFailed;
        private set { _imageLoadFailed = value; OnPropertyChanged(); }
    }
    
    public Dictionary<string, object> SourceData { get; set; } = new();
    
    /// <summary>
    /// Loads the card image asynchronously from the ImageUrl
    /// </summary>
    public async Task LoadImageAsync(HttpClient httpClient)
    {
        if (string.IsNullOrEmpty(ImageUrl) || IsImageLoading || ImageSource != null)
            return;

        try
        {
            IsImageLoading = true;
            ImageLoadFailed = false;

            // Use small image URL for search results (faster loading)
            //var smallImageUrl = ImageUrl.Replace("/large/", "/small/").Replace("/normal/", "/small/");

            var normalImageUrl = ImageUrl.Replace("/large/", "/normal/");
            
            var response = await httpClient.GetAsync(normalImageUrl);
            response.EnsureSuccessStatusCode();
            
            var imageData = await response.Content.ReadAsByteArrayAsync();
            
            // Create bitmap from byte array
            using var stream = new MemoryStream(imageData);
            ImageSource = new Bitmap(stream);
        }
        catch (Exception)
        {
            ImageLoadFailed = true;
            // Create a placeholder image
            CreatePlaceholderImage();
        }
        finally
        {
            IsImageLoading = false;
        }
    }
    
    private void CreatePlaceholderImage()
    {
        try
        {
            // Create a simple 200x280 placeholder using SixLabors.ImageSharp
            const int width = 200;
            const int height = 280;
            
            using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
            
            // Set background color based on card name hash
            var hash = Name?.GetHashCode() ?? 0;
            var r = (byte)(Math.Abs(hash) % 100 + 155);
            var g = (byte)(Math.Abs(hash >> 8) % 100 + 155);
            var b = (byte)(Math.Abs(hash >> 16) % 100 + 155);
            
            image.Mutate(x => x.BackgroundColor(SixLabors.ImageSharp.Color.FromRgb(r, g, b)));
            
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            ms.Position = 0;
            
            ImageSource = new Bitmap(ms);
        }
        catch
        {
            // If even placeholder creation fails, leave as null
            ImageSource = null;
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    
}