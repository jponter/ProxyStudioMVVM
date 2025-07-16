// ViewModels/DesignTimeMainViewModel.cs
using System.Collections.ObjectModel;
using System.Linq;
using ProxyStudio.Models;

namespace ProxyStudio.ViewModels;

public class DesignTimeMainViewModel
{
    public CardCollection Cards { get; private set; } = new();
    public Card? SelectedCard { get; set; }
    public bool IsBusy { get; set; }

    public DesignTimeMainViewModel()
    {
        
        
        SelectedCard = Cards.FirstOrDefault();
        IsBusy = false;
    }
}