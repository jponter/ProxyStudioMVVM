using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using ProxyStudio.Helpers;

namespace ProxyStudio.Models;

public partial class Card : ObservableObject
{
    
    private readonly IConfigManager _configManager;
    
    // ── Plain auto-properties ───────────────────────────────────────────────────
    public string Name        { get; set; } = "Default Name";
    public string Id          { get; set; } = "Default ID";
    public string Description { get; set; } = "Default Description";
    public string Query       { get; set; } = "Default Query";

    public int Width  { get; set; } = 83;
    public int Height { get; set; } = 118;

    public bool ImageDownloaded { get; set; } = true;
    

    public IRelayCommand? EditMeCommand { get; set; }

    // ── Observable fields (toolkit generates public properties) ────────────────
    [ObservableProperty] private bool enableBleed = false;
    [ObservableProperty] private byte[]? imageData;

    // ── Lazy-loaded bitmap cache ───────────────────────────────────────────────
    private IImage? _imageCache;
    public IImage? ImageSource
    {
        get
        {
            if (_imageCache is null && ImageData is { Length: > 0 })
            {
                using var ms = new MemoryStream(ImageData);
                _imageCache = new Bitmap(ms);
            }
            return _imageCache;
        }
    }

    /// <summary>
    /// Whenever ImageData changes, purge the cache and notify the UI that
    /// ImageSource has changed.
    /// This partial method is auto-hooked by the toolkit.
    /// </summary>
    partial void OnImageDataChanged(byte[]? value)
    {
        _imageCache = null;
        OnPropertyChanged(nameof(ImageSource));
    }

    // ── Constructors ───────────────────────────────────────────────────────────
    public Card(string name, string id, byte[] image, IConfigManager configManager)
    {
        var config = configManager.Config;
        Name      = name;
        Id        = id;
        ImageData = image; // fires OnImageDataChanged
        enableBleed = config.GlobalBleedEnabled;
    }

    public Card(string name, string id, string query)
    {
        Name  = name;
        Id    = id;
        Query = query;
        ImageData = Array.Empty<byte>();
    }
}
