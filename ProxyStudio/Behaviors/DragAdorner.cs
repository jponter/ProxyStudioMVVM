using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ProxyStudio.Behaviors
{
    /// <summary>
    /// Custom drag adorner that displays a scaled-down visual representation of the dragged item
    /// </summary>
    public partial class DragAdorner : ContentControl
    {
        private readonly Visual _originalElement;
        private Point _offset;

        public DragAdorner(Visual originalElement, Visual adornerLayer)
        {
            _originalElement = originalElement;
            IsHitTestVisible = false;
            
            // Set up the visual
            if (originalElement is Control control)
            {
                // Scale down the adorner by 50%
                Width = control.Bounds.Width * 0.5;
                Height = control.Bounds.Height * 0.5;
                
                // Create a semi-transparent version
                Opacity = 0.7;
                
                // Try to copy the background
                if (control is TemplatedControl templatedControl && templatedControl.Background != null)
                {
                    Background = templatedControl.Background;
                }
                
                // Find the image in the original element
                var image = FindDescendantOfType<Image>(control);
                if (image?.Source != null)
                {
                    var adornerImage = new Image
                    {
                        Source = image.Source,
                        Stretch = image.Stretch,
                        Width = image.Width * 0.5,  // Scale down image by 50%
                        Height = image.Height * 0.5  // Scale down image by 50%
                    };
                    
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Colors.White, 0.9),
                        BorderBrush = new SolidColorBrush(Colors.Gray),
                        BorderThickness = new Thickness(1),
                        Child = adornerImage,
                        CornerRadius = new CornerRadius(4)
                    };
                    
                    Content = border;
                }
            }
        }

        /// <summary>
        /// Updates the position of the adorner to follow the mouse cursor
        /// </summary>
        /// <param name="position">The current mouse position</param>
        public void UpdatePosition(Point position)
        {
            // Position the adorner closer to the mouse pointer
            // Offset it slightly down and to the right so it doesn't obscure the cursor
            Canvas.SetLeft(this, position.X + 10);
            Canvas.SetTop(this, position.Y + 10);
        }

        /// <summary>
        /// Recursively searches for a descendant of the specified type in the visual tree
        /// </summary>
        /// <typeparam name="T">The type of visual element to find</typeparam>
        /// <param name="visual">The visual element to search from</param>
        /// <returns>The first descendant of type T, or null if not found</returns>
        private static T? FindDescendantOfType<T>(Visual? visual) where T : Visual
        {
            if (visual == null) return null;

            if (visual is T t)
                return t;

            foreach (var child in visual.GetVisualChildren())
            {
                var result = FindDescendantOfType<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}