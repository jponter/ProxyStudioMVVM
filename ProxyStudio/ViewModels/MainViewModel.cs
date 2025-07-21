using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Dialogs;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using ProxyStudio.Services;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ProxyStudio.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    //di configmanager interface
    private readonly IConfigManager _configManager;
    private readonly IPdfGenerationService _pdfService;

    //public ObservableCollection<Card> Cards { get; } = new();
    public CardCollection Cards { get; private set; } = new();

    // Print ViewModel
    [ObservableProperty] private PrintViewModel? _printViewModel;

    // configuration properties
    [ObservableProperty] private bool _globalBleedEnabled;

    partial void OnGlobalBleedEnabledChanged(bool value)
    {
        _configManager.Config.GlobalBleedEnabled = value;
        DebugHelper.WriteDebug("Global bleed enabled changed to " + value);
    }

    //selected card
    [ObservableProperty] private Card? _selectedCard;

    // check to see if we are busy before adding cards
    private bool CanAddTestCards()
    {
        return !IsBusy;
    }

    [ObservableProperty] private bool _isBusy;

    //constructor with design time check as some of the DI stuff breaks it
    public MainViewModel(IConfigManager configManager, IPdfGenerationService pdfService)
    {
        _configManager = configManager;
        _pdfService = pdfService;

        if (Design.IsDesignMode)
        {
            // Only set up minimal data for design-time
            // Don't load config, don't load images, etc.
            Cards.AddRange(AddTestCards());
            // Create a simple PrintViewModel for design time
            PrintViewModel = new PrintViewModel(_pdfService, _configManager, Cards);
            return;
        }

#if DEBUG
        DebugHelper.WriteDebug("Creating new MainViewModel.");
#endif

#if DEBUG
        DebugHelper.WriteDebug("Set the _configManager in Mainviewmodel");
        DebugHelper.WriteDebug($"Before load: GlobalBleedEnabled = {GlobalBleedEnabled}");
#endif

        GlobalBleedEnabled = _configManager.Config.GlobalBleedEnabled;
        // Cards.AddRange(AddTestCards());
        // PrintViewModel?.RefreshCardInfo();
        
        DebugHelper.WriteDebug($"Cards loaded: {Cards.Count} cards");
        
        // Initialize PrintViewModel
        PrintViewModel = new PrintViewModel(_pdfService, _configManager, Cards);
        
        DebugHelper.WriteDebug($"PrintViewModel initialized with {Cards.Count} cards");
        DebugHelper.WriteDebug($"PrintViewModel.CardsPerRow = {PrintViewModel.CardsPerRow}");
        DebugHelper.WriteDebug($"PrintViewModel.ShowCuttingLines = {PrintViewModel.ShowCuttingLines}");
    }

    // Constructor for design-time support
    public MainViewModel(IConfigManager configManager) : this(configManager, new DesignTimePdfService())
    {
        // Design-time constructor that creates a mock PDF service
    }

    // Simple design-time PDF service
    // Simple design-time PDF service
    private class DesignTimePdfService : IPdfGenerationService
    {
        public Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options, IProgress<PdfGenerationProgress>? progress = null)
        {
            // Simulate progress for design-time
            if (progress != null)
            {
                var progressInfo = new PdfGenerationProgress
                {
                    CurrentStep = 1,
                    TotalSteps = 1,
                    CurrentOperation = "Design-time generation",
                    //PercentageComplete = 100
                };
                progress.Report(progressInfo);
            }
        
            return Task.FromResult(new byte[0]);
        }

        public Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return Task.FromResult<Bitmap>(null!);
        }
    }

    // Update the AddTestCardsRelayAsync method in MainViewModel:

    [RelayCommand(CanExecute = nameof(CanAddTestCards))]
    private async Task AddTestCardsRelayAsync()
    {
        IsBusy = true;

        // do the heavy lifting off the UI thread
        var newCards = await Task.Run(() => { return AddTestCards(); });

        Cards.AddRange(newCards);
    
        // IMPORTANT: Refresh PrintViewModel after adding cards
        PrintViewModel?.RefreshCardInfo();
        //this will ensure the PrintViewModel has the latest cards
        PrintViewModel?.GeneratePreviewCommand.Execute(null);
    
        DebugHelper.WriteDebug($"Added {newCards.Count} cards, total now: {Cards.Count}");

        IsBusy = false;
    }

    [RelayCommand]
    public void EditCard(Card card)
    {
        // open an edit dialog, navigate, etc.
        // e.g. DialogService.ShowEditCard(card);
        SelectedCard = card;
    }

    // Fix 1: Update AddTestCards() in MainViewModel.cs to store high-resolution images

private List<Card> AddTestCards()
{
    List<Card> cards = new();

    DebugHelper.WriteDebug("Loading high-resolution images for dynamic DPI scaling...");
    
    // Load images at their native resolution
    var image = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/preacher.jpg");
    var image2 = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/vampire.jpg");

    DebugHelper.WriteDebug($"Loaded images: preacher={image.Width}x{image.Height}, vampire={image2.Width}x{image2.Height}");

    // IMPORTANT: Store images at a high base resolution instead of scaling at startup
    // We'll use 600 DPI (1500x2100) as our "source" resolution that can be scaled down
    const int baseDpi = 600;
    var baseWidth = (int)(2.5 * baseDpi);   // 1500 pixels
    var baseHeight = (int)(3.5 * baseDpi);  // 2100 pixels

    // Resize to high base resolution for maximum quality
    image.Mutate(x => x.Resize(new ResizeOptions
    {
        Size = new SixLabors.ImageSharp.Size(baseWidth, baseHeight),
        Mode = ResizeMode.Stretch,
        Sampler = KnownResamplers.Lanczos3 // High-quality resampling
    }));

    image2.Mutate(x => x.Resize(new ResizeOptions
    {
        Size = new SixLabors.ImageSharp.Size(baseWidth, baseHeight),
        Mode = ResizeMode.Stretch,
        Sampler = KnownResamplers.Lanczos3
    }));

    // Store as high-quality PNG to preserve all detail for later scaling
    var pngEncoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
    {
        CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression,
        ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
    };

    using var ms1 = new MemoryStream();
    image.Save(ms1, pngEncoder);
    var buffer = ms1.ToArray();

    using var ms2 = new MemoryStream();
    image2.Save(ms2, pngEncoder);
    var buffer2 = ms2.ToArray();

    DebugHelper.WriteDebug($"Created high-resolution base images: {baseWidth}x{baseHeight} ({baseDpi} DPI base)");
    DebugHelper.WriteDebug($"Image sizes: preacher={buffer.Length} bytes, vampire={buffer2.Length} bytes");

    for (var i = 0; i < 2; i++)
    {
        cards.Add(new Card("Preacher of the Schism", "12345", buffer, _configManager));
        cards.Add(new Card("Vampire Token", "563726", buffer2, _configManager));
        cards.Add(new Card("Preacher of the Schism", "12345", buffer, _configManager));
        cards.Add(new Card("Vampire Token", "563726", buffer2, _configManager));
    }

    foreach (var card in cards) card.EditMeCommand = EditCardCommand;
    
    DebugHelper.WriteDebug($"Created {cards.Count} cards with high-resolution images ready for dynamic DPI scaling");
    return cards;
}
}