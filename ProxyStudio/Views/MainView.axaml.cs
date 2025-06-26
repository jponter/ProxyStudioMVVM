using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using ProxyStudio.ViewModels;

namespace ProxyStudio;

public partial class MainView : Window
{
    
   
    public MainView()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }


    
}