using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Dialogs;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProxyStudio.Models;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ProxyStudio.ViewModels;

public partial class MainViewModel : ViewModelBase
{

    public CardCollection Cards { get; private set; } = new();
    
  
  
  [ObservableProperty]
   private Card? selectedCard;

   
  
   private bool CanAddTestCards() => !IsBusy;
   
[ObservableProperty]
private bool isBusy;


//design time constructor
    public MainViewModel()
    {
        
        Cards.AddRange(AddTestCards());
        
       
    }
    
    
    [RelayCommand]
    private void MoveCardRight(object sender)
    {
        // open an add card dialog, navigate, etc.
        // e.g. DialogService.ShowAddCard();
        if (sender is Card card)
        {
            //Cards.RemoveCard(card);
        }
    }


[RelayCommand(CanExecute = nameof(CanAddTestCards))]
private async Task AddTestCardsRelayAsync()
{
    IsBusy = true;
    
    // do the heavy lifting off the UI thread
    var newCards = await Task.Run( () =>
    {
        return AddTestCards();
    });
    
    // marshall back to the UI thread for changing the collection

    // await Dispatcher.UIThread.InvokeAsync(() =>
    // {
    //     foreach (var card in newCards)
    //         Cards.AddCard(card);
    //
    // });
    
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

        //really stress the program
        for (int i = 0; i < 2; i++)
        {
            cards.Add(new Card("Preacher of the Schism", "12345", buffer));
            cards.Add(new Card("Vampire Token", "563726", buffer2));
            cards.Add(new Card("Preacher of the Schism", "12345", buffer));
            cards.Add(new Card("Vampire Token", "563726", buffer2));
        }


        //set the command of each card directlu
        foreach (var card in cards)
        {
            card.EditMeCommand = EditCardCommand;
        }
        
        #endregion
        return cards;
    }



}