using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProxyStudio.Models;
using ProxyStudio.ViewModels;

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

        // New property to enable XML file drop support
        public static readonly AttachedProperty<bool> AcceptXmlFilesProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "AcceptXmlFiles", typeof(ReorderableListBoxBehavior), false);

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

            AcceptXmlFilesProperty.Changed.Subscribe(args =>
            {
                if (args.Sender is ListBox listBox && args.NewValue.HasValue)
                {
                    var acceptFiles = args.NewValue.Value;
                    OnAcceptXmlFilesChanged(listBox, acceptFiles);
                }
            });
        }

        public static void SetIsReorderEnabled(AvaloniaObject element, bool value) =>
            element.SetValue(IsReorderEnabledProperty, value);

        public static bool GetIsReorderEnabled(AvaloniaObject element) =>
            element.GetValue(IsReorderEnabledProperty);

        public static void SetAcceptXmlFiles(AvaloniaObject element, bool value) =>
            element.SetValue(AcceptXmlFilesProperty, value);

        public static bool GetAcceptXmlFiles(AvaloniaObject element) =>
            element.GetValue(AcceptXmlFilesProperty);

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

        private static void OnAcceptXmlFilesChanged(ListBox listBox, bool acceptFiles)
        {
            if (acceptFiles)
            {
                // Only add drag/drop handlers if not already added by reorder functionality
                if (!GetIsReorderEnabled(listBox))
                {
                    listBox.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble);
                    listBox.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble);
                }
            }
            else if (!GetIsReorderEnabled(listBox))
            {
                // Only remove if reorder is also disabled
                listBox.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
                listBox.RemoveHandler(DragDrop.DropEvent, OnDrop);
            }
        }

        // Existing pointer event handlers remain unchanged
        private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not ListBox listBox) return;

            _startPoint = e.GetPosition(listBox);
            var hit = listBox.InputHitTest(_startPoint);
            var itemContainer = FindAncestorOfType<ListBoxItem>(hit as Visual);

            if (itemContainer?.DataContext is Card card)
            {
                _draggedItem = card;
                _draggedItemContainer = itemContainer;
            }
            else
            {
                _draggedItem = null;
                _draggedItemContainer = null;
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
                RemoveDragAdorner();
                
                if (_draggedItemContainer != null)
                {
                    CreateDragAdorner(listBox, _draggedItemContainer, e);
                }

                var dataObject = new DataObject();
                dataObject.Set(CardDataFormat, _draggedItem);

                try
                {
                    DragDrop.DoDragDrop(e, dataObject, DragDropEffects.Move);
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

        // Enhanced DragOver handler
        private static void OnDragOver(object? sender, DragEventArgs e)
        {
            if (sender is not ListBox listBox) return;

            // Update adorner position for internal drag operations
            if (_dragAdorner != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (adornerLayer != null)
                {
                    var position = e.GetPosition(adornerLayer);
                    _dragAdorner.UpdatePosition(position);
                }
            }

            // Check for internal card reordering
            if (e.Data.Contains(CardDataFormat))
            {
                e.DragEffects = DragDropEffects.Move;
                return;
            }

            // Check for external file drops
            if (GetAcceptXmlFiles(listBox) && e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                if (files?.OfType<IStorageFile>().Any(f => IsAcceptedFileType(f.Name)) == true)
                {
                    e.DragEffects = DragDropEffects.Copy;
                    return;
                }
            }

            e.DragEffects = DragDropEffects.None;
        }

        // Enhanced Drop handler
        private static void OnDrop(object? sender, DragEventArgs e)
        {
            RemoveDragAdorner();

            if (sender is not ListBox listBox) return;

            // Handle internal card reordering
            if (e.Data.Contains(CardDataFormat))
            {
                HandleCardReorder(listBox, e);
                return;
            }

            // Handle external XML file drops
            if (GetAcceptXmlFiles(listBox) && e.Data.Contains(DataFormats.Files))
            {
                HandleXmlFileDrop(listBox, e);
                return;
            }
        }

        private static void HandleCardReorder(ListBox listBox, DragEventArgs e)
        {
            var draggedCard = e.Data.Get(CardDataFormat) as Card;
            if (draggedCard == null) return;

            var position = e.GetPosition(listBox);
            var hit = listBox.InputHitTest(position);
            var targetContainer = FindAncestorOfType<ListBoxItem>(hit as Visual);

            if (targetContainer?.DataContext is not Card targetCard) return;
            if (Equals(draggedCard, targetCard)) return;

            // Get collection using existing logic
            IList? items = null;

            if (listBox.ItemsSource is CardCollection cardCollection)
            {
                items = cardCollection;
            }
            else if (listBox.ItemsSource is IList list)
            {
                items = list;
            }
            else if (listBox.ItemsSource is IList<Card> cardList)
            {
                items = new ListWrapper<Card>(cardList);
            }

            if (items == null) return;

            var oldIndex = items.IndexOf(draggedCard);
            var newIndex = items.IndexOf(targetCard);

            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;

            try
            {
                items.RemoveAt(oldIndex);
                items.Insert(newIndex, draggedCard);
                listBox.SelectedItem = draggedCard;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Helpers.DebugHelper.WriteDebug($"Error during reorder: {ex.Message}");
            }
        }

        private static async void HandleXmlFileDrop(ListBox listBox, DragEventArgs e)
        {
            try
            {
                var files = e.Data.GetFiles();
                if (files == null) return;

                // Cast IStorageItem to IStorageFile and filter accepted types
                var acceptedFiles = files.OfType<IStorageFile>()
                    .Where(f => IsAcceptedFileType(f.Name))
                    .ToList();
                    
                if (!acceptedFiles.Any()) return;

                // Get the MainViewModel from DataContext
                var mainViewModel = FindMainViewModel(listBox);
                if (mainViewModel == null)
                {
                    Helpers.DebugHelper.WriteDebug("Could not find MainViewModel to handle file drop");
                    return;
                }

                // Process each file by type
                foreach (var file in acceptedFiles)
                {
                    try
                    {
                        if (file.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        {
                            using var stream = await file.OpenReadAsync();
                            using var reader = new StreamReader(stream);
                            var xmlContent = await reader.ReadToEndAsync();
                            
                            await mainViewModel.ProcessXmlFileAsync(xmlContent, file.Name);
                        }
                        else if (IsImageFile(file.Name))
                        {
                            using var stream = await file.OpenReadAsync();
                            using var memoryStream = new MemoryStream();
                            await stream.CopyToAsync(memoryStream);
                            var imageData = memoryStream.ToArray();
                            
                            await mainViewModel.ProcessImageFileAsync(imageData, file.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helpers.DebugHelper.WriteDebug($"Error processing file {file.Name}: {ex.Message}");
                    }
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Helpers.DebugHelper.WriteDebug($"Error in HandleXmlFileDrop: {ex.Message}");
            }
        }

        private static bool IsAcceptedFileType(string fileName)
        {
            return fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || IsImageFile(fileName);
        }

        private static bool IsImageFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png";
        }

        private static MainViewModel? FindMainViewModel(ListBox listBox)
        {
            // Walk up the visual tree to find MainViewModel
            var current = listBox as Visual;
            while (current != null)
            {
                if (current is Control control && control.DataContext is MainViewModel vm)
                {
                    return vm;
                }
                current = current.GetVisualParent();
            }
            return null;
        }

        // Existing helper methods remain unchanged
        private static void CreateDragAdorner(ListBox listBox, ListBoxItem container, PointerEventArgs e)
        {
            try
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (adornerLayer == null) return;

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
                if (visual is T t) return t;
                visual = visual.GetVisualParent();
            }
            return null;
        }
    }
}