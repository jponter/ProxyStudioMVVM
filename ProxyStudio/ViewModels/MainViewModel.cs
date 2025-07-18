using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        Cards.AddRange(AddTestCards());
        
        DebugHelper.WriteDebug($"Cards loaded: {Cards.Count} cards");
        
        // Initialize PrintViewModel
        PrintViewModel = new PrintViewModel(_pdfService, _configManager, Cards);
        
        DebugHelper.WriteDebug($"PrintViewModel initialized with {Cards.Count} cards");
    }

    // Constructor for design-time support
    public MainViewModel(IConfigManager configManager) : this(configManager, new DesignTimePdfService())
    {
        // Design-time constructor that creates a mock PDF service
    }

    // Simple design-time PDF service
    private class DesignTimePdfService : IPdfGenerationService
    {
        public Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return Task.FromResult(new byte[0]);
        }

        public Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return Task.FromResult<Bitmap>(null!);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddTestCards))]
    private async Task AddTestCardsRelayAsync()
    {
        IsBusy = true;

        // do the heavy lifting off the UI thread
        var newCards = await Task.Run(() => { return AddTestCards(); });

        Cards.AddRange(newCards);

        IsBusy = false;
    }

    [RelayCommand]
    public void EditCard(Card card)
    {
        // open an edit dialog, navigate, etc.
        // e.g. DialogService.ShowEditCard(card);
        SelectedCard = card;
    }

    private List<Card> AddTestCards()
    {
        List<Card> cards = new();

        #region Add some default cards to the collection

        //reset the cards

        //Helper.WriteDebug("Loading images...");
        // Load images using SixLabors.ImageSharp
        var image = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/preacher.jpg");
        var image2 = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/vampire.jpg");

        //change the image to 2.5 x 3.5 inches at 300 DPI
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(750,
                1050) // 2.5 inches * 300 DPI = 750 pixels, 3.5 inches * 300 DPI = 1050 pixels
            //Mode = ResizeMode
        }));

        //var t = image.Metadata.HorizontalResolution ; // Set horizontal resolution to 300 DPI

        image2.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(750,
                1050) // 2.5 inches * 300 DPI = 750 pixels, 3.5 inches * 300 DPI = 1050 pixels
            //Mode = ResizeMode
        }));

        //image2.Metadata.HorizontalResolution = 300; // Set horizontal resolution to 300 DPI

        // Fix for CS0176: Qualify the static method call with the type name instead of using an instance reference.
        var buffer = ImageSharpToWPFConverter.ImageToByteArray(image);

        var buffer2 = ImageSharpToWPFConverter.ImageToByteArray(image2);

        //really stress the program
        for (var i = 0; i < 2; i++)
        {
            cards.Add(new Card("Preacher of the Schism", "12345", buffer, _configManager));
            cards.Add(new Card("Vampire Token", "563726", buffer2, _configManager));
            cards.Add(new Card("Preacher of the Schism", "12345", buffer, _configManager));
            cards.Add(new Card("Vampire Token", "563726", buffer2, _configManager));
        }

        //set the command of each card directly
        foreach (var card in cards) card.EditMeCommand = EditCardCommand;

        #endregion

        return cards;
    }
}