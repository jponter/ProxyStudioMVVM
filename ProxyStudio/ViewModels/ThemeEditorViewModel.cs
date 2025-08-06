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

    public SolidColorBrush ColorBrush => new(Color.Parse(HexValue));

    public ColorProperty(string name, string hexValue, string description)
    {
        _name = name;
        _hexValue = hexValue;
        _description = description;
    }

    partial void OnHexValueChanged(string value)
    {
        // Validate hex color format
        if (IsValidHexColor(value)) 
        {
            OnPropertyChanged(nameof(ColorBrush));
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
    private async Task ApplyThemeInMemory()
    {
        await Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var app = Application.Current;
                if (app?.Resources == null) return;

                // Apply new color resources directly to Application.Resources
                ApplyColorsToResources(app.Resources, FoundationColors, "Foundation");
                ApplyColorsToResources(app.Resources, SemanticColors, "Semantic");
                ApplyColorsToResources(app.Resources, SurfaceColors, "Surface");
                ApplyColorsToResources(app.Resources, TextColors, "Text");

                // Apply derived colors
                ApplyDerivedColorsToResources(app.Resources);

                _logger.LogDebug("Applied theme colors directly to Application.Resources in memory");
            });
        });
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

                // Add both color and brush resources
                resources[colorKey] = color;
                resources[brushKey] = brush;

                _logger.LogTrace("Applied {Category} color: {Name} = {Value}", category, colorProp.Name, colorProp.HexValue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse color {Name}: {Value}", colorProp.Name, colorProp.HexValue);
            }
        }
    }

    private void ApplyDerivedColorsToResources(IResourceDictionary resources)
    {
        try
        {
            // Generate hover states (15% darker)
            if (resources.TryGetValue("PrimaryColor", out var primaryColorObj) && primaryColorObj is Color primaryColor)
            {
                var primaryHover = DarkenColor(primaryColor, 0.15f);
                resources["PrimaryHoverColor"] = primaryHover;
                resources["PrimaryHoverBrush"] = new SolidColorBrush(primaryHover);
            }

            if (resources.TryGetValue("SecondaryColor", out var secondaryColorObj) && secondaryColorObj is Color secondaryColor)
            {
                var secondaryHover = DarkenColor(secondaryColor, 0.15f);
                resources["SecondaryHoverColor"] = secondaryHover;
                resources["SecondaryHoverBrush"] = new SolidColorBrush(secondaryHover);
            }

            // Generate light variants for status backgrounds
            GenerateStatusLightVariants(resources, "Success");
            GenerateStatusLightVariants(resources, "Warning");
            GenerateStatusLightVariants(resources, "Error");
            GenerateStatusLightVariants(resources, "Info");

            // Generate contrasting text on primary
            if (resources.TryGetValue("PrimaryColor", out var primaryForTextObj) && primaryForTextObj is Color primaryForText)
            {
                var textOnPrimary = GetContrastingTextColor(primaryForText);
                resources["TextOnPrimaryColor"] = textOnPrimary;
                resources["TextOnPrimaryBrush"] = new SolidColorBrush(textOnPrimary);
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate derived colors");
        }
    }

    private void GenerateStatusLightVariants(IResourceDictionary resources, string statusName)
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