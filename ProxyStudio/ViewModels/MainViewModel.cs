using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProxyStudio.Models;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ProxyStudio.ViewModels;

public partial class MainViewModel : ViewModelBase
{

    public CardCollection Cards { get; } = new();
  
    [ObservableProperty]
   private Card? selectedCard;




[RelayCommand(CanExecute = nameof(CanAddTestCards))]
private void AddTestCardsRelay()
{
    AddTestCards();
    OnPropertyChanged(nameof(Cards));
}

[RelayCommand]
public void EditCard(Card card)
{
    // open an edit dialog, navigate, etc.
    // e.g. DialogService.ShowEditCard(card);
}

private bool CanAddTestCards() => true;

private void AddTestCards()
    {
        #region Add some default cards to the collection
        //reset the cards
        Cards.RemoveAllCards(); // Clear the existing cards in the collection



        //Helper.WriteDebug("Loading images...");
        // Load images using SixLabors.ImageSharp
        SixLabors.ImageSharp.Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/preacher.jpg");
        SixLabors.ImageSharp.Image<Rgba32> image2 = SixLabors.ImageSharp.Image.Load<Rgba32>("Resources/vampire.jpg");
       

        //change the image to 2.5 x 3.5 inches at 300 DPI
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(750, 1050), // 2.5 inches * 300 DPI = 750 pixels, 3.5 inches * 300 DPI = 1050 pixels
            //Mode = ResizeMode
        }));

        //var t = image.Metadata.HorizontalResolution ; // Set horizontal resolution to 300 DPI

        image2.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(750, 1050), // 2.5 inches * 300 DPI = 750 pixels, 3.5 inches * 300 DPI = 1050 pixels
            //Mode = ResizeMode
        }));

        //image2.Metadata.HorizontalResolution = 300; // Set horizontal resolution to 300 DPI

        // Fix for CS0176: Qualify the static method call with the type name instead of using an instance reference.
        byte[] buffer = Helpers.ImageSharpToWPFConverter.ImageToByteArray(image);


        byte[]? buffer2 = Helpers.ImageSharpToWPFConverter.ImageToByteArray(image2);


        Cards.AddCard(new Card("Preacher of the Schism", "12345", buffer));
        Cards.AddCard(new Card("Vampire Token", "563726", buffer2));
        Cards.AddCard(new Card("Preacher of the Schism", "12345", buffer));
        Cards.AddCard(new Card("Vampire Token", "563726", buffer2));
        Cards.AddCard(new Card("Preacher of the Schism", "12345", buffer));
        Cards.AddCard(new Card("Vampire Token", "563726", buffer2));
        Cards.AddCard(new Card("Preacher of the Schism", "12345", buffer));
        Cards.AddCard(new Card("Vampire Token", "563726", buffer2));
        Cards.AddCard(new Card("Preacher of the Schism", "12345", buffer));
        Cards.AddCard(new Card("Vampire Token", "563726", buffer2));
        Cards.AddCard(new Card("Preacher of the Schism", "12345", buffer));
        Cards.AddCard(new Card("Vampire Token", "563726", buffer2));
        Cards.AddCard(new Card("Preacher of the Schism", "12345", buffer));
        Cards.AddCard(new Card("Vampire Token", "563726", buffer2));
        Cards.AddCard(new Card("Preacher of the Schism", "12345", buffer));
        Cards.AddCard(new Card("Vampire Token", "563726", buffer2));
        #endregion
    }
    
}