/*
ProxyStudio - A cross-platform proxy management application.
Copyright (C) 2025 James Ponter

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
*/

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