// Updated ThemeEditorViewModel.cs - Simplified Theme System
// This replaces the existing complex 39+ color system with 12 core colors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;
using ProxyStudio.Services;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace ProxyStudio.ViewModels;

/// <summary>
/// Represents a color property that can be edited in the theme editor
/// </summary>
public partial class ColorProperty : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _hexValue;
    [ObservableProperty] private string _description;
    [ObservableProperty] private Color _color;

    public SolidColorBrush ColorBrush => new(Color);

    public ColorProperty(string name, string hexValue, string description)
    {
        _name = name;
        _hexValue = hexValue;
        _description = description;
        _color = Color.Parse(hexValue);
    }
    
    partial void OnColorChanged(Color value)
    {
        HexValue = value.ToString();
        OnPropertyChanged(nameof(ColorBrush));
    }

    partial void OnHexValueChanged(string value)
    {
        // Validate hex color format
        if (IsValidHexColor(value))
        {
            Color = Color.Parse(value);
        }
    }

    private static bool IsValidHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || !hex.StartsWith("#") || hex.Length != 7)
            return false;

        return hex.Skip(1).All(c =>
            (c >= '0' && c <= '9') ||
            (c >= 'A' && c <= 'F') ||
            (c >= 'a' && c <= 'f'));
    }
}

/// <summary>
/// Updated ThemeEditorViewModel using simplified 12-color system
/// </summary>
public partial class ThemeEditorViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IConfigManager _configManager;
    private readonly ILogger<ThemeEditorViewModel> _logger;
    private readonly IErrorHandlingService _errorHandler;
    private ThemeType? _originalThemeBeforePreview;
    private Dictionary<string, object>? _originalResources; // Store original resources for restore

    [ObservableProperty] private string _themeName = "My Custom Theme";
    [ObservableProperty] private string _themeDescription = "A custom theme created with ProxyStudio Theme Editor";
    [ObservableProperty] private string _themeAuthor = Environment.UserName;
    [ObservableProperty] private string _themeVersion = "1.0.0";
    [ObservableProperty] private string _statusMessage = "Ready to create theme";
    [ObservableProperty] private bool _canSaveTheme = false;
    [ObservableProperty] private bool _isPreviewActive = false;
    [ObservableProperty] private ThemeDefinition? _selectedBaseTheme;

    // SIMPLIFIED COLOR COLLECTIONS (12 total instead of 39+)
    public ObservableCollection<ColorProperty> FoundationColors { get; } = new();
    public ObservableCollection<ColorProperty> SemanticColors { get; } = new();
    public ObservableCollection<ColorProperty> SurfaceColors { get; } = new();
    public ObservableCollection<ColorProperty> TextColors { get; } = new();

    public IReadOnlyList<ThemeDefinition> BaseThemes => _themeService.AvailableThemes;

    // Commands
    [RelayCommand] private async Task SaveTheme() => await SaveThemeAsync();
    [RelayCommand] private async Task ExportTheme() => await ExportThemeAsync();
    [RelayCommand] private void ResetToBase() => ResetToBaseTheme();
    [RelayCommand] private async Task PreviewTheme() => await PreviewCurrentThemeAsync();
    [RelayCommand] private async Task StopPreview() => await StopPreviewAsync();

    public ThemeEditorViewModel(
        IThemeService themeService,
        IConfigManager configManager,
        ILogger<ThemeEditorViewModel> logger,
        IErrorHandlingService errorHandler)
    {
        _themeService = themeService;
        _configManager = configManager;
        _logger = logger;
        _errorHandler = errorHandler;

        InitializeSimplifiedColors();
        SetupColorChangeHandlers();

        _selectedBaseTheme = BaseThemes.FirstOrDefault(t => t.Type == ThemeType.DarkProfessional);
        if (_selectedBaseTheme != null)
        {
            ResetToBaseTheme();
        }
    }

    private void InitializeSimplifiedColors()
    {
        FoundationColors.Add(new ColorProperty("Primary", "#3498db", "Main brand color for primary actions and highlights"));
        FoundationColors.Add(new ColorProperty("Secondary", "#95a5a6", "Secondary accent color for supporting elements"));
        FoundationColors.Add(new ColorProperty("Surface", "#2c3e50", "Base surface color for backgrounds and cards"));
        FoundationColors.Add(new ColorProperty("Border", "#556983", "Border and separator color"));

        SemanticColors.Add(new ColorProperty("Success", "#27ae60", "Success states, confirmations, and positive actions"));
        SemanticColors.Add(new ColorProperty("Warning", "#f39c12", "Warning states, cautions, and important notices"));
        SemanticColors.Add(new ColorProperty("Error", "#e74c3c", "Error states, failures, and destructive actions"));
        SemanticColors.Add(new ColorProperty("Info", "#3498db", "Informational messages, hints, and neutral notices"));

        SurfaceColors.Add(new ColorProperty("Background", "#2c3e50", "Main application background color"));
        SurfaceColors.Add(new ColorProperty("Surface Elevated", "#34495e", "Elevated surfaces like cards, modals, and panels"));

        TextColors.Add(new ColorProperty("Text Primary", "#ffffff", "Primary text color for headings and important content"));
        TextColors.Add(new ColorProperty("Text Secondary", "#bdc3c7", "Secondary text color for descriptions and labels"));
    }

    private void SetupColorChangeHandlers()
    {
        foreach (var color in FoundationColors.Concat(SemanticColors).Concat(SurfaceColors).Concat(TextColors))
        {
            color.PropertyChanged += async (s, e) => {
                if (e.PropertyName == nameof(ColorProperty.HexValue))
                {
                    CanSaveTheme = true;
                    StatusMessage = "Theme modified - ready to save";

                    if (IsPreviewActive)
                    {
                        await UpdatePreviewAsync();
                    }
                }
            };
        }
    }

    private void ResetToBaseTheme()
    {
        if (SelectedBaseTheme == null) return;

        try
        {
            switch (SelectedBaseTheme.Type)
            {
                case ThemeType.DarkProfessional:
                    SetDarkProfessionalColors();
                    break;
                case ThemeType.LightClassic:
                    SetLightClassicColors();
                    break;
                case ThemeType.DarkRed:
                    SetDarkRedColors();
                    break;
                default:
                    SetDarkProfessionalColors();
                    break;
            }

            StatusMessage = $"Reset to {SelectedBaseTheme.Name} base colors";
            CanSaveTheme = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset to base theme");
            StatusMessage = "Failed to reset to base theme";
        }
    }

    private void SetDarkProfessionalColors()
    {
        SetColorValue(FoundationColors, "Primary", "#3498db");
        SetColorValue(FoundationColors, "Secondary", "#95a5a6");
        SetColorValue(FoundationColors, "Surface", "#2c3e50");
        SetColorValue(FoundationColors, "Border", "#556983");

        SetColorValue(SemanticColors, "Success", "#27ae60");
        SetColorValue(SemanticColors, "Warning", "#f39c12");
        SetColorValue(SemanticColors, "Error", "#e74c3c");
        SetColorValue(SemanticColors, "Info", "#3498db");

        SetColorValue(SurfaceColors, "Background", "#2c3e50");
        SetColorValue(SurfaceColors, "Surface Elevated", "#34495e");

        SetColorValue(TextColors, "Text Primary", "#ffffff");
        SetColorValue(TextColors, "Text Secondary", "#bdc3c7");
    }
    
    private void SetDarkRedColors()
    {
        SetColorValue(FoundationColors, "Primary", "#3498db");
        SetColorValue(FoundationColors, "Secondary", "#ffa5a6");
        SetColorValue(FoundationColors, "Surface", "#550000");
        SetColorValue(FoundationColors, "Border", "#556983");

        SetColorValue(SemanticColors, "Success", "#27ae60");
        SetColorValue(SemanticColors, "Warning", "#f39c12");
        SetColorValue(SemanticColors, "Error", "#e74c3c");
        SetColorValue(SemanticColors, "Info", "#3498db");

        SetColorValue(SurfaceColors, "Background", "#330000");
        SetColorValue(SurfaceColors, "Surface Elevated", "#440000");

        SetColorValue(TextColors, "Text Primary", "#ffffff");
        SetColorValue(TextColors, "Text Secondary", "#bdc3c7");
    }

    private void SetLightClassicColors()
    {
        SetColorValue(FoundationColors, "Primary", "#007acc");
        SetColorValue(FoundationColors, "Secondary", "#6c757d");
        SetColorValue(FoundationColors, "Surface", "#ffffff");
        SetColorValue(FoundationColors, "Border", "#dee2e6");

        SetColorValue(SemanticColors, "Success", "#28a745");
        SetColorValue(SemanticColors, "Warning", "#ffc107");
        SetColorValue(SemanticColors, "Error", "#dc3545");
        SetColorValue(SemanticColors, "Info", "#17a2b8");

        SetColorValue(SurfaceColors, "Background", "#ffffff");
        SetColorValue(SurfaceColors, "Surface Elevated", "#f8f9fa");

        SetColorValue(TextColors, "Text Primary", "#212529");
        SetColorValue(TextColors, "Text Secondary", "#6c757d");
    }

    private void SetColorValue(ObservableCollection<ColorProperty> collection, string name, string hexValue)
    {
        var color = collection.FirstOrDefault(c => c.Name == name);
        if (color != null)
        {
            color.HexValue = hexValue;
        }
    }

    // CORRECT IN-MEMORY PREVIEW METHODS - NO FILE OPERATIONS
    private async Task PreviewCurrentThemeAsync()
    {
        try
        {
            StatusMessage = "Applying preview theme...";

            if (!IsPreviewActive)
            {
                _originalThemeBeforePreview = _themeService.CurrentTheme;
                BackupCurrentResources();
            }

            IsPreviewActive = true;

            // Apply theme colors directly to Application.Resources in memory
            await ApplyThemeInMemory();

            StatusMessage = "Preview active - theme applied temporarily";
            _logger.LogInformation("Theme preview applied: {ThemeName}", ThemeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview theme: {ThemeName}", ThemeName);
            await _errorHandler.HandleExceptionAsync(ex, "Failed to preview theme", "Theme Editor");
            StatusMessage = "Failed to preview theme";
            IsPreviewActive = false;
            _originalThemeBeforePreview = null;
            _originalResources = null;
        }
    }

    private async Task UpdatePreviewAsync()
    {
        try
        {
            if (!IsPreviewActive) return;

            // Update theme colors directly in Application.Resources
            await ApplyThemeInMemory();

            _logger.LogDebug("Preview theme updated with new colors");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update preview theme");
        }
    }

    private async Task StopPreviewAsync()
    {
        try
        {
            if (!IsPreviewActive || _originalResources == null)
            {
                StatusMessage = "No preview is currently active";
                return;
            }

            StatusMessage = "Restoring original theme...";

            // Restore original resources directly in memory - NO FILE LOADING
            await Task.Run(() =>
            {
                try
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            var app = Application.Current;
                            if (app?.Resources == null)
                            {
                                _logger.LogWarning("Application or Resources is null during preview restoration");
                                return;
                            }

                            // Remove all theme-related resources first
                            var keysToRemove = app.Resources.Keys
                                .OfType<string>()
                                .Where(k => k.EndsWith("Brush") || k.EndsWith("Color"))
                                .ToList();

                            foreach (var key in keysToRemove)
                            {
                                try
                                {
                                    app.Resources.Remove(key);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to remove resource key: {Key}", key);
                                }
                            }

                            // Restore original resources if we have them
                            if (_originalResources != null)
                            {
                                foreach (var kvp in _originalResources)
                                {
                                    try
                                    {
                                        if (kvp.Value != null)
                                        {
                                            app.Resources[kvp.Key] = kvp.Value;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to restore resource: {Key}", kvp.Key);
                                    }
                                }
                            }

                            _logger.LogDebug("Restored original theme resources from memory backup");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in UI thread during resource restoration");
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background thread during preview restoration");
                }
            });

            IsPreviewActive = false;
            _originalThemeBeforePreview = null;
            _originalResources = null;
            StatusMessage = "Preview stopped - original theme restored";

            _logger.LogInformation("Theme preview stopped, resources restored from memory");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop theme preview");
            await _errorHandler.HandleExceptionAsync(ex, "Failed to stop preview", "Theme Editor");
            StatusMessage = "Failed to stop preview";
            
            // Ensure we clean up state even if restoration failed
            IsPreviewActive = false;
            _originalThemeBeforePreview = null;
            _originalResources = null;
        }
    }

    /// <summary>
    /// Apply theme colors directly to application resources in memory
    /// </summary>
    // private async Task ApplyThemeInMemory()
    // {
    //     await Task.Run(() =>
    //     {
    //         Dispatcher.UIThread.Post(() =>
    //         {
    //             var app = Application.Current;
    //             if (app?.Resources == null) return;
    //
    //             // Apply new color resources directly to Application.Resources
    //             ApplyColorsToResources(app.Resources, FoundationColors, "Foundation");
    //             ApplyColorsToResources(app.Resources, SemanticColors, "Semantic");
    //             ApplyColorsToResources(app.Resources, SurfaceColors, "Surface");
    //             ApplyColorsToResources(app.Resources, TextColors, "Text");
    //
    //             // Apply derived colors
    //             ApplyDerivedColorsToResources(app.Resources);
    //
    //             _logger.LogDebug("Applied theme colors directly to Application.Resources in memory");
    //         });
    //     });
    // }
    
/// <summary>
/// Updated theme application with the final fix
/// </summary>
private async Task ApplyThemeInMemory()
{
    await Task.Run(() =>
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var app = Application.Current;
                if (app?.Resources == null) 
                {
                    _logger.LogWarning("Application.Current.Resources is null");
                    return;
                }

                _logger.LogDebug("Applying theme colors to Application.Resources...");

                // Create a new resource dictionary for our theme
                var themeDict = new ResourceDictionary();

                // Apply colors to the new dictionary
                ApplyColorsToResources(themeDict, FoundationColors, "Foundation");
                ApplyColorsToResources(themeDict, SemanticColors, "Semantic");
                ApplyColorsToResources(themeDict, SurfaceColors, "Surface");
                ApplyColorsToResources(themeDict, TextColors, "Text");

                // Apply derived colors AFTER base colors are set
                ApplyDerivedColorsToResources(themeDict);

                // Remove existing theme dictionary
                ResourceDictionary? existingThemeDict = null;
                for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                {
                    if (app.Resources.MergedDictionaries[i] is ResourceDictionary dict && 
                        dict.ContainsKey("_ThemeEditorGenerated"))
                    {
                        existingThemeDict = dict;
                        app.Resources.MergedDictionaries.RemoveAt(i);
                        _logger.LogDebug("Removed existing theme dictionary at index {Index}", i);
                        break;
                    }
                }

                // Add marker and add to merged dictionaries  
                themeDict["_ThemeEditorGenerated"] = true;
                app.Resources.MergedDictionaries.Add(themeDict);

                _logger.LogDebug("Applied theme colors via MergedDictionaries");

                // THE FINAL FIX: Force complete style invalidation
                ForceStyleInvalidationAfterResourceConfirmed();

                // Debug and verify
                DebugResourceState();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying theme in memory");
            }
        });
    });
}

/// <summary>
/// The FINAL fix - Force complete style invalidation after confirming resources exist
/// </summary>
private void ForceStyleInvalidationAfterResourceConfirmed()
{
    try
    {
        var app = Application.Current;
        if (app?.Styles == null) return;

        _logger.LogDebug("Forcing complete style invalidation...");

        // First, confirm our resources are actually findable
        try
        {
            var primaryHover = app.FindResource("PrimaryHoverBrush");
            var secondaryHover = app.FindResource("SecondaryHoverBrush");
            _logger.LogDebug("✅ Confirmed resources exist: Primary={Primary}, Secondary={Secondary}", 
                primaryHover, secondaryHover);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Resources still not findable, cannot proceed with style invalidation");
            return;
        }

        // Method 1: Force style system refresh by manipulating the style collection
        // This is the most reliable approach
        var allStyles = app.Styles.ToList();
        app.Styles.Clear();
        
        // Small delay to ensure style system clears
        System.Threading.Thread.Sleep(50);
        
        // Re-add all styles in correct order
        foreach (var style in allStyles)
        {
            app.Styles.Add(style);
        }
        
        _logger.LogDebug("✅ Completely refreshed style system");

        // Method 2: Force theme variant refresh to invalidate all theme-dependent styles
        var currentVariant = app.RequestedThemeVariant;
        app.RequestedThemeVariant = ThemeVariant.Light;
        System.Threading.Thread.Sleep(10);
        app.RequestedThemeVariant = currentVariant;
        
        _logger.LogDebug("✅ Forced theme variant refresh");

        // Method 3: Try to force visual invalidation on main windows
        if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                try
                {
                    // Force visual invalidation
                    window.InvalidateVisual();
                    window.InvalidateMeasure();
                    window.InvalidateArrange();
                    _logger.LogDebug("Invalidated visuals for window: {Title}", window.Title);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to invalidate window visuals");
                }
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to force style invalidation");
    }
}

/// <summary>
/// The CORRECT fix - don't reload static theme files, just refresh ModernDesignClasses
/// </summary>
private void RefreshModernDesignClassesOnly()
{
    try
    {
        var app = Application.Current;
        if (app?.Styles == null) return;

        _logger.LogDebug("Refreshing ONLY ModernDesignClasses (not theme files)...");

        // Find ModernDesignClasses
        var modernDesignClasses = app.Styles
            .OfType<StyleInclude>()
            .FirstOrDefault(s => s.Source?.ToString().Contains("ModernDesignClasses") == true);

        if (modernDesignClasses != null)
        {
            var sourceUri = modernDesignClasses.Source;
            
            // Remove and re-add ONLY ModernDesignClasses
            app.Styles.Remove(modernDesignClasses);
            
            // Small delay
            System.Threading.Thread.Sleep(10);
            
            // Re-add ModernDesignClasses - it should now find our hover brushes
            var newStyleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
            {
                Source = sourceUri
            };
            
            app.Styles.Add(newStyleInclude);
            
            _logger.LogDebug("✅ ModernDesignClasses refreshed - should now find hover brushes");
        }
        else
        {
            // Manually add if not found
            var styleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
            {
                Source = new Uri("avares://ProxyStudio/Themes/Common/ModernDesignClasses.axaml")
            };

            app.Styles.Add(styleInclude);
            _logger.LogDebug("✅ Manually added ModernDesignClasses");
        }

        // Test resource accessibility
        var canFindHover = app.Resources.TryGetValue("PrimaryHoverBrush", out var hoverTest);
        _logger.LogDebug("After ModernDesignClasses refresh, can find PrimaryHoverBrush: {CanFind}, Value: {Value}", 
            canFindHover, hoverTest);

        // Also test with FindResource (what DynamicResource actually uses)
        try
        {
            var dynamicHover = app.FindResource("PrimaryHoverBrush");
            _logger.LogDebug("✅ FindResource found PrimaryHoverBrush: {Value}", dynamicHover);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("❌ FindResource failed to find PrimaryHoverBrush: {Error}", ex.Message);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to refresh ModernDesignClasses");
    }
}

    /// <summary>
    /// Backup current application resources for restoration
    /// </summary>
    private void BackupCurrentResources()
    {
        try
        {
            var app = Application.Current;
            if (app?.Resources == null) return;

            _originalResources = new Dictionary<string, object>();

            // Backup all theme-related resources
            var themeKeys = app.Resources.Keys
                .OfType<string>()
                .Where(k => k.EndsWith("Brush") || k.EndsWith("Color"))
                .ToList();

            foreach (var key in themeKeys)
            {
                if (app.Resources.TryGetValue(key, out var resource))
                {
                    _originalResources[key] = resource;
                }
            }

            _logger.LogDebug("Backed up {Count} theme resources", _originalResources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to backup current resources");
            _originalResources = new Dictionary<string, object>();
        }
    }

    // private void ApplyColorsToResources(IResourceDictionary resources, ObservableCollection<ColorProperty> colors, string category)
    // {
    //     foreach (var colorProp in colors)
    //     {
    //         try
    //         {
    //             var color = Color.Parse(colorProp.HexValue);
    //             var brush = new SolidColorBrush(color);
    //
    //             var colorKey = colorProp.Name.Replace(" ", "") + "Color";
    //             var brushKey = colorProp.Name.Replace(" ", "") + "Brush";
    //
    //             // Add both color and brush resources
    //             resources[colorKey] = color;
    //             resources[brushKey] = brush;
    //
    //             _logger.LogTrace("Applied {Category} color: {Name} = {Value}", category, colorProp.Name, colorProp.HexValue);
    //         }
    //         catch (Exception ex)
    //         {
    //             _logger.LogWarning(ex, "Failed to parse color {Name}: {Value}", colorProp.Name, colorProp.HexValue);
    //         }
    //     }
    // }
    
    // <summary>
    /// Improved color application with resource invalidation
    /// </summary>
    private void ApplyColorsToResources(IResourceDictionary resources, ObservableCollection<ColorProperty> colors, string category)
    {
        foreach (var colorProp in colors)
        {
            try
            {
                var color = Color.Parse(colorProp.HexValue);
                var brush = new SolidColorBrush(color);

                var colorKey = colorProp.Name.Replace(" ", "") + "Color";
                var brushKey = colorProp.Name.Replace(" ", "") + "Brush";

                // For ResourceDictionary, we can add directly without removing first
                // since we're creating a new dictionary each time
                resources[colorKey] = color;
                resources[brushKey] = brush;

                _logger.LogTrace("Applied {Category} color: {Name} = {Value} (Keys: {ColorKey}, {BrushKey})", 
                    category, colorProp.Name, colorProp.HexValue, colorKey, brushKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse color {Name}: {Value}", colorProp.Name, colorProp.HexValue);
            }
        }
    }
    
/// <summary>
/// Debug method to verify resource state  
/// </summary>
private void DebugResourceState()
{
    try
    {
        var app = Application.Current;
        if (app?.Resources == null) return;

        _logger.LogDebug("=== Resource Debug Information ===");
        
        // Check if Primary color exists
        if (app.Resources.TryGetValue("PrimaryColor", out var primaryColor))
        {
            _logger.LogDebug("PrimaryColor found: {Color}", primaryColor);
        }
        else
        {
            _logger.LogWarning("PrimaryColor NOT found in resources");
        }

        // Check if PrimaryHover exists
        if (app.Resources.TryGetValue("PrimaryHoverColor", out var primaryHoverColor))
        {
            _logger.LogDebug("PrimaryHoverColor found: {Color}", primaryHoverColor);
        }
        else
        {
            _logger.LogWarning("PrimaryHoverColor NOT found in resources");
        }

        if (app.Resources.TryGetValue("PrimaryHoverBrush", out var primaryHoverBrush))
        {
            _logger.LogDebug("PrimaryHoverBrush found: {Brush}", primaryHoverBrush);
        }
        else
        {
            _logger.LogWarning("PrimaryHoverBrush NOT found in resources");
        }

        // List all color/brush resources from main dictionary
        if (app.Resources is ResourceDictionary mainDict)
        {
            var colorBrushKeys = mainDict.Keys
                .OfType<string>()
                .Where(k => k.Contains("Color") || k.Contains("Brush"))
                .OrderBy(k => k)
                .ToList();

            _logger.LogDebug("Main Resource Dictionary Color/Brush resources ({Count}): {Keys}", 
                colorBrushKeys.Count, string.Join(", ", colorBrushKeys));
        }

        // Check merged dictionaries
        _logger.LogDebug("MergedDictionaries count: {Count}", app.Resources.MergedDictionaries.Count);
        
        for (int i = 0; i < app.Resources.MergedDictionaries.Count; i++)
        {
            var provider = app.Resources.MergedDictionaries[i];
            
            // Try to cast to ResourceDictionary to access Keys
            if (provider is ResourceDictionary dict)
            {
                var dictKeys = dict.Keys.OfType<string>()
                    .Where(k => k.Contains("Primary"))
                    .ToList();
                
                if (dictKeys.Any())
                {
                    _logger.LogDebug("MergedDictionary[{Index}] Primary keys: {Keys}", i, string.Join(", ", dictKeys));
                }
                
                // Check if our generated marker exists
                if (dict.ContainsKey("_ThemeEditorGenerated"))
                {
                    _logger.LogDebug("Found our theme dictionary at index {Index}", i);
                }
            }
            else
            {
                _logger.LogDebug("MergedDictionary[{Index}] is {Type} (not ResourceDictionary)", i, provider.GetType().Name);
            }
        }

        _logger.LogDebug("=== End Resource Debug ===");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during resource debug");
    }
}
    
// /// <summary>
// /// Enhanced derived colors generation - includes all hover states for semantic colors
// /// </summary>
// private void ApplyDerivedColorsToResources(IResourceDictionary resources)
// {
//     try
//     {
//         _logger.LogDebug("Generating enhanced derived colors with all hover states...");
//
//         // Generate hover states for Foundation colors (15% darker)
//         GenerateHoverState(resources, "Primary");
//         GenerateHoverState(resources, "Secondary");
//
//         // Generate hover states for Semantic colors (15% darker)
//         GenerateHoverState(resources, "Success");
//         GenerateHoverState(resources, "Warning");
//         GenerateHoverState(resources, "Error");
//         GenerateHoverState(resources, "Info");
//
//         // Generate light variants for status backgrounds
//         GenerateStatusLightVariants(resources, "Success");
//         GenerateStatusLightVariants(resources, "Warning");
//         GenerateStatusLightVariants(resources, "Error");
//         GenerateStatusLightVariants(resources, "Info");
//
//         // Generate contrasting text colors
//         GenerateContrastingText(resources, "Primary");
//         GenerateContrastingText(resources, "Secondary");
//
//         // Generate surface hover states if needed
//         if (resources.TryGetValue("SurfaceColor", out var surfaceColorObj) && surfaceColorObj is Color surfaceColor)
//         {
//             var surfaceHover = DarkenColor(surfaceColor, 0.05f); // Subtle hover for surfaces
//             resources["SurfaceHoverColor"] = surfaceHover;
//             resources["SurfaceHoverBrush"] = new SolidColorBrush(surfaceHover);
//         }
//
//         _logger.LogDebug("Enhanced derived colors generation completed successfully");
//     }
//     catch (Exception ex)
//     {
//         _logger.LogWarning(ex, "Failed to generate enhanced derived colors");
//     }
// }

/// <summary>
/// Debug version - Enhanced derived colors generation with detailed logging
/// </summary>
private void ApplyDerivedColorsToResources(IResourceDictionary resources)
{
    try
    {
        _logger.LogDebug("=== STARTING DERIVED COLORS GENERATION ===");

        // Check what's in the resources before we start
        _logger.LogDebug("Resources contains {Count} items before generation", resources.Count);
        
        // List all color resources currently in the dictionary
        var existingColors = resources.Keys.OfType<string>()
            .Where(k => k.Contains("Color"))
            .ToList();
        _logger.LogDebug("Existing color resources: {Colors}", string.Join(", ", existingColors));

        // Generate hover states for Foundation colors (15% darker)
        _logger.LogDebug("Generating Foundation hover states...");
        GenerateHoverState(resources, "Primary");
        GenerateHoverState(resources, "Secondary");

        // Generate hover states for Semantic colors (15% darker)
        _logger.LogDebug("Generating Semantic hover states...");
        GenerateHoverState(resources, "Success");
        GenerateHoverState(resources, "Warning");
        GenerateHoverState(resources, "Error");
        GenerateHoverState(resources, "Info");

        // Generate light variants for status backgrounds
        _logger.LogDebug("Generating status light variants...");
        GenerateStatusLightVariants(resources, "Success");
        GenerateStatusLightVariants(resources, "Warning");
        GenerateStatusLightVariants(resources, "Error");
        GenerateStatusLightVariants(resources, "Info");

        // Generate contrasting text colors
        _logger.LogDebug("Generating contrasting text colors...");
        GenerateContrastingText(resources, "Primary");
        GenerateContrastingText(resources, "Secondary");

        // Generate surface hover states if needed
        if (resources.TryGetValue("SurfaceColor", out var surfaceColorObj) && surfaceColorObj is Color surfaceColor)
        {
            var surfaceHover = DarkenColor(surfaceColor, 0.05f); // Subtle hover for surfaces
            resources["SurfaceHoverColor"] = surfaceHover;
            resources["SurfaceHoverBrush"] = new SolidColorBrush(surfaceHover);
            _logger.LogDebug("Generated SurfaceHover: {Color}", surfaceHover);
        }

        // Final check - list all hover brushes that were created
        var hoverBrushes = resources.Keys.OfType<string>()
            .Where(k => k.Contains("Hover") && k.EndsWith("Brush"))
            .ToList();
        _logger.LogDebug("Created hover brushes: {Brushes}", string.Join(", ", hoverBrushes));

        // Specifically check if PrimaryHoverBrush exists and log its value
        if (resources.TryGetValue("PrimaryHoverBrush", out var primaryHoverBrush) && primaryHoverBrush is SolidColorBrush phb)
        {
            _logger.LogDebug("✅ PrimaryHoverBrush successfully created: {Color}", phb.Color);
        }
        else
        {
            _logger.LogError("❌ PrimaryHoverBrush was NOT created!");
        }

        if (resources.TryGetValue("SecondaryHoverBrush", out var secondaryHoverBrush) && secondaryHoverBrush is SolidColorBrush shb)
        {
            _logger.LogDebug("✅ SecondaryHoverBrush successfully created: {Color}", shb.Color);
        }
        else
        {
            _logger.LogError("❌ SecondaryHoverBrush was NOT created!");
        }

        _logger.LogDebug("=== DERIVED COLORS GENERATION COMPLETED ===");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to generate enhanced derived colors");
    }
}

/// <summary>
/// Generate hover state for any color (15% darker)
/// </summary>
// private void GenerateHoverState(IResourceDictionary resources, string colorName)
// {
//     try
//     {
//         var colorKey = colorName + "Color";
//         var hoverColorKey = colorName + "HoverColor";
//         var hoverBrushKey = colorName + "HoverBrush";
//
//         if (resources.TryGetValue(colorKey, out var colorObj) && colorObj is Color color)
//         {
//             var hoverColor = DarkenColor(color, 0.15f);
//             
//             resources[hoverColorKey] = hoverColor;
//             resources[hoverBrushKey] = new SolidColorBrush(hoverColor);
//             
//             _logger.LogDebug("Generated {ColorName}Hover: {HoverColor} from {OriginalColor}", 
//                 colorName, hoverColor, color);
//         }
//         else
//         {
//             _logger.LogWarning("{ColorName} color not found for hover generation", colorName);
//         }
//     }
//     catch (Exception ex)
//     {
//         _logger.LogWarning(ex, "Failed to generate hover state for {ColorName}", colorName);
//     }
// }

/// <summary>
/// Generate hover state for any color (15% darker) - with detailed logging
/// </summary>
private void GenerateHoverState(IResourceDictionary resources, string colorName)
{
    try
    {
        var colorKey = colorName + "Color";
        var hoverColorKey = colorName + "HoverColor";
        var hoverBrushKey = colorName + "HoverBrush";

        _logger.LogDebug("Attempting to generate hover for {ColorName}...", colorName);
        _logger.LogDebug("Looking for source color with key: {ColorKey}", colorKey);

        if (resources.TryGetValue(colorKey, out var colorObj))
        {
            _logger.LogDebug("Found color object: {ColorObj} (Type: {Type})", colorObj, colorObj?.GetType().Name);
            
            if (colorObj is Color color)
            {
                var hoverColor = DarkenColor(color, 0.15f);
                
                resources[hoverColorKey] = hoverColor;
                resources[hoverBrushKey] = new SolidColorBrush(hoverColor);
                
                _logger.LogDebug("✅ Generated {ColorName}Hover: {HoverColor} from {OriginalColor}", 
                    colorName, hoverColor, color);
                _logger.LogDebug("✅ Added keys: {HoverColorKey}, {HoverBrushKey}", hoverColorKey, hoverBrushKey);
            }
            else
            {
                _logger.LogWarning("❌ Color object is not of type Color: {ActualType}", colorObj?.GetType().Name);
            }
        }
        else
        {
            _logger.LogWarning("❌ {ColorName} color not found for hover generation (key: {ColorKey})", colorName, colorKey);
            
            // List all available keys for debugging
            var availableKeys = resources.Keys.OfType<string>().Where(k => k.Contains(colorName)).ToList();
            _logger.LogDebug("Available keys containing '{ColorName}': {Keys}", colorName, string.Join(", ", availableKeys));
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to generate hover state for {ColorName}", colorName);
    }
}

/// <summary>
/// Debug method to see what styles are actually loaded
/// </summary>
private void DebugApplicationStyles()
{
    try
    {
        var app = Application.Current;
        if (app?.Styles == null) return;

        _logger.LogDebug("=== APPLICATION STYLES DEBUG ===");
        _logger.LogDebug("Total styles count: {Count}", app.Styles.Count);

        for (int i = 0; i < app.Styles.Count; i++)
        {
            var style = app.Styles[i];
            _logger.LogDebug("Style[{Index}]: {Type}", i, style.GetType().Name);

            if (style is StyleInclude styleInclude)
            {
                _logger.LogDebug("  Source: {Source}", styleInclude.Source?.ToString());
            }
            else if (style is FluentTheme)
            {
                _logger.LogDebug("  FluentTheme detected");
            }
            else
            {
                _logger.LogDebug("  Details: {ToString}", style.ToString());
            }
        }

        // Look for ModernDesignClasses more specifically
        var modernDesignFound = app.Styles
            .OfType<StyleInclude>()
            .Where(s => s.Source?.ToString().Contains("ModernDesign") == true)
            .ToList();

        _logger.LogDebug("Found {Count} ModernDesign-related styles:", modernDesignFound.Count);
        foreach (var style in modernDesignFound)
        {
            _logger.LogDebug("  - {Source}", style.Source?.ToString());
        }

        // List ALL StyleInclude sources
        var allStyleIncludes = app.Styles
            .OfType<StyleInclude>()
            .Select(s => s.Source?.ToString())
            .Where(s => s != null)
            .ToList();

        _logger.LogDebug("All StyleInclude sources: {Sources}", string.Join(", ", allStyleIncludes));

        _logger.LogDebug("=== END APPLICATION STYLES DEBUG ===");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error debugging application styles");
    }
}

/// <summary>
/// Force complete recompilation of ModernDesignClasses after hover brushes are available
/// </summary>
private void ForceCompleteStyleRecompilation()
{
    try
    {
        var app = Application.Current;
        if (app?.Styles == null) return;

        _logger.LogDebug("Forcing complete style recompilation...");

        // Remove ALL StyleInclude items that might reference our resources
        var stylesToRemove = app.Styles
            .OfType<StyleInclude>()
            .ToList();

        foreach (var style in stylesToRemove)
        {
            app.Styles.Remove(style);
            _logger.LogDebug("Removed style: {Source}", style.Source);
        }

        // Force garbage collection to clear any cached compiled styles
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Small delay to ensure everything is cleared
        //await Task.Delay(10);

        // Now re-add them in the correct order
        // Add theme first (so resources are available)
        var themeStyle = new StyleInclude(new Uri("avares://ProxyStudio/"))
        {
            Source = new Uri("avares://ProxyStudio/Themes/DarkProfessional.axaml")
        };
        app.Styles.Add(themeStyle);
        _logger.LogDebug("Re-added theme style");

        // Small delay to let theme resources settle
        //await Task.Delay(10);

        // Then add ModernDesignClasses (which can now find the hover brushes)
        var modernDesignStyle = new StyleInclude(new Uri("avares://ProxyStudio/"))
        {
            Source = new Uri("avares://ProxyStudio/Themes/Common/ModernDesignClasses.axaml")
        };
        app.Styles.Add(modernDesignStyle);
        _logger.LogDebug("Re-added ModernDesignClasses style");

        // Test if we can now find the hover brush
        var canFindHover = app.Resources.TryGetValue("PrimaryHoverBrush", out var hoverTest);
        _logger.LogDebug("After recompilation, can find PrimaryHoverBrush: {CanFind}, Value: {Value}", 
            canFindHover, hoverTest);

        _logger.LogDebug("✅ Complete style recompilation finished");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to force complete style recompilation");
    }
}


/// <summary>
/// Enhanced refresh method with better detection
/// </summary>
private void RefreshModernDesignClassesAfterThemeChange()
{
    try
    {
        var app = Application.Current;
        if (app?.Styles == null) return;

        _logger.LogDebug("Refreshing ModernDesignClasses to pick up new hover brushes...");

        // First, debug what styles we have
        DebugApplicationStyles();

        // Try different ways to find ModernDesignClasses
        var modernDesignClasses = app.Styles
            .OfType<StyleInclude>()
            .FirstOrDefault(s => s.Source?.ToString().Contains("ModernDesignClasses") == true);

        if (modernDesignClasses == null)
        {
            // Try without case sensitivity
            modernDesignClasses = app.Styles
                .OfType<StyleInclude>()
                .FirstOrDefault(s => s.Source?.ToString().ToLowerInvariant().Contains("moderndesign") == true);
        }

        if (modernDesignClasses == null)
        {
            // Try looking for any file in Common folder
            modernDesignClasses = app.Styles
                .OfType<StyleInclude>()
                .FirstOrDefault(s => s.Source?.ToString().Contains("Common") == true);
        }

        if (modernDesignClasses != null)
        {
            var sourceUri = modernDesignClasses.Source;
            _logger.LogDebug("Found ModernDesignClasses: {Source}", sourceUri);
            
            // Remove and re-add to force DynamicResource re-evaluation
            app.Styles.Remove(modernDesignClasses);
            
            // Create a new StyleInclude with the same source
            var newStyleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
            {
                Source = sourceUri
            };
            
            app.Styles.Add(newStyleInclude);
            
            _logger.LogDebug("✅ ModernDesignClasses refreshed - hover styles should now work");
        }
        else
        {
            _logger.LogWarning("❌ ModernDesignClasses not found in app styles AT ALL");
            _logger.LogWarning("This means the styles are not being loaded or have a different name");
            
            // As a fallback, try to manually add ModernDesignClasses
            TryAddModernDesignClasses();
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to refresh ModernDesignClasses");
    }
}

/// <summary>
/// Fallback method to manually add ModernDesignClasses if not found
/// </summary>
private void TryAddModernDesignClasses()
{
    try
    {
        var app = Application.Current;
        if (app?.Styles == null) return;

        _logger.LogDebug("Attempting to manually add ModernDesignClasses...");

        var styleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
        {
            Source = new Uri("avares://ProxyStudio/Themes/Common/ModernDesignClasses.axaml")
        };

        app.Styles.Add(styleInclude);
        
        _logger.LogDebug("✅ Manually added ModernDesignClasses to application styles");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to manually add ModernDesignClasses: {Error}", ex.Message);
    }
}


/// <summary>
/// Generate contrasting text color for backgrounds
/// </summary>
private void GenerateContrastingText(IResourceDictionary resources, string backgroundColorName)
{
    try
    {
        var backgroundColorKey = backgroundColorName + "Color";
        var textColorKey = "TextOn" + backgroundColorName + "Color";
        var textBrushKey = "TextOn" + backgroundColorName + "Brush";

        if (resources.TryGetValue(backgroundColorKey, out var bgColorObj) && bgColorObj is Color bgColor)
        {
            var textColor = GetContrastingTextColor(bgColor);
            
            resources[textColorKey] = textColor;
            resources[textBrushKey] = new SolidColorBrush(textColor);
            
            _logger.LogDebug("Generated TextOn{BackgroundName}: {TextColor} for background {BackgroundColor}", 
                backgroundColorName, textColor, bgColor);
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to generate contrasting text for {BackgroundName}", backgroundColorName);
    }
}

/// <summary>
/// Force UI refresh using the correct Avalonia 11.3 approach
/// </summary>
private void ForceResourceRefresh()
{
    try
    {
        // The correct way in Avalonia 11.3 is to manipulate the MergedDictionaries
        // This triggers proper change notifications for DynamicResource bindings
        var app = Application.Current;
        if (app?.Resources == null) return;

        // Create a temporary resource dictionary to trigger change notification
        var tempDict = new ResourceDictionary();
        
        // Add and immediately remove to force resource system refresh
        // This is the recommended approach for forcing DynamicResource updates
        app.Resources.MergedDictionaries.Add(tempDict);
        app.Resources.MergedDictionaries.Remove(tempDict);

        _logger.LogDebug("Forced resource refresh using MergedDictionaries manipulation");
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "Could not force resource refresh - changes may require restart");
    }
}

    // private void ApplyDerivedColorsToResources(IResourceDictionary resources)
    // {
    //     try
    //     {
    //         // Generate hover states (15% darker)
    //         if (resources.TryGetValue("PrimaryColor", out var primaryColorObj) && primaryColorObj is Color primaryColor)
    //         {
    //             var primaryHover = DarkenColor(primaryColor, 0.15f);
    //             resources["PrimaryHoverColor"] = primaryHover;
    //             resources["PrimaryHoverBrush"] = new SolidColorBrush(primaryHover);
    //         }
    //
    //         if (resources.TryGetValue("SecondaryColor", out var secondaryColorObj) && secondaryColorObj is Color secondaryColor)
    //         {
    //             var secondaryHover = DarkenColor(secondaryColor, 0.15f);
    //             resources["SecondaryHoverColor"] = secondaryHover;
    //             resources["SecondaryHoverBrush"] = new SolidColorBrush(secondaryHover);
    //         }
    //
    //         // Generate light variants for status backgrounds
    //         GenerateStatusLightVariants(resources, "Success");
    //         GenerateStatusLightVariants(resources, "Warning");
    //         GenerateStatusLightVariants(resources, "Error");
    //         GenerateStatusLightVariants(resources, "Info");
    //
    //         // Generate contrasting text on primary
    //         if (resources.TryGetValue("PrimaryColor", out var primaryForTextObj) && primaryForTextObj is Color primaryForText)
    //         {
    //             var textOnPrimary = GetContrastingTextColor(primaryForText);
    //             resources["TextOnPrimaryColor"] = textOnPrimary;
    //             resources["TextOnPrimaryBrush"] = new SolidColorBrush(textOnPrimary);
    //         }
    //
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogWarning(ex, "Failed to generate derived colors");
    //     }
    // }
    
    

    /// <summary>
    /// Enhanced status light variants generation
    /// </summary>
    private void GenerateStatusLightVariants(IResourceDictionary resources, string statusName)
    {
        try
        {
            if (resources.TryGetValue($"{statusName}Color", out var statusColorObj) && statusColorObj is Color statusColor)
            {
                // Determine if we're in dark theme by checking background
                var isDarkTheme = true; // Default assumption
                if (resources.TryGetValue("BackgroundColor", out var bgColorObj) && bgColorObj is Color bgColor)
                {
                    var brightness = (0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B) / 255;
                    isDarkTheme = brightness < 0.5;
                }

                // Generate appropriate light variant
                var lightColor = isDarkTheme 
                    ? DarkenColor(statusColor, 0.7f)  // Much darker for dark themes
                    : LightenColor(statusColor, 0.8f); // Much lighter for light themes

                resources[$"{statusName}LightColor"] = lightColor;
                resources[$"{statusName}LightBrush"] = new SolidColorBrush(lightColor);

                _logger.LogDebug("Generated {StatusName}Light: {LightColor} (isDarkTheme: {IsDark})", 
                    statusName, lightColor, isDarkTheme);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate light variant for {StatusName}", statusName);
        }
    }


    private Color DarkenColor(Color color, float amount)
    {
        var r = (byte)Math.Max(0, color.R - (int)(255 * amount));
        var g = (byte)Math.Max(0, color.G - (int)(255 * amount));
        var b = (byte)Math.Max(0, color.B - (int)(255 * amount));
        return Color.FromRgb(r, g, b);
    }

    private Color LightenColor(Color color, float amount)
    {
        var r = (byte)Math.Min(255, color.R + (int)(255 * amount));
        var g = (byte)Math.Min(255, color.G + (int)(255 * amount));
        var b = (byte)Math.Min(255, color.B + (int)(255 * amount));
        return Color.FromRgb(r, g, b);
    }

    private Color GetContrastingTextColor(Color backgroundColor)
    {
        var brightness = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
        return brightness > 0.5 ? Colors.Black : Colors.White;
    }

    // FILE OPERATIONS - SEPARATE FROM PREVIEW
    private async Task SaveThemeAsync()
    {
        try
        {
            StatusMessage = "Saving theme...";
            
            var themeXaml = GenerateFullThemeXaml();
            var fileName = $"{ThemeName.Replace(" ", "_")}.axaml";
            var themesDir = _themeService.GetThemesDirectory();
            var filePath = Path.Combine(themesDir, fileName);

            Directory.CreateDirectory(themesDir);
            await File.WriteAllTextAsync(filePath, themeXaml);

            StatusMessage = "Theme saved successfully";
            CanSaveTheme = false;

            await _errorHandler.ShowErrorAsync("Theme Saved", $"Theme '{ThemeName}' saved successfully!", ErrorSeverity.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save theme");
            await _errorHandler.HandleExceptionAsync(ex, "Failed to save theme", "Theme Editor");
            StatusMessage = "Failed to save theme";
        }
    }

    private async Task ExportThemeAsync()
    {
        try
        {
            var topLevel = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;

            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Theme File",
                DefaultExtension = "axaml",
                SuggestedFileName = $"{ThemeName.Replace(" ", "_")}.axaml",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("XAML Theme Files") { Patterns = new[] { "*.axaml" } }
                }
            });

            if (file != null)
            {
                StatusMessage = "Exporting theme...";
                
                var themeXaml = GenerateFullThemeXaml();

                using var stream = await file.OpenWriteAsync();
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync(themeXaml);

                StatusMessage = "Theme exported successfully";
                await _errorHandler.ShowErrorAsync("Theme Exported", $"Theme exported successfully to: {file.Name}", ErrorSeverity.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export theme");
            await _errorHandler.HandleExceptionAsync(ex, "Failed to export theme", "Theme Editor");
            StatusMessage = "Failed to export theme";
        }
    }

    private string GenerateFullThemeXaml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Styles xmlns=\"https://github.com/avaloniaui\"");
        sb.AppendLine("        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        sb.AppendLine();
        sb.AppendLine($"  <!-- {ThemeName} -->");
        sb.AppendLine($"  <!-- Description: {ThemeDescription} -->");
        sb.AppendLine($"  <!-- Author: {ThemeAuthor} -->");
        sb.AppendLine($"  <!-- Version: {ThemeVersion} -->");
        sb.AppendLine($"  <!-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} -->");
        sb.AppendLine();

        sb.AppendLine("  <Styles.Resources>");
        sb.AppendLine("    <ResourceDictionary>");
        sb.AppendLine("      <ResourceDictionary.ThemeDictionaries>");
        sb.AppendLine("        <ResourceDictionary x:Key=\"Dark\">");

        foreach (var color in FoundationColors.Concat(SemanticColors).Concat(SurfaceColors).Concat(TextColors))
        {
            var colorKey = color.Name.Replace(" ", "") + "Color";
            var brushKey = color.Name.Replace(" ", "") + "Brush";
            sb.AppendLine($"          <Color x:Key=\"{colorKey}\">{color.HexValue}</Color>");
            sb.AppendLine($"          <SolidColorBrush x:Key=\"{brushKey}\" Color=\"{{StaticResource {colorKey}}}\"/>");
        }

        sb.AppendLine("        </ResourceDictionary>");
        sb.AppendLine("      </ResourceDictionary.ThemeDictionaries>");
        sb.AppendLine("    </ResourceDictionary>");
        sb.AppendLine("  </Styles.Resources>");
        sb.AppendLine("</Styles>");

        return sb.ToString();
    }

    public async Task CleanupAsync()
    {
        if (IsPreviewActive && _originalResources != null)
        {
            try
            {
                await Task.Run(() =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var app = Application.Current;
                        if (app?.Resources == null) return;

                        var keysToRemove = app.Resources.Keys
                            .OfType<string>()
                            .Where(k => k.EndsWith("Brush") || k.EndsWith("Color"))
                            .ToList();

                        foreach (var key in keysToRemove)
                        {
                            app.Resources.Remove(key);
                        }

                        foreach (var kvp in _originalResources)
                        {
                            app.Resources[kvp.Key] = kvp.Value;
                        }
                    });
                });

                _logger.LogInformation("Cleaned up theme preview from memory on disposal");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup theme preview");
            }
            finally
            {
                IsPreviewActive = false;
                _originalThemeBeforePreview = null;
                _originalResources = null;
            }
        }
    }
}