using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

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
}