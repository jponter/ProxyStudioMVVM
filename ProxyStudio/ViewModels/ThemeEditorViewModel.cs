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
    // Field to track our custom resource dictionary
    private ResourceDictionary? _customThemeDictionary;
    // Field to track our custom resource dictionary
   
    // Store original theme file path for restoration
    private string? _originalThemeStylePath;

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

    // // CORRECT IN-MEMORY PREVIEW METHODS - NO FILE OPERATIONS
    // private async Task PreviewCurrentThemeAsync()
    // {
    //     try
    //     {
    //         StatusMessage = "Applying preview theme...";
    //
    //         if (!IsPreviewActive)
    //         {
    //             _originalThemeBeforePreview = _themeService.CurrentTheme;
    //             BackupCurrentResources();
    //         }
    //
    //         IsPreviewActive = true;
    //
    //         // Apply theme colors directly to Application.Resources in memory
    //         await ApplyThemeInMemory();
    //
    //         StatusMessage = "Preview active - theme applied temporarily";
    //         _logger.LogInformation("Theme preview applied: {ThemeName}", ThemeName);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Failed to preview theme: {ThemeName}", ThemeName);
    //         await _errorHandler.HandleExceptionAsync(ex, "Failed to preview theme", "Theme Editor");
    //         StatusMessage = "Failed to preview theme";
    //         IsPreviewActive = false;
    //         _originalThemeBeforePreview = null;
    //         _originalResources = null;
    //     }
    // }
    
    
    
    /// <summary>
    /// WORKING: Apply preview using the original MergedDictionaries approach
    /// </summary>
    [RelayCommand]
    private async Task PreviewCurrentThemeAsync()
    {
        try
        {
            StatusMessage = "Applying preview theme...";

            if (!IsPreviewActive)
            {
                _originalThemeBeforePreview = _themeService.CurrentTheme;
                // DON'T backup resources - they're not in Application.Resources anyway
            }

            IsPreviewActive = true;

            // Apply theme colors directly using the ORIGINAL working method
            await ApplyThemeInMemory();

            StatusMessage = "Preview active - theme applied temporarily";
            _logger.LogInformation("Theme preview applied: {ThemeName}", ThemeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview theme: {ThemeName}", ThemeName);
            StatusMessage = "Failed to preview theme";
            IsPreviewActive = false;
            _originalThemeBeforePreview = null;
        }
    }
    
    /// <summary>
    /// Apply preview theme by creating and loading a temporary theme StyleInclude
    /// </summary>
    private async Task ApplyPreviewThemeAsStyleInclude()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Styles == null) return;
                    
                    // 1. Generate the theme XAML content
                    var themeXaml = GeneratePreviewThemeXaml();
                    
                    // 2. Create temporary theme file
                    var tempDir = Path.Combine(Path.GetTempPath(), "ProxyStudio", "ThemePreview");
                    Directory.CreateDirectory(tempDir);
                    var tempThemeFile = Path.Combine(tempDir, "PreviewTheme.axaml");
                    
                    await File.WriteAllTextAsync(tempThemeFile, themeXaml);
                    _logger.LogDebug("Created temporary theme file: {Path}", tempThemeFile);
                    
                    // 3. Remove existing theme StyleIncludes (but keep ModernDesignClasses)
                    var existingThemes = app.Styles
                        .OfType<StyleInclude>()
                        .Where(s => s.Source?.ToString().Contains("Themes/") == true && 
                                   !s.Source.ToString().Contains("ModernDesign") == true)
                        .ToList();
                    
                    foreach (var theme in existingThemes)
                    {
                        app.Styles.Remove(theme);
                        _logger.LogDebug("Removed existing theme: {Source}", theme.Source);
                    }
                    
                    // 4. Add our preview theme StyleInclude
                    var previewStyleInclude = new StyleInclude(new Uri("file:///"))
                    {
                        Source = new Uri(tempThemeFile)
                    };
                    
                    // Insert before ModernDesignClasses (so resources are available to it)
                    var modernDesignIndex = app.Styles
                        .OfType<StyleInclude>()
                        .Select((style, index) => new { style, index })
                        .FirstOrDefault(x => x.style.Source?.ToString().Contains("ModernDesign") == true)?.index;
                    
                    if (modernDesignIndex.HasValue)
                    {
                        app.Styles.Insert(modernDesignIndex.Value, previewStyleInclude);
                        _logger.LogDebug("Inserted preview theme before ModernDesignClasses at index {Index}", modernDesignIndex.Value);
                    }
                    else
                    {
                        app.Styles.Add(previewStyleInclude);
                        _logger.LogDebug("Added preview theme at end of styles");
                    }
                    
                    _logger.LogInformation("Preview theme StyleInclude loaded successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply preview theme as StyleInclude");
                }
            });
        });
    }
    
    /// <summary>
    /// Generate the complete theme XAML content for preview
    /// </summary>
    private string GeneratePreviewThemeXaml()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<Styles xmlns=\"https://github.com/avaloniaui\"");
        sb.AppendLine("       xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        sb.AppendLine("    ");
        sb.AppendLine("    <Styles.Resources>");
        sb.AppendLine("        <ResourceDictionary>");
        
        // Generate Foundation Colors
        foreach (var color in FoundationColors)
        {
            var colorName = color.Name.Replace(" ", "");
            sb.AppendLine($"            <!-- {colorName} Colors -->");
            sb.AppendLine($"            <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
            sb.AppendLine($"            <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\" />");
            
            // Generate hover color (15% darker)
            var baseColor = Color.Parse(color.HexValue);
            var hoverColor = DarkenColor(baseColor, 0.15f);
            sb.AppendLine($"            <Color x:Key=\"{colorName}HoverColor\">{hoverColor}</Color>");
            sb.AppendLine($"            <SolidColorBrush x:Key=\"{colorName}HoverBrush\" Color=\"{{DynamicResource {colorName}HoverColor}}\" />");
        }
        
        // Generate Semantic Colors
        foreach (var color in SemanticColors)
        {
            var colorName = color.Name.Replace(" ", "");
            sb.AppendLine($"            <!-- {colorName} Colors -->");
            sb.AppendLine($"            <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
            sb.AppendLine($"            <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\" />");
            
            // Generate hover and light variants
            var baseColor = Color.Parse(color.HexValue);
            var hoverColor = DarkenColor(baseColor, 0.15f);
            var lightColor = LightenColor(baseColor, 0.8f);
            
            sb.AppendLine($"            <Color x:Key=\"{colorName}HoverColor\">{hoverColor}</Color>");
            sb.AppendLine($"            <SolidColorBrush x:Key=\"{colorName}HoverBrush\" Color=\"{{DynamicResource {colorName}HoverColor}}\" />");
            sb.AppendLine($"            <Color x:Key=\"{colorName}LightColor\">{lightColor}</Color>");
            sb.AppendLine($"            <SolidColorBrush x:Key=\"{colorName}LightBrush\" Color=\"{{DynamicResource {colorName}LightColor}}\" />");
        }
        
        // Generate Surface Colors
        foreach (var color in SurfaceColors.Concat(TextColors))
        {
            var colorName = color.Name.Replace(" ", "");
            sb.AppendLine($"            <!-- {colorName} Colors -->");
            sb.AppendLine($"            <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
            sb.AppendLine($"            <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\" />");
        }
        
        // Generate contrasting text colors
        GenerateContrastingTextXaml(sb, "Primary");
        GenerateContrastingTextXaml(sb, "Secondary");
        GenerateContrastingTextXaml(sb, "Success");
        GenerateContrastingTextXaml(sb, "Warning");
        GenerateContrastingTextXaml(sb, "Error");
        GenerateContrastingTextXaml(sb, "Info");
        
        // Generate surface hover states
        var surfaceColor = SurfaceColors.FirstOrDefault(c => c.Name.Contains("Surface") && !c.Name.Contains("Elevated"));
        if (surfaceColor != null)
        {
            var baseColor = Color.Parse(surfaceColor.HexValue);
            var hoverColor = DarkenColor(baseColor, 0.15f);
            sb.AppendLine($"            <Color x:Key=\"SurfaceHoverColor\">{hoverColor}</Color>");
            sb.AppendLine($"            <SolidColorBrush x:Key=\"SurfaceHoverBrush\" Color=\"{{DynamicResource SurfaceHoverColor}}\" />");
        }
        
        sb.AppendLine("        </ResourceDictionary>");
        sb.AppendLine("    </Styles.Resources>");
        sb.AppendLine("    ");
        sb.AppendLine("</Styles>");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generate contrasting text colors in XAML
    /// </summary>
    private void GenerateContrastingTextXaml(StringBuilder sb, string backgroundColorName)
    {
        try
        {
            var colorProperty = FoundationColors.Concat(SemanticColors)
                .FirstOrDefault(c => c.Name.Equals(backgroundColorName, StringComparison.OrdinalIgnoreCase));
            
            if (colorProperty != null)
            {
                var bgColor = Color.Parse(colorProperty.HexValue);
                var textColor = GetContrastingTextColor(bgColor);
                
                sb.AppendLine($"            <!-- Text on {backgroundColorName} -->");
                sb.AppendLine($"            <Color x:Key=\"TextOn{backgroundColorName}Color\">{textColor}</Color>");
                sb.AppendLine($"            <SolidColorBrush x:Key=\"TextOn{backgroundColorName}Brush\" Color=\"{{DynamicResource TextOn{backgroundColorName}Color}}\" />");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate contrasting text for {BackgroundName}", backgroundColorName);
        }
    }
    
    /// <summary>
    /// Apply theme with proper resource scoping to ensure ModernDesignClasses can find resources
    /// </summary>
    private async Task ApplyThemeWithProperScoping()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Resources == null) return;
                    
                    // 1. Remove any existing custom dictionary
                    RemoveExistingCustomDictionary();
                    
                    // 2. Create new theme dictionary with HIGHER PRIORITY
                    _customThemeDictionary = new ResourceDictionary();
                    
                    // 3. Apply colors to our custom dictionary
                    ApplyColorsToResources(_customThemeDictionary, FoundationColors, "Foundation");
                    ApplyColorsToResources(_customThemeDictionary, SemanticColors, "Semantic");
                    ApplyColorsToResources(_customThemeDictionary, SurfaceColors, "Surface");
                    ApplyColorsToResources(_customThemeDictionary, TextColors, "Text");
                    
                    // 4. Apply derived colors (hover states, etc.)
                    ApplyDerivedColorsToResources(_customThemeDictionary);
                    
                    // 5. Mark our dictionary for identification
                    _customThemeDictionary["_ThemeEditorCustom"] = true;
                    
                    // 6. Add to the BEGINNING of MergedDictionaries for highest priority
                    app.Resources.MergedDictionaries.Insert(0, _customThemeDictionary);
                    
                    _logger.LogDebug("Applied custom theme dictionary with {Count} resources", 
                        _customThemeDictionary.Count);
                    
                    // 7. Debug the resource state
                    DebugResourceAvailability();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply theme with proper scoping");
                }
            });
        });
    }
    
    /// <summary>
    /// Remove existing custom dictionary to prevent conflicts
    /// </summary>
    private void RemoveExistingCustomDictionary()
    {
        try
        {
            var app = Application.Current;
            if (app?.Resources == null) return;
            
            // Remove any existing custom or generated dictionaries
            for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = app.Resources.MergedDictionaries[i];
                if (dict is ResourceDictionary resourceDict && 
                    (resourceDict.ContainsKey("_ThemeEditorCustom") || 
                     resourceDict.ContainsKey("_ThemeEditorGenerated")))
                {
                    app.Resources.MergedDictionaries.RemoveAt(i);
                    _logger.LogDebug("Removed existing custom dictionary at index {Index}", i);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove existing custom dictionary");
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

    // private async Task StopPreviewAsync()
    // {
    //     try
    //     {
    //         if (!IsPreviewActive || _originalResources == null)
    //         {
    //             StatusMessage = "No preview is currently active";
    //             return;
    //         }
    //
    //         StatusMessage = "Restoring original theme...";
    //
    //         // Restore original resources directly in memory - NO FILE LOADING
    //         await Task.Run(() =>
    //         {
    //             try
    //             {
    //                 Dispatcher.UIThread.Post(() =>
    //                 {
    //                     try
    //                     {
    //                         var app = Application.Current;
    //                         if (app?.Resources == null)
    //                         {
    //                             _logger.LogWarning("Application or Resources is null during preview restoration");
    //                             return;
    //                         }
    //
    //                         // Remove all theme-related resources first
    //                         var keysToRemove = app.Resources.Keys
    //                             .OfType<string>()
    //                             .Where(k => k.EndsWith("Brush") || k.EndsWith("Color"))
    //                             .ToList();
    //
    //                         foreach (var key in keysToRemove)
    //                         {
    //                             try
    //                             {
    //                                 app.Resources.Remove(key);
    //                             }
    //                             catch (Exception ex)
    //                             {
    //                                 _logger.LogWarning(ex, "Failed to remove resource key: {Key}", key);
    //                             }
    //                         }
    //
    //                         // Restore original resources if we have them
    //                         if (_originalResources != null)
    //                         {
    //                             foreach (var kvp in _originalResources)
    //                             {
    //                                 try
    //                                 {
    //                                     if (kvp.Value != null)
    //                                     {
    //                                         app.Resources[kvp.Key] = kvp.Value;
    //                                     }
    //                                 }
    //                                 catch (Exception ex)
    //                                 {
    //                                     _logger.LogWarning(ex, "Failed to restore resource: {Key}", kvp.Key);
    //                                 }
    //                             }
    //                         }
    //
    //                         _logger.LogDebug("Restored original theme resources from memory backup");
    //                     }
    //                     catch (Exception ex)
    //                     {
    //                         _logger.LogError(ex, "Error in UI thread during resource restoration");
    //                     }
    //                 });
    //             }
    //             catch (Exception ex)
    //             {
    //                 _logger.LogError(ex, "Error in background thread during preview restoration");
    //             }
    //         });
    //
    //         IsPreviewActive = false;
    //         _originalThemeBeforePreview = null;
    //         _originalResources = null;
    //         StatusMessage = "Preview stopped - original theme restored";
    //
    //         _logger.LogInformation("Theme preview stopped, resources restored from memory");
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Failed to stop theme preview");
    //         await _errorHandler.HandleExceptionAsync(ex, "Failed to stop preview", "Theme Editor");
    //         StatusMessage = "Failed to stop preview";
    //         
    //         // Ensure we clean up state even if restoration failed
    //         IsPreviewActive = false;
    //         _originalThemeBeforePreview = null;
    //         _originalResources = null;
    //     }
    // }
    
    /// <summary>
    /// ULTIMATE SIMPLE FIX: Just remove preview and force refresh - no backup/restore complexity
    /// </summary>
    
    private async Task StopPreviewAsync()
    {
        try
        {
            StatusMessage = "Restoring original theme...";
        
            var app = Application.Current;
            if (app?.Resources != null)
            {
                // Remove our preview dictionary
                for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                {
                    var dict = app.Resources.MergedDictionaries[i];
                    if (dict is ResourceDictionary resourceDict && 
                        resourceDict.ContainsKey("_ThemeEditorGenerated"))
                    {
                        app.Resources.MergedDictionaries.RemoveAt(i);
                        _logger.LogDebug("Removed theme dictionary at index {Index}", i);
                        break;
                    }
                }
            }
        
            // Force resource system to refresh by adding/removing empty dictionary
            // This forces Avalonia to re-evaluate all DynamicResource bindings
            var tempDict = new ResourceDictionary();
            app.Resources.MergedDictionaries.Add(tempDict);
            app.Resources.MergedDictionaries.Remove(tempDict);
        
            // Minimal delay to let resources settle
            await Task.Delay(50);
        
            IsPreviewActive = false;
            StatusMessage = "Preview stopped - original theme restored";

            _logger.LogInformation("Theme preview stopped - simple refresh approach");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop theme preview");
            StatusMessage = "Failed to stop preview";
            IsPreviewActive = false;
        }
    }
    
    /// <summary>
/// Force Avalonia to refresh and re-resolve all StyleInclude resources
/// </summary>
private async Task ForceStyleIncludeRefresh()
{
    await Task.Run(() =>
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var app = Application.Current;
                if (app?.Styles == null) return;
                
                _logger.LogDebug("Forcing StyleInclude refresh to re-resolve resources...");
                
                // Find the ORIGINAL theme (whatever was loaded before preview)
                string? originalThemePath = null;
                if (_originalThemeBeforePreview.HasValue)
                {
                    var originalThemeDefinition = _themeService.AvailableThemes
                        .FirstOrDefault(t => t.Type == _originalThemeBeforePreview.Value);
                    originalThemePath = originalThemeDefinition?.ResourcePath;
                }
                
                var originalThemeStyle = app.Styles
                    .OfType<StyleInclude>()
                    .FirstOrDefault(s => originalThemePath != null && s.Source?.ToString().Contains(originalThemePath.Split('/').Last()) == true);
                
                if (originalThemeStyle != null)
                {
                    var sourceUri = originalThemeStyle.Source;
                    var index = app.Styles.IndexOf(originalThemeStyle);
                    
                    // Remove and re-add to force resource re-resolution
                    app.Styles.RemoveAt(index);
                    
                    var newStyleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                    {
                        Source = sourceUri
                    };
                    
                    app.Styles.Insert(index, newStyleInclude);
                    
                    _logger.LogDebug("✅ Original theme refreshed at index {Index}: {Theme}", index, _originalThemeBeforePreview);
                }
                else
                {
                    _logger.LogWarning("❌ Original theme StyleInclude not found: {Theme}", _originalThemeBeforePreview);
                }
                
                // Also refresh ModernDesignClasses
                var modernDesignClasses = app.Styles
                    .OfType<StyleInclude>()
                    .FirstOrDefault(s => s.Source?.ToString().Contains("ModernDesignClasses") == true);
                
                if (modernDesignClasses != null)
                {
                    var sourceUri = modernDesignClasses.Source;
                    var index = app.Styles.IndexOf(modernDesignClasses);
                    
                    app.Styles.RemoveAt(index);
                    
                    var newStyleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                    {
                        Source = sourceUri
                    };
                    
                    app.Styles.Insert(index, newStyleInclude);
                    
                    _logger.LogDebug("✅ ModernDesignClasses refreshed at index {Index}", index);
                }
                else
                {
                    // Add ModernDesignClasses if missing
                    var styleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                    {
                        Source = new Uri("avares://ProxyStudio/Themes/Common/ModernDesignClasses.axaml")
                    };
                    
                    app.Styles.Add(styleInclude);
                    _logger.LogInformation("✅ Added missing ModernDesignClasses");
                }
                
                // Force a complete resource system refresh
                ForceResourceRefresh();
                
                _logger.LogDebug("StyleInclude refresh completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to force StyleInclude refresh");
            }
        });
    });
}
    
    /// <summary>
    /// Restore the original theme StyleInclude
    /// </summary>
    private async Task RestoreOriginalThemeStyleInclude()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Styles == null) return;
                    
                    // 1. Remove preview theme StyleIncludes
                    var previewThemes = app.Styles
                        .OfType<StyleInclude>()
                        .Where(s => s.Source?.ToString().Contains("PreviewTheme") == true ||
                                   s.Source?.ToString().Contains("temp") == true)
                        .ToList();
                    
                    foreach (var theme in previewThemes)
                    {
                        app.Styles.Remove(theme);
                        _logger.LogDebug("Removed preview theme: {Source}", theme.Source);
                    }
                    
                    // 2. Restore original theme if we have the path
                    if (!string.IsNullOrEmpty(_originalThemeStylePath))
                    {
                        var originalStyleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                        {
                            Source = new Uri(_originalThemeStylePath)
                        };
                        
                        // Insert before ModernDesignClasses
                        var modernDesignIndex = app.Styles
                            .OfType<StyleInclude>()
                            .Select((style, index) => new { style, index })
                            .FirstOrDefault(x => x.style.Source?.ToString().Contains("ModernDesign") == true)?.index;
                        
                        if (modernDesignIndex.HasValue)
                        {
                            app.Styles.Insert(modernDesignIndex.Value, originalStyleInclude);
                            _logger.LogDebug("Restored original theme before ModernDesignClasses at index {Index}", modernDesignIndex.Value);
                        }
                        else
                        {
                            app.Styles.Add(originalStyleInclude);
                            _logger.LogDebug("Restored original theme at end of styles");
                        }
                        
                        _logger.LogInformation("Original theme StyleInclude restored: {Path}", _originalThemeStylePath);
                    }
                    else
                    {
                        _logger.LogWarning("No original theme path to restore, using theme service fallback");
                        
                        // Fallback: use theme service
                        if (_originalThemeBeforePreview.HasValue)
                        {
                            Task.Run(async () =>
                            {
                                try
                                {
                                    await _themeService.ApplyThemeAsync(_originalThemeBeforePreview.Value);
                                    _logger.LogInformation("Restored theme via service: {Theme}", _originalThemeBeforePreview.Value);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to restore theme via service");
                                }
                            });
                        }
                    }
                    
                    // 3. Clear backup data
                    _originalThemeStylePath = null;
                    _originalThemeBeforePreview = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore original theme StyleInclude");
                }
            });
        });
    }
    
   /// <summary>
    /// CORRECTED: Restore original style resources without calling theme service
    /// </summary>
    private async Task RestoreOriginalStyleResources()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Resources == null || _originalResources == null) 
                    {
                        _logger.LogWarning("Cannot restore resources - app.Resources or backup is null");
                        
                        // FALLBACK: If we have no backup, re-apply the original theme via theme service
                        if (_originalThemeBeforePreview.HasValue)
                        {
                            _logger.LogInformation("No backup found, re-applying original theme via service");
                            // This is risky but better than white background
                            Task.Run(async () => {
                                try
                                {
                                    await _themeService.ApplyThemeAsync(_originalThemeBeforePreview.Value);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to re-apply theme via service");
                                }
                            });
                        }
                        return;
                    }
                    
                    _logger.LogDebug("Restoring {Count} resources directly", _originalResources.Count);
                    
                    // Restore each backed up resource directly to Application.Resources
                    int restoredCount = 0;
                    foreach (var kvp in _originalResources)
                    {
                        try
                        {
                            if (kvp.Value != null)
                            {
                                app.Resources[kvp.Key] = kvp.Value;
                                restoredCount++;
                                
                                // Debug important ones
                                if (kvp.Key.Contains("Primary") || kvp.Key.Contains("Surface") || kvp.Key.Contains("Background"))
                                {
                                    if (kvp.Value is SolidColorBrush brush)
                                    {
                                        _logger.LogDebug("✅ Restored {Key}: {Color}", kvp.Key, brush.Color);
                                    }
                                    else
                                    {
                                        _logger.LogDebug("✅ Restored {Key}: {Type}", kvp.Key, kvp.Value.GetType().Name);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to restore resource: {Key}", kvp.Key);
                        }
                    }
                    
                    _logger.LogInformation("Successfully restored {Count}/{Total} resources", restoredCount, _originalResources.Count);
                    
                    // Clear backup
                    _originalResources.Clear();
                    _originalResources = null;
                    _originalThemeBeforePreview = null;
                    
                    _logger.LogDebug("Resources restored directly to Application.Resources");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore original style resources");
                }
            });
        });
    } 
    
    // <summary>
    /// NEW: Restore resources directly without calling theme service
    /// This prevents the white background issue
    /// </summary>
    private async Task RestoreResourcesDirectly()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Resources == null || _originalResources == null) 
                    {
                        _logger.LogWarning("Cannot restore resources - app.Resources or backup is null");
                        return;
                    }
                    
                    _logger.LogDebug("Restoring {Count} resources directly", _originalResources.Count);
                    
                    // Restore each backed up resource directly to Application.Resources
                    foreach (var kvp in _originalResources)
                    {
                        try
                        {
                            if (kvp.Value != null)
                            {
                                app.Resources[kvp.Key] = kvp.Value;
                                
                                // Debug important ones
                                if (kvp.Key.Contains("Primary") || kvp.Key.Contains("Surface") || kvp.Key.Contains("Background"))
                                {
                                    if (kvp.Value is SolidColorBrush brush)
                                    {
                                        _logger.LogDebug("Restored {Key}: {Color}", kvp.Key, brush.Color);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to restore resource: {Key}", kvp.Key);
                        }
                    }
                    
                    // Clear backup
                    _originalResources.Clear();
                    _originalResources = null;
                    _originalThemeBeforePreview = null;
                    
                    _logger.LogDebug("Resources restored directly to Application.Resources");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore resources directly");
                }
            });
        });
    }
    
    /// <summary>
    /// Force complete style refresh - enhanced approach
    /// </summary>
    private async Task ForceCompleteStyleRefresh()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Styles == null) return;
                    
                    // 1. Force resource refresh
                    ForceResourceRefresh();
                    
                    // 2. Refresh ModernDesignClasses
                    RefreshModernDesignClasses();
                    
                    // 3. Force garbage collection to ensure resources are updated
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    
                    _logger.LogDebug("Complete style refresh performed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to force complete style refresh");
                }
            });
        });
    }
    
    /// <summary>
    /// Enhanced ModernDesignClasses refresh
    /// </summary>
    private void RefreshModernDesignClasses()
    {
        try
        {
            var app = Application.Current;
            if (app?.Styles == null) return;
            
            // Find ModernDesignClasses
            var modernDesignClasses = app.Styles
                .OfType<StyleInclude>()
                .FirstOrDefault(s => s.Source?.ToString().Contains("ModernDesignClasses") == true);
            
            if (modernDesignClasses != null)
            {
                var sourceUri = modernDesignClasses.Source;
                var index = app.Styles.IndexOf(modernDesignClasses);
                
                // Remove and re-add to force refresh
                app.Styles.RemoveAt(index);
                
                var newStyleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                {
                    Source = sourceUri
                };
                
                app.Styles.Insert(index, newStyleInclude);
                
                _logger.LogDebug("ModernDesignClasses refreshed at index {Index}", index);
            }
            else
            {
                _logger.LogWarning("ModernDesignClasses not found for refresh");
                
                // Try to add it manually
                try
                {
                    var styleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                    {
                        Source = new Uri("avares://ProxyStudio/Themes/Common/ModernDesignClasses.axaml")
                    };
                    
                    app.Styles.Add(styleInclude);
                    _logger.LogInformation("✅ Added missing ModernDesignClasses");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to add missing ModernDesignClasses");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh ModernDesignClasses");
        }
    }
    
    
    
    /// <summary>
    /// Debug resource availability for troubleshooting
    /// </summary>
    private void DebugResourceAvailability()
    {
        try
        {
            var app = Application.Current;
            if (app?.Resources == null) return;
            
            var testKeys = new[] { "PrimaryBrush", "PrimaryHoverBrush", "SecondaryBrush", "SecondaryHoverBrush", "SurfaceBrush", "BackgroundBrush" };
            
            _logger.LogDebug("=== RESOURCE AVAILABILITY DEBUG ===");
            
            foreach (var key in testKeys)
            {
                var found = app.Resources.TryGetValue(key, out var value);
                if (found && value is SolidColorBrush brush)
                {
                    _logger.LogDebug("✅ {Key}: {Color}", key, brush.Color);
                }
                else
                {
                    _logger.LogWarning("❌ {Key}: NOT FOUND", key);
                }
            }
            
            _logger.LogDebug("MergedDictionaries count: {Count}", app.Resources.MergedDictionaries.Count);
            
            // Check each merged dictionary
            for (int i = 0; i < app.Resources.MergedDictionaries.Count; i++)
            {
                var dict = app.Resources.MergedDictionaries[i];
                if (dict is ResourceDictionary resourceDict)
                {
                    var markerKeys = resourceDict.Keys.OfType<string>()
                        .Where(k => k.StartsWith("_ThemeEditor"))
                        .ToList();
                    
                    if (markerKeys.Any())
                    {
                        _logger.LogDebug("Dictionary[{Index}]: ThemeEditor dictionary with {Count} keys", 
                            i, resourceDict.Count);
                    }
                    else
                    {
                        _logger.LogDebug("Dictionary[{Index}]: Standard dictionary with {Count} keys", 
                            i, resourceDict.Count);
                    }
                }
                else
                {
                    _logger.LogDebug("Dictionary[{Index}]: IResourceProvider (type: {Type})", 
                        i, dict.GetType().Name);
                }
            }
            
            _logger.LogDebug("=== END RESOURCE DEBUG ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to debug resource availability");
        }
    }
    
    /// <summary>
    /// Restore original theme state
    /// </summary>
    private async Task RestoreOriginalThemeState()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    // 1. Restore original theme if we had one
                    if (_originalThemeBeforePreview.HasValue)
                    {
                        await _themeService.ApplyThemeAsync(_originalThemeBeforePreview.Value);
                        _originalThemeBeforePreview = null;
                    }
                    
                    // 2. Clear backup resources
                    _originalResources?.Clear();
                    _originalResources = null;
                    
                    _logger.LogDebug("Original theme state restored");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore original theme state");
                }
            });
        });
    }

    
    /// <summary>
    /// Remove our custom theme dictionary
    /// </summary>
    private async Task RemoveCustomThemeDictionary()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Resources == null) return;
                    
                    // Remove our custom dictionary if it exists
                    if (_customThemeDictionary != null)
                    {
                        app.Resources.MergedDictionaries.Remove(_customThemeDictionary);
                        _customThemeDictionary = null;
                        _logger.LogDebug("Removed custom theme dictionary");
                    }
                    
                    // Also remove any marked dictionaries
                    for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                    {
                        var dict = app.Resources.MergedDictionaries[i];
                        if (dict is ResourceDictionary resourceDict && 
                            (resourceDict.ContainsKey("_ThemeEditorCustom") || resourceDict.ContainsKey("_ThemeEditorGenerated")))
                        {
                            app.Resources.MergedDictionaries.RemoveAt(i);
                            _logger.LogDebug("Removed marked theme dictionary at index {Index}", i);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove custom theme dictionary");
                }
            });
        });
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
/// ORIGINAL WORKING METHOD: Enhanced memory theme application with proper resource handling
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

                // Remove any existing theme dictionary we might have added
                for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                {
                    if (app.Resources.MergedDictionaries[i] is ResourceDictionary dict && 
                        dict.ContainsKey("_ThemeEditorGenerated"))
                    {
                        app.Resources.MergedDictionaries.RemoveAt(i);
                        _logger.LogDebug("Removed existing theme dictionary at index {Index}", i);
                        break;
                    }
                }

                // Add a marker to identify our theme dictionary
                themeDict["_ThemeEditorGenerated"] = true;

                // Add the new theme dictionary to merged dictionaries
                // This approach ensures proper DynamicResource change notifications
                app.Resources.MergedDictionaries.Add(themeDict);

                _logger.LogDebug("Applied theme colors via MergedDictionaries");
                
                // Refresh ModernDesignClasses
                RefreshModernDesignClassesAfterThemeChange();

                // Debug the current state
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
    /// Backup current theme state - comprehensive approach
    /// </summary>
    private async Task BackupCurrentThemeState()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Resources == null) return;
                    
                    // Store the original theme before preview (but don't use it for restoration)
                    _originalThemeBeforePreview = _themeService.CurrentTheme;
                    
                    // Create comprehensive backup of ALL current resources
                    _originalResources = new Dictionary<string, object>();
                    
                    // First, backup from Application.Resources itself
                    foreach (var key in app.Resources.Keys.OfType<string>())
                    {
                        if (app.Resources.TryGetValue(key, out var value) && value != null)
                        {
                            _originalResources[key] = value;
                        }
                    }
                    
                    // Then backup from each MergedDictionary
                    foreach (var dict in app.Resources.MergedDictionaries)
                    {
                        if (dict is ResourceDictionary resourceDict)
                        {
                            foreach (var key in resourceDict.Keys.OfType<string>())
                            {
                                if (resourceDict.TryGetValue(key, out var value) && value != null)
                                {
                                    // Only backup if we don't already have it (higher priority wins)
                                    if (!_originalResources.ContainsKey(key))
                                    {
                                        _originalResources[key] = value;
                                    }
                                }
                            }
                        }
                    }
                    
                    _logger.LogDebug("Backed up {Count} original resources", _originalResources.Count);
                    
                    // Debug what we backed up
                    var importantKeys = new[] { "PrimaryBrush", "SecondaryBrush", "SurfaceBrush", "BackgroundBrush" };
                    foreach (var key in importantKeys)
                    {
                        if (_originalResources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
                        {
                            _logger.LogDebug("Backed up {Key}: {Color}", key, brush.Color);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backup current theme state");
                }
            });
        });
    }


/// <summary>
    /// CORRECTED: Backup resources from StyleInclude themes, not just Application.Resources
    /// </summary>
    private async Task BackupCurrentStyleResources()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Styles == null) return;
                    
                    // Store the original theme before preview
                    _originalThemeBeforePreview = _themeService.CurrentTheme;
                    
                    // Create comprehensive backup of resources from ALL SOURCES
                    _originalResources = new Dictionary<string, object>();
                    
                    _logger.LogDebug("Starting backup from all resource sources...");
                    
                    // 1. Backup from Application.Resources itself
                    foreach (var key in app.Resources.Keys.OfType<string>())
                    {
                        if (app.Resources.TryGetValue(key, out var value) && value != null)
                        {
                            _originalResources[key] = value;
                        }
                    }
                    _logger.LogDebug("Backed up {Count} from Application.Resources", _originalResources.Count);
                    
                    // 2. Backup from each MergedDictionary
                    foreach (var dict in app.Resources.MergedDictionaries)
                    {
                        if (dict is ResourceDictionary resourceDict)
                        {
                            foreach (var key in resourceDict.Keys.OfType<string>())
                            {
                                if (resourceDict.TryGetValue(key, out var value) && value != null)
                                {
                                    if (!_originalResources.ContainsKey(key))
                                    {
                                        _originalResources[key] = value;
                                    }
                                }
                            }
                        }
                    }
                    _logger.LogDebug("After MergedDictionaries: {Count} resources", _originalResources.Count);
                    
                    // 3. NEW: Backup from StyleInclude resources (like DarkProfessional.axaml)
                    foreach (var style in app.Styles.OfType<StyleInclude>())
                    {
                        try
                        {
                            // Try to access the StyleInclude's resources
                            var styleSource = style.Source?.ToString();
                            if (styleSource?.Contains("DarkProfessional") == true ||
                                styleSource?.Contains("Themes/") == true)
                            {
                                _logger.LogDebug("Found theme StyleInclude: {Source}", styleSource);
                                
                                // The StyleInclude itself might have loaded resources into the resource system
                                // We need to query what resources are currently resolved
                                var themeKeys = new[]
                                {
                                    "PrimaryBrush", "PrimaryColor", "SecondaryBrush", "SecondaryColor",
                                    "SurfaceBrush", "SurfaceColor", "BackgroundBrush", "BackgroundColor",
                                    "BorderBrush", "BorderColor", "TextPrimaryBrush", "TextSecondaryBrush",
                                    "SuccessBrush", "WarningBrush", "ErrorBrush", "InfoBrush"
                                };
                                
                                foreach (var key in themeKeys)
                                {
                                    if (app.Resources.TryGetValue(key, out var themeValue) && themeValue != null)
                                    {
                                        if (!_originalResources.ContainsKey(key))
                                        {
                                            _originalResources[key] = themeValue;
                                            _logger.LogDebug("Backed up theme resource: {Key}", key);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to backup from StyleInclude: {Source}", style.Source);
                        }
                    }
                    
                    _logger.LogDebug("Final backup count: {Count} original resources", _originalResources.Count);
                    
                    // Debug what we backed up
                    var importantKeys = new[] { "PrimaryBrush", "SecondaryBrush", "SurfaceBrush", "BackgroundBrush" };
                    foreach (var key in importantKeys)
                    {
                        if (_originalResources.TryGetValue(key, out var value))
                        {
                            if (value is SolidColorBrush brush)
                            {
                                _logger.LogDebug("✅ Backed up {Key}: {Color}", key, brush.Color);
                            }
                            else
                            {
                                _logger.LogDebug("✅ Backed up {Key}: {Type}", key, value.GetType().Name);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("❌ Failed to backup {Key}", key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backup current style resources");
                }
            });
        });
    }

    /// <summary>
    /// Backup the current theme StyleInclude path
    /// </summary>
    private void BackupCurrentThemeStyleInclude()
    {
        try
        {
            var app = Application.Current;
            if (app?.Styles == null) return;
            
            // Find the current theme StyleInclude
            var themeStyleInclude = app.Styles
                .OfType<StyleInclude>()
                .FirstOrDefault(s => s.Source?.ToString().Contains("Themes/") == true && 
                                     !s.Source.ToString().Contains("ModernDesign") == true);
            
            if (themeStyleInclude != null)
            {
                _originalThemeStylePath = themeStyleInclude.Source?.ToString();
                _logger.LogDebug("Backed up original theme StyleInclude: {Path}", _originalThemeStylePath);
            }
            else
            {
                _logger.LogWarning("No theme StyleInclude found to backup");
            }
            
            // Store the original theme before preview
            _originalThemeBeforePreview = _themeService.CurrentTheme;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backup current theme StyleInclude");
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

        // Find ModernDesignClasses
        var modernDesignClasses = app.Styles
            .OfType<StyleInclude>()
            .FirstOrDefault(s => s.Source?.ToString().Contains("ModernDesignClasses") == true);

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
            _logger.LogWarning("❌ ModernDesignClasses not found in app styles");

            // Try to manually add ModernDesignClasses
            try
            {
                var styleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                {
                    Source = new Uri("avares://ProxyStudio/Themes/Common/ModernDesignClasses.axaml")
                };

                app.Styles.Add(styleInclude);

                _logger.LogInformation("✅ Manually added ModernDesignClasses to application styles");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to manually add ModernDesignClasses");
            }
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

    /// <summary>
    /// Updated Save method that ensures proper theme structure
    /// </summary>
    private async Task SaveThemeAsync()
    {
        try
        {
            StatusMessage = "Saving theme...";
        
            // Generate theme using the SAME method as preview
            var themeXaml = GenerateFullThemeXaml();
            var fileName = $"{ThemeName.Replace(" ", "_")}.axaml";
        
            // Save to themes directory
            var themesDir = _themeService.GetThemesDirectory();
            var filePath = Path.Combine(themesDir, fileName);

            Directory.CreateDirectory(themesDir);
            await File.WriteAllTextAsync(filePath, themeXaml);

            // ✅ CRITICAL: Apply the saved theme to test it works
            await _themeService.ApplyCustomThemeAsync(themeXaml, ThemeName);

            StatusMessage = "Theme saved and applied successfully";
            CanSaveTheme = false;

            await _errorHandler.ShowErrorAsync("Theme Saved", 
                $"Theme '{ThemeName}' saved and applied successfully!\n\nLocation: {filePath}", 
                ErrorSeverity.Information);
            
            _logger.LogInformation("Theme saved and applied: {FilePath}", filePath);
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

/// <summary>
/// FIXED: Generate complete theme XAML with all derived colors and correct resource references
/// </summary>
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
    sb.AppendLine($"  <!-- COMPLETE THEME: Matches preview exactly with all derived colors -->");
    sb.AppendLine();

    sb.AppendLine("  <Styles.Resources>");
    sb.AppendLine("    <ResourceDictionary>");
    sb.AppendLine("      <ResourceDictionary.ThemeDictionaries>");
    sb.AppendLine("        <ResourceDictionary x:Key=\"Dark\">");
    sb.AppendLine();

    // ===== FOUNDATION COLORS =====
    sb.AppendLine("          <!-- ===== FOUNDATION COLORS ===== -->");
    foreach (var color in FoundationColors)
    {
        var colorName = color.Name.Replace(" ", "");
        sb.AppendLine($"          <!-- {color.Name}: {color.Description} -->");
        sb.AppendLine($"          <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
        // ✅ FIXED: Use DynamicResource instead of StaticResource in ThemeDictionaries
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\"/>");
        
        // Generate hover state (15% darker)
        var baseColor = Color.Parse(color.HexValue);
        var hoverColor = DarkenColor(baseColor, 0.15f);
        sb.AppendLine($"          <Color x:Key=\"{colorName}HoverColor\">{hoverColor}</Color>");
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}HoverBrush\" Color=\"{{DynamicResource {colorName}HoverColor}}\"/>");
        sb.AppendLine();
    }

    // ===== SEMANTIC COLORS =====
    sb.AppendLine("          <!-- ===== SEMANTIC COLORS ===== -->");
    foreach (var color in SemanticColors)
    {
        var colorName = color.Name.Replace(" ", "");
        sb.AppendLine($"          <!-- {color.Name}: {color.Description} -->");
        sb.AppendLine($"          <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\"/>");
        
        // Generate hover and light variants
        var baseColor = Color.Parse(color.HexValue);
        var hoverColor = DarkenColor(baseColor, 0.15f);
        var lightColor = LightenColor(baseColor, 0.8f);
        
        sb.AppendLine($"          <Color x:Key=\"{colorName}HoverColor\">{hoverColor}</Color>");
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}HoverBrush\" Color=\"{{DynamicResource {colorName}HoverColor}}\"/>");
        sb.AppendLine($"          <Color x:Key=\"{colorName}LightColor\">{lightColor}</Color>");
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}LightBrush\" Color=\"{{DynamicResource {colorName}LightColor}}\"/>");
        sb.AppendLine();
    }

    // ===== SURFACE COLORS =====
    sb.AppendLine("          <!-- ===== SURFACE COLORS ===== -->");
    foreach (var color in SurfaceColors)
    {
        var colorName = color.Name.Replace(" ", "");
        sb.AppendLine($"          <!-- {color.Name}: {color.Description} -->");
        sb.AppendLine($"          <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\"/>");
        
        // Add subtle hover for surfaces
        if (colorName == "Surface")
        {
            var baseColor = Color.Parse(color.HexValue);
            var hoverColor = DarkenColor(baseColor, 0.05f); // Subtle hover
            sb.AppendLine($"          <Color x:Key=\"{colorName}HoverColor\">{hoverColor}</Color>");
            sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}HoverBrush\" Color=\"{{DynamicResource {colorName}HoverColor}}\"/>");
        }
        sb.AppendLine();
    }

    // ===== TEXT COLORS =====
    sb.AppendLine("          <!-- ===== TEXT COLORS ===== -->");
    foreach (var color in TextColors)
    {
        var colorName = color.Name.Replace(" ", "");
        sb.AppendLine($"          <!-- {color.Name}: {color.Description} -->");
        sb.AppendLine($"          <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\"/>");
    }
    sb.AppendLine();

    // ===== CONTRASTING TEXT COLORS =====
    sb.AppendLine("          <!-- ===== AUTO-GENERATED CONTRASTING TEXT ===== -->");
    GenerateContrastingTextInXaml(sb, "Primary");
    GenerateContrastingTextInXaml(sb, "Secondary");
    sb.AppendLine();

    sb.AppendLine("        </ResourceDictionary>");
    sb.AppendLine();
    
    // ===== LIGHT THEME VARIANT =====
    sb.AppendLine("        <!-- Light theme variant (optional) -->");
    sb.AppendLine("        <ResourceDictionary x:Key=\"Light\">");
    sb.AppendLine("          <!-- TODO: Implement proper light theme derivation -->");
    sb.AppendLine("          <!-- For now, using same colors - replace with light variants -->");
    GenerateLightThemeVariant(sb);
    sb.AppendLine("        </ResourceDictionary>");

    sb.AppendLine("      </ResourceDictionary.ThemeDictionaries>");
    sb.AppendLine("    </ResourceDictionary>");
    sb.AppendLine("  </Styles.Resources>");
    sb.AppendLine("</Styles>");

    return sb.ToString();
}

// ===== NEW HELPER METHODS FOR COMPLETE THEME GENERATION =====

/// <summary>
/// Generate hover states in XAML format (15% darker)
/// </summary>
private void GenerateHoverColorsInXaml(StringBuilder sb, ObservableCollection<ColorProperty> colors)
{
    foreach (var color in colors)
    {
        try
        {
            var colorName = color.Name.Replace(" ", "");
            var baseColor = Color.Parse(color.HexValue);
            var hoverColor = DarkenColor(baseColor, 0.15f);
            
            sb.AppendLine($"          <Color x:Key=\"{colorName}HoverColor\">{hoverColor}</Color>");
            sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}HoverBrush\" Color=\"{{StaticResource {colorName}HoverColor}}\"/>");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate hover color for {Name}", color.Name);
        }
    }
}

/// <summary>
/// Generate light variants for semantic colors (80% lighter for backgrounds)
/// </summary>
private void GenerateStatusLightVariantsInXaml(StringBuilder sb, ObservableCollection<ColorProperty> semanticColors)
{
    foreach (var color in semanticColors)
    {
        try
        {
            var colorName = color.Name.Replace(" ", "");
            var baseColor = Color.Parse(color.HexValue);
            
            // For dark themes, we want much lighter versions for status backgrounds
            var lightColor = LightenColor(baseColor, 0.8f);
            
            sb.AppendLine($"          <Color x:Key=\"{colorName}LightColor\">{lightColor}</Color>");
            sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}LightBrush\" Color=\"{{StaticResource {colorName}LightColor}}\"/>");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate light variant for {Name}", color.Name);
        }
    }
}

// <summary>
/// Generate contrasting text colors in XAML for export
/// </summary>
private void GenerateContrastingTextInXaml(StringBuilder sb, string backgroundColorName)
{
    try
    {
        var colorProperty = FoundationColors.FirstOrDefault(c => 
            c.Name.Equals(backgroundColorName, StringComparison.OrdinalIgnoreCase));
        
        if (colorProperty != null)
        {
            var bgColor = Color.Parse(colorProperty.HexValue);
            var textColor = GetContrastingTextColor(bgColor);
            
            sb.AppendLine($"          <!-- Contrasting text for {backgroundColorName} background -->");
            sb.AppendLine($"          <Color x:Key=\"TextOn{backgroundColorName}Color\">{textColor}</Color>");
            sb.AppendLine($"          <SolidColorBrush x:Key=\"TextOn{backgroundColorName}Brush\" Color=\"{{DynamicResource TextOn{backgroundColorName}Color}}\"/>");
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to generate contrasting text for {BackgroundName}", backgroundColorName);
    }
}

/// <summary>
/// Generate surface variations (subtle hover states)
/// </summary>
private void GenerateSurfaceVariationsInXaml(StringBuilder sb)
{
    try
    {
        var surfaceColor = SurfaceColors.FirstOrDefault(c => c.Name == "Surface");
        if (surfaceColor != null)
        {
            var baseColor = Color.Parse(surfaceColor.HexValue);
            var hoverColor = DarkenColor(baseColor, 0.05f); // Subtle hover for surfaces
            
            sb.AppendLine($"          <Color x:Key=\"SurfaceHoverColor\">{hoverColor}</Color>");
            sb.AppendLine($"          <SolidColorBrush x:Key=\"SurfaceHoverBrush\" Color=\"{{StaticResource SurfaceHoverColor}}\"/>");
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to generate surface variations");
    }
}

/// <summary>
/// Generate light theme variant - simplified version
/// </summary>
private void GenerateLightThemeVariant(StringBuilder sb)
{
    // Generate light theme versions of all colors
    sb.AppendLine("          <!-- Foundation Colors - Light Theme -->");
    foreach (var color in FoundationColors)
    {
        var colorName = color.Name.Replace(" ", "");
        // For light theme, you might want to derive lighter/different colors
        // For now, using the same colors - you can enhance this later
        sb.AppendLine($"          <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\"/>");
    }
    
    // Add semantic colors for light theme
    sb.AppendLine("          <!-- Semantic Colors - Light Theme -->");
    foreach (var color in SemanticColors)
    {
        var colorName = color.Name.Replace(" ", "");
        sb.AppendLine($"          <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\"/>");
    }
    
    // Add surface and text colors
    sb.AppendLine("          <!-- Surface & Text Colors - Light Theme -->");
    foreach (var color in SurfaceColors.Concat(TextColors))
    {
        var colorName = color.Name.Replace(" ", "");
        sb.AppendLine($"          <Color x:Key=\"{colorName}Color\">{color.HexValue}</Color>");
        sb.AppendLine($"          <SolidColorBrush x:Key=\"{colorName}Brush\" Color=\"{{DynamicResource {colorName}Color}}\"/>");
    }
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