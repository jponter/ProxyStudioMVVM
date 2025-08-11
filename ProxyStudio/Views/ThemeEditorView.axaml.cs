using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ProxyStudio.Views;

public partial class ThemeEditorView : UserControl
{
    public ThemeEditorView()
    {
        InitializeComponent();
    }

    private void OnButtonPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Button button && Application.Current?.Resources != null)
        {
            var hasHoverBrush = Application.Current.Resources.TryGetValue("PrimaryHoverBrush", out var hoverBrush);
            System.Diagnostics.Debug.WriteLine($"PrimaryHoverBrush exists: {hasHoverBrush}, Value: {hoverBrush}");
        }
    }
    
    private void TestButton_OnPointerEntered(object sender, PointerEventArgs e)
    {
        if (sender is Button button)
        {
            button.Background = Brushes.Red; // Force red on hover
            System.Diagnostics.Debug.WriteLine("Manual hover applied");
        }
    }

    private void TestButton_OnPointerExited(object sender, PointerEventArgs e)
    {
        if (sender is Button button)
        {
            button.Background = Brushes.Blue; // Back to blue
            System.Diagnostics.Debug.WriteLine("Manual hover removed");
        }
    }
}