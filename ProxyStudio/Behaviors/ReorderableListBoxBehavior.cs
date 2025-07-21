using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ProxyStudio.Models;
using System.Windows;

namespace ProxyStudio.Behaviors
{
    public static class ReorderableListBoxBehavior
    {
        private const string CardDataFormat = "ProxyStudio.Models.Card";

        private static Point _startPoint;
        private static Card? _draggedItem;
        private static ListBoxItem? _draggedItemContainer;
        private static DragAdorner? _dragAdorner;

        public static readonly AttachedProperty<bool> IsReorderEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "IsReorderEnabled", typeof(ReorderableListBoxBehavior), false);

        static ReorderableListBoxBehavior()
        {
            IsReorderEnabledProperty.Changed.Subscribe(args =>
            {
                if (args.Sender is ListBox listBox && args.NewValue.HasValue)
                {
                    var isEnabled = args.NewValue.Value;
                    OnIsReorderEnabledChanged(listBox, isEnabled);
                }
            });
        }

        public static void SetIsReorderEnabled(AvaloniaObject element, bool value) =>
            element.SetValue(IsReorderEnabledProperty, value);

        public static bool GetIsReorderEnabled(AvaloniaObject element) =>
            element.GetValue(IsReorderEnabledProperty);

        private static void OnIsReorderEnabledChanged(ListBox listBox, bool enabled)
        {
            if (enabled)
            {
                listBox.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
                listBox.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
                listBox.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
                listBox.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble);
                listBox.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble);
            }
            else
            {
                listBox.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
                listBox.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
                listBox.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
                listBox.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
                listBox.RemoveHandler(DragDrop.DropEvent, OnDrop);
            }
        }

        private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            Helpers.DebugHelper.WriteDebug("Pointer pressed.");
            if (sender is not ListBox listBox) return;

            _startPoint = e.GetPosition(listBox);

            var hit = listBox.InputHitTest(_startPoint);
            var itemContainer = FindAncestorOfType<ListBoxItem>(hit as Visual);

            if (itemContainer?.DataContext is Card card)
            {
                _draggedItem = card;
                _draggedItemContainer = itemContainer;
                Helpers.DebugHelper.WriteDebug($"Dragged item set: {card.Name}");
            }
            else
            {
                _draggedItem = null;
                _draggedItemContainer = null;
                Helpers.DebugHelper.WriteDebug("No card found in item container");
            }
        }

        private static void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_draggedItem == null || sender is not ListBox listBox)
                return;

            var currentPos = e.GetPosition(listBox);
            var diff = currentPos - _startPoint;

            if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
            {
                // Always remove any existing adorner first
                RemoveDragAdorner();
                
                // Create a fresh drag adorner with current element size
                if (_draggedItemContainer != null)
                {
                    CreateDragAdorner(listBox, _draggedItemContainer, e);
                }

                var dataObject = new DataObject();
                dataObject.Set(CardDataFormat, _draggedItem);

                Helpers.DebugHelper.WriteDebug("Drag started.");

                try
                {
                    // Use the synchronous version for event handlers
                    DragDrop.DoDragDrop(e, dataObject, DragDropEffects.Move);
                    Helpers.DebugHelper.WriteDebug("Drag operation completed.");
                }
                catch (Exception ex)
                {
                    Helpers.DebugHelper.WriteDebug($"Drag operation failed: {ex.Message}");
                }
                finally
                {
                    RemoveDragAdorner();
                }

                _draggedItem = null;
                _draggedItemContainer = null;
            }
        }

        private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            RemoveDragAdorner();
            _draggedItem = null;
            _draggedItemContainer = null;
        }

        private static void OnDragOver(object? sender, DragEventArgs e)
        {
            Helpers.DebugHelper.WriteDebug("Drag over.");
            
            // Update adorner position using the same coordinate system as creation
            if (_dragAdorner != null && sender is ListBox listBox)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (adornerLayer != null)
                {
                    var position = e.GetPosition(adornerLayer);
                    _dragAdorner.UpdatePosition(position);
                }
            }

            if (e.Data.Contains(CardDataFormat))
            {
                Helpers.DebugHelper.WriteDebug("Drag over: Card data found.");
                e.DragEffects = DragDropEffects.Move;
            }
            else
            {
                Helpers.DebugHelper.WriteDebug("Drag over: No card data found.");
                e.DragEffects = DragDropEffects.None;
            }
        }

        private static void OnDrop(object? sender, DragEventArgs e)
        {
            Helpers.DebugHelper.WriteDebug("Drop event triggered.");

            RemoveDragAdorner();

            if (!e.Data.Contains(CardDataFormat))
            {
                Helpers.DebugHelper.WriteDebug("Drop: No card data found.");
                return;
            }

            if (sender is not ListBox listBox)
            {
                Helpers.DebugHelper.WriteDebug("Drop: Sender is not ListBox.");
                return;
            }

            var draggedCard = e.Data.Get(CardDataFormat) as Card;
            if (draggedCard == null)
            {
                Helpers.DebugHelper.WriteDebug("Drop: Dragged card is null.");
                return;
            }

            var position = e.GetPosition(listBox);
            var hit = listBox.InputHitTest(position);
            var targetContainer = FindAncestorOfType<ListBoxItem>(hit as Visual);

            if (targetContainer?.DataContext is not Card targetCard)
            {
                Helpers.DebugHelper.WriteDebug("Drop: No target card found.");
                return;
            }

            if (Equals(draggedCard, targetCard))
            {
                Helpers.DebugHelper.WriteDebug("Drop: Dragged card equals target card.");
                return;
            }

            // Try multiple approaches to get the collection
            IList? items = null;

            // First, try to get it as CardCollection
            if (listBox.ItemsSource is CardCollection cardCollection)
            {
                items = cardCollection;
                Helpers.DebugHelper.WriteDebug("Drop: Got CardCollection.");
            }
            // Then try as IList
            else if (listBox.ItemsSource is IList list)
            {
                items = list;
                Helpers.DebugHelper.WriteDebug("Drop: Got IList.");
            }
            // Finally try as IList<Card>
            else if (listBox.ItemsSource is IList<Card> cardList)
            {
                // Create a wrapper for IList<Card>
                items = new ListWrapper<Card>(cardList);
                Helpers.DebugHelper.WriteDebug("Drop: Got IList<Card>.");
            }

            if (items == null)
            {
                Helpers.DebugHelper.WriteDebug(
                    $"Drop: ItemsSource is not IList. Type: {listBox.ItemsSource?.GetType().Name}");
                return;
            }

            var oldIndex = items.IndexOf(draggedCard);
            var newIndex = items.IndexOf(targetCard);

            Helpers.DebugHelper.WriteDebug($"Drop: Old index: {oldIndex}, New index: {newIndex}");

            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            {
                Helpers.DebugHelper.WriteDebug("Drop: Invalid indices.");
                return;
            }

            try
            {
                items.RemoveAt(oldIndex);
                items.Insert(newIndex, draggedCard);

                listBox.SelectedItem = draggedCard;

                Helpers.DebugHelper.WriteDebug("Drop: Reorder completed successfully.");
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Helpers.DebugHelper.WriteDebug($"Drop: Error during reorder: {ex.Message}");
            }
        }

        private static void CreateDragAdorner(ListBox listBox, ListBoxItem container, PointerEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(listBox);
                if (topLevel?.PlatformImpl == null) return;

                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (adornerLayer == null) return;

                // Create a fresh adorner with current container size
                _dragAdorner = new DragAdorner(container, adornerLayer);
                adornerLayer.Children.Add(_dragAdorner);

                var position = e.GetPosition(adornerLayer);
                _dragAdorner.UpdatePosition(position);
            }
            catch (Exception ex)
            {
                Helpers.DebugHelper.WriteDebug($"Error creating drag adorner: {ex.Message}");
            }
        }

        private static void RemoveDragAdorner()
        {
            if (_dragAdorner?.Parent is Panel parent)
            {
                parent.Children.Remove(_dragAdorner);
            }

            _dragAdorner = null;
        }

        private static T? FindAncestorOfType<T>(Visual? visual) where T : Visual
        {
            while (visual != null)
            {
                if (visual is T t)
                    return t;

                visual = visual.GetVisualParent();
            }

            return null;
        }
    }
}