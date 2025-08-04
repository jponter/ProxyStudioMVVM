using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;
using ProxyStudio.Services;

namespace ProxyStudio.ViewModels
{
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
    /// ViewModel for the Theme Editor that allows users to create and customize themes
    /// </summary>
    public partial class ThemeEditorViewModel : ViewModelBase
    {
        private readonly IThemeService _themeService;
        private readonly IConfigManager _configManager;
        private readonly ILogger<ThemeEditorViewModel> _logger;
        private readonly IErrorHandlingService _errorHandler;

        [ObservableProperty] private string _themeName = "My Custom Theme";
        [ObservableProperty] private string _themeDescription = "A custom theme created with ProxyStudio Theme Editor";
        [ObservableProperty] private string _themeAuthor = Environment.UserName;
        [ObservableProperty] private string _themeVersion = "1.0.0";
        [ObservableProperty] private string _statusMessage = "Ready to create theme";
        [ObservableProperty] private bool _canSaveTheme = false;
        [ObservableProperty] private ThemeDefinition? _selectedBaseTheme;

        public ObservableCollection<ThemeDefinition> BaseThemes { get; }
        public ObservableCollection<ColorProperty> PrimaryColors { get; }
        public ObservableCollection<ColorProperty> BackgroundColors { get; }
        public ObservableCollection<ColorProperty> TextColors { get; }
        public ObservableCollection<ColorProperty> StatusColors { get; }

        public ThemeEditorViewModel(IThemeService themeService, IConfigManager configManager, 
            ILogger<ThemeEditorViewModel> logger, IErrorHandlingService errorHandler)
        {
            _themeService = themeService;
            _configManager = configManager;
            _logger = logger;
            _errorHandler = errorHandler;

            // Initialize base themes
            BaseThemes = new ObservableCollection<ThemeDefinition>(_themeService.AvailableThemes);

            // Initialize color properties FIRST
            PrimaryColors = new ObservableCollection<ColorProperty>();
            BackgroundColors = new ObservableCollection<ColorProperty>();
            TextColors = new ObservableCollection<ColorProperty>();
            StatusColors = new ObservableCollection<ColorProperty>();

            InitializeColorProperties();
            
            // Subscribe to property changes AFTER initialization
            foreach (var colorCollection in new[] { PrimaryColors, BackgroundColors, TextColors, StatusColors })
            {
                foreach (var color in colorCollection)
                {
                    color.PropertyChanged += OnColorPropertyChanged;
                }
            }

            // Set the default base theme AFTER collections are initialized
            SelectedBaseTheme = BaseThemes.FirstOrDefault(t => t.Type == ThemeType.DarkProfessional);
        }

        private void InitializeColorProperties()
        {
            // Primary Colors
            PrimaryColors.Add(new ColorProperty("Primary", "#3498db", "Main brand color"));
            PrimaryColors.Add(new ColorProperty("Primary Hover", "#2980b9", "Primary color on hover"));
            PrimaryColors.Add(new ColorProperty("Secondary", "#95a5a6", "Secondary accent color"));
            PrimaryColors.Add(new ColorProperty("Secondary Hover", "#7f8c8d", "Secondary color on hover"));

            // Background Colors  
            BackgroundColors.Add(new ColorProperty("Background Primary", "#2c3e50", "Main background"));
            BackgroundColors.Add(new ColorProperty("Background Secondary", "#34495e", "Secondary background"));
            BackgroundColors.Add(new ColorProperty("Background Tertiary", "#3c4f66", "Tertiary background"));
            BackgroundColors.Add(new ColorProperty("Card Background", "#ffffff", "Card background"));
            BackgroundColors.Add(new ColorProperty("Surface", "#f8f9fa", "Surface color"));

            // Text Colors
            TextColors.Add(new ColorProperty("Text Primary", "#2c3e50", "Primary text color"));
            TextColors.Add(new ColorProperty("Text Secondary", "#7f8c8d", "Secondary text color"));
            TextColors.Add(new ColorProperty("Text Tertiary", "#bdc3c7", "Tertiary text color"));
            TextColors.Add(new ColorProperty("Text On Primary", "#ffffff", "Text on primary background"));

            // Status Colors
            StatusColors.Add(new ColorProperty("Success", "#27ae60", "Success/positive states"));
            StatusColors.Add(new ColorProperty("Success Hover", "#229954", "Success color on hover"));
            StatusColors.Add(new ColorProperty("Warning", "#f39c12", "Warning states"));
            StatusColors.Add(new ColorProperty("Warning Hover", "#e67e22", "Warning color on hover"));
            StatusColors.Add(new ColorProperty("Error", "#e74c3c", "Error/danger states"));
            StatusColors.Add(new ColorProperty("Error Hover", "#c0392b", "Error color on hover"));
            StatusColors.Add(new ColorProperty("Info", "#3498db", "Info states"));
            StatusColors.Add(new ColorProperty("Info Hover", "#2980b9", "Info color on hover"));
        }

        private void OnColorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ColorProperty.HexValue))
            {
                CanSaveTheme = true;
                StatusMessage = "Theme modified - click 'Apply Preview' to see changes";
            }
        }

        [RelayCommand]
        private async Task ApplyPreviewAsync()
        {
            try
            {
                StatusMessage = "Applying preview theme...";
                
                // Generate theme XAML
                var themeXaml = GenerateThemeXaml();
                
                // Apply the custom theme directly to the application
                await ApplyCustomThemeToApplicationAsync(themeXaml);
                
                StatusMessage = "Preview applied successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme preview");
                await _errorHandler.HandleExceptionAsync(ex, "Failed to apply theme preview", "Theme Editor");
                StatusMessage = "Failed to apply preview";
            }
        }

        [RelayCommand]
        private async Task SaveThemeAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ThemeName))
                {
                    await _errorHandler.ShowErrorAsync("Invalid Theme Name", 
                        "Please enter a valid theme name.", ErrorSeverity.Warning);
                    return;
                }

                StatusMessage = "Saving theme...";
                
                // Generate theme XAML
                var themeXaml = GenerateThemeXaml();
                
                // Save to custom themes directory
                var customThemesDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ProxyStudio", "CustomThemes");
                
                Directory.CreateDirectory(customThemesDir);
                
                var fileName = $"{ThemeName.Replace(" ", "_")}.axaml";
                var filePath = Path.Combine(customThemesDir, fileName);
                
                await File.WriteAllTextAsync(filePath, themeXaml);
                
                _logger.LogInformation("Custom theme saved: {FilePath}", filePath);
                StatusMessage = $"Theme saved successfully to: {fileName}";
                CanSaveTheme = false;
                
                await _errorHandler.ShowErrorAsync("Theme Saved", 
                    $"Your custom theme '{ThemeName}' has been saved successfully!", 
                    ErrorSeverity.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save custom theme");
                await _errorHandler.HandleExceptionAsync(ex, "Failed to save custom theme", "Theme Editor");
                StatusMessage = "Failed to save theme";
            }
        }

        [RelayCommand]
        private async Task ExportThemeAsync()
        {
            try
            {
                // Get the top-level window for the file dialog
                var topLevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow 
                    : null;

                if (topLevel == null) return;

                // Show save file dialog
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export Theme File",
                    DefaultExtension = "axaml",
                    SuggestedFileName = $"{ThemeName.Replace(" ", "_")}.axaml",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("XAML Theme Files")
                        {
                            Patterns = new[] { "*.axaml" }
                        }
                    }
                });

                if (file != null)
                {
                    StatusMessage = "Exporting theme...";
                    
                    var themeXaml = GenerateThemeXaml();
                    
                    using var stream = await file.OpenWriteAsync();
                    using var writer = new StreamWriter(stream);
                    await writer.WriteAsync(themeXaml);
                    
                    StatusMessage = "Theme exported successfully";
                    
                    await _errorHandler.ShowErrorAsync("Theme Exported", 
                        $"Theme exported successfully to: {file.Name}", 
                        ErrorSeverity.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export theme");
                await _errorHandler.HandleExceptionAsync(ex, "Failed to export theme", "Theme Editor");
                StatusMessage = "Failed to export theme";
            }
        }

        [RelayCommand]
        private void ResetToBase()
        {
            if (SelectedBaseTheme == null) return;

            try
            {
                // Reset colors based on base theme
                ResetColorsFromBaseTheme(SelectedBaseTheme);
                
                StatusMessage = $"Reset to {SelectedBaseTheme.Name} base colors";
                CanSaveTheme = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset to base theme");
                StatusMessage = "Failed to reset to base theme";
            }
        }

        private void ResetColorsFromBaseTheme(ThemeDefinition baseTheme)
        {
            // Define color schemes for different base themes
            switch (baseTheme.Type)
            {
                case ThemeType.DarkProfessional:
                    SetDarkProfessionalColors();
                    break;
                case ThemeType.LightClassic:
                    SetLightClassicColors();
                    break;
                case ThemeType.Gaming:
                    SetGamingColors();
                    break;
                case ThemeType.HighContrast:
                    SetHighContrastColors();
                    break;
                case ThemeType.Minimal:
                    SetMinimalColors();
                    break;
            }
        }

        private void SetDarkProfessionalColors()
        {
            SetColorValue(PrimaryColors, "Primary", "#3498db");
            SetColorValue(PrimaryColors, "Primary Hover", "#2980b9");
            SetColorValue(PrimaryColors, "Secondary", "#95a5a6");
            SetColorValue(PrimaryColors, "Secondary Hover", "#7f8c8d");
            
            SetColorValue(BackgroundColors, "Background Primary", "#2c3e50");
            SetColorValue(BackgroundColors, "Background Secondary", "#34495e");
            SetColorValue(BackgroundColors, "Background Tertiary", "#3c4f66");
            SetColorValue(BackgroundColors, "Card Background", "#ffffff");
            SetColorValue(BackgroundColors, "Surface", "#f8f9fa");
            
            SetColorValue(TextColors, "Text Primary", "#ffffff");
            SetColorValue(TextColors, "Text Secondary", "#bdc3c7");
            SetColorValue(TextColors, "Text Tertiary", "#95a5a6");
            SetColorValue(TextColors, "Text On Primary", "#ffffff");
            
            SetColorValue(StatusColors, "Success", "#27ae60");
            SetColorValue(StatusColors, "Success Hover", "#229954");
            SetColorValue(StatusColors, "Warning", "#f39c12");
            SetColorValue(StatusColors, "Warning Hover", "#e67e22");
            SetColorValue(StatusColors, "Error", "#e74c3c");
            SetColorValue(StatusColors, "Error Hover", "#c0392b");
            SetColorValue(StatusColors, "Info", "#3498db");
            SetColorValue(StatusColors, "Info Hover", "#2980b9");
        }

        private void SetLightClassicColors()
        {
            SetColorValue(PrimaryColors, "Primary", "#007acc");
            SetColorValue(PrimaryColors, "Primary Hover", "#005a9e");
            SetColorValue(PrimaryColors, "Secondary", "#6c757d");
            SetColorValue(PrimaryColors, "Secondary Hover", "#5a6268");
            
            SetColorValue(BackgroundColors, "Background Primary", "#ffffff");
            SetColorValue(BackgroundColors, "Background Secondary", "#f8f9fa");
            SetColorValue(BackgroundColors, "Background Tertiary", "#e9ecef");
            SetColorValue(BackgroundColors, "Card Background", "#ffffff");
            SetColorValue(BackgroundColors, "Surface", "#f8f9fa");
            
            SetColorValue(TextColors, "Text Primary", "#212529");
            SetColorValue(TextColors, "Text Secondary", "#6c757d");
            SetColorValue(TextColors, "Text Tertiary", "#adb5bd");
            SetColorValue(TextColors, "Text On Primary", "#ffffff");
            
            SetColorValue(StatusColors, "Success", "#28a745");
            SetColorValue(StatusColors, "Success Hover", "#218838");
            SetColorValue(StatusColors, "Warning", "#ffc107");
            SetColorValue(StatusColors, "Warning Hover", "#e0a800");
            SetColorValue(StatusColors, "Error", "#dc3545");
            SetColorValue(StatusColors, "Error Hover", "#c82333");
            SetColorValue(StatusColors, "Info", "#17a2b8");
            SetColorValue(StatusColors, "Info Hover", "#138496");
        }

        private void SetGamingColors()
        {
            SetColorValue(PrimaryColors, "Primary", "#ff6b35");
            SetColorValue(PrimaryColors, "Primary Hover", "#e55a2b");
            SetColorValue(PrimaryColors, "Secondary", "#00d4ff");
            SetColorValue(PrimaryColors, "Secondary Hover", "#00bde6");
            
            SetColorValue(BackgroundColors, "Background Primary", "#0a0a0a");
            SetColorValue(BackgroundColors, "Background Secondary", "#1a1a1a");
            SetColorValue(BackgroundColors, "Background Tertiary", "#2a2a2a");
            SetColorValue(BackgroundColors, "Card Background", "#1a1a1a");
            SetColorValue(BackgroundColors, "Surface", "#2a2a2a");
            
            SetColorValue(TextColors, "Text Primary", "#ffffff");
            SetColorValue(TextColors, "Text Secondary", "#cccccc");
            SetColorValue(TextColors, "Text Tertiary", "#999999");
            SetColorValue(TextColors, "Text On Primary", "#ffffff");
            
            SetColorValue(StatusColors, "Success", "#00ff88");
            SetColorValue(StatusColors, "Success Hover", "#00e67a");
            SetColorValue(StatusColors, "Warning", "#ffaa00");
            SetColorValue(StatusColors, "Warning Hover", "#e69900");
            SetColorValue(StatusColors, "Error", "#ff3366");
            SetColorValue(StatusColors, "Error Hover", "#e62e5c");
            SetColorValue(StatusColors, "Info", "#00d4ff");
            SetColorValue(StatusColors, "Info Hover", "#00bde6");
        }

        private void SetHighContrastColors()
        {
            SetColorValue(PrimaryColors, "Primary", "#ffff00");
            SetColorValue(PrimaryColors, "Primary Hover", "#e6e600");
            SetColorValue(PrimaryColors, "Secondary", "#00ffff");
            SetColorValue(PrimaryColors, "Secondary Hover", "#00e6e6");
            
            SetColorValue(BackgroundColors, "Background Primary", "#000000");
            SetColorValue(BackgroundColors, "Background Secondary", "#1a1a1a");
            SetColorValue(BackgroundColors, "Background Tertiary", "#333333");
            SetColorValue(BackgroundColors, "Card Background", "#000000");
            SetColorValue(BackgroundColors, "Surface", "#1a1a1a");
            
            SetColorValue(TextColors, "Text Primary", "#ffffff");
            SetColorValue(TextColors, "Text Secondary", "#ffff00");
            SetColorValue(TextColors, "Text Tertiary", "#00ffff");
            SetColorValue(TextColors, "Text On Primary", "#000000");
            
            SetColorValue(StatusColors, "Success", "#00ff00");
            SetColorValue(StatusColors, "Success Hover", "#00e600");
            SetColorValue(StatusColors, "Warning", "#ffff00");
            SetColorValue(StatusColors, "Warning Hover", "#e6e600");
            SetColorValue(StatusColors, "Error", "#ff0000");
            SetColorValue(StatusColors, "Error Hover", "#e60000");
            SetColorValue(StatusColors, "Info", "#00ffff");
            SetColorValue(StatusColors, "Info Hover", "#00e6e6");
        }

        private void SetMinimalColors()
        {
            SetColorValue(PrimaryColors, "Primary", "#333333");
            SetColorValue(PrimaryColors, "Primary Hover", "#555555");
            SetColorValue(PrimaryColors, "Secondary", "#666666");
            SetColorValue(PrimaryColors, "Secondary Hover", "#777777");
            
            SetColorValue(BackgroundColors, "Background Primary", "#ffffff");
            SetColorValue(BackgroundColors, "Background Secondary", "#fafafa");
            SetColorValue(BackgroundColors, "Background Tertiary", "#f5f5f5");
            SetColorValue(BackgroundColors, "Card Background", "#ffffff");
            SetColorValue(BackgroundColors, "Surface", "#fafafa");
            
            SetColorValue(TextColors, "Text Primary", "#333333");
            SetColorValue(TextColors, "Text Secondary", "#666666");
            SetColorValue(TextColors, "Text Tertiary", "#999999");
            SetColorValue(TextColors, "Text On Primary", "#ffffff");
            
            SetColorValue(StatusColors, "Success", "#4caf50");
            SetColorValue(StatusColors, "Success Hover", "#45a049");
            SetColorValue(StatusColors, "Warning", "#ff9800");
            SetColorValue(StatusColors, "Warning Hover", "#e68900");
            SetColorValue(StatusColors, "Error", "#f44336");
            SetColorValue(StatusColors, "Error Hover", "#d32f2f");
            SetColorValue(StatusColors, "Info", "#2196f3");
            SetColorValue(StatusColors, "Info Hover", "#1976d2");
        }

        private void SetColorValue(ObservableCollection<ColorProperty> collection, string name, string hexValue)
        {
            var colorProperty = collection.FirstOrDefault(c => c.Name == name);
            if (colorProperty != null)
            {
                // Temporarily unsubscribe to prevent triggering change events during bulk updates
                colorProperty.PropertyChanged -= OnColorPropertyChanged;
                colorProperty.HexValue = hexValue;
                colorProperty.PropertyChanged += OnColorPropertyChanged;
                
                _logger.LogDebug("Updated color {ColorName} to {HexValue}", name, hexValue);
            }
            else
            {
                _logger.LogWarning("Color property {ColorName} not found in collection", name);
            }
        }

        private string GenerateThemeXaml()
        {
            var sb = new StringBuilder();
            
            // XAML Header
            sb.AppendLine("<Styles xmlns=\"https://github.com/avaloniaui\"");
            sb.AppendLine("        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
            sb.AppendLine();
            
            // Theme metadata comment
            sb.AppendLine($"  <!-- {ThemeName} -->");
            sb.AppendLine($"  <!-- Description: {ThemeDescription} -->");
            sb.AppendLine($"  <!-- Author: {ThemeAuthor} -->");
            sb.AppendLine($"  <!-- Version: {ThemeVersion} -->");
            sb.AppendLine($"  <!-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} -->");
            sb.AppendLine();
            
            // Resources section
            sb.AppendLine("  <Styles.Resources>");
            sb.AppendLine();
            sb.AppendLine("    <!-- Marker to identify preview themes -->");
            sb.AppendLine("    <Color x:Key=\"__PreviewThemeMarker__\">#000000</Color>");
            
            // Add all color resources
            sb.AppendLine("    <!-- Primary Colors -->");
            AppendColorResources(sb, PrimaryColors);
            sb.AppendLine();
            
            sb.AppendLine("    <!-- Background Colors -->");
            AppendColorResources(sb, BackgroundColors);
            sb.AppendLine();
            
            sb.AppendLine("    <!-- Text Colors -->");
            AppendColorResources(sb, TextColors);
            sb.AppendLine();
            
            sb.AppendLine("    <!-- Status Colors -->");
            AppendColorResources(sb, StatusColors);
            sb.AppendLine();
            
            // Create brushes from colors
            sb.AppendLine("    <!-- Brushes -->");
            AppendBrushResources(sb, PrimaryColors);
            AppendBrushResources(sb, BackgroundColors);
            AppendBrushResources(sb, TextColors);
            AppendBrushResources(sb, StatusColors);
            sb.AppendLine();
            
            sb.AppendLine("  </Styles.Resources>");
            sb.AppendLine();
            
            // // Caption/Label style - Avalonia 11.3 compatible
            // sb.AppendLine("  <Style Selector=\"TextBlock.caption\">");
            // sb.AppendLine("    <Setter Property=\"FontSize\" Value=\"11\"/>");
            // sb.AppendLine("    <Setter Property=\"FontWeight\" Value=\"SemiBold\"/>");
            // sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextTertiaryBrush}\"/>");
            // sb.AppendLine("  </Style>");
            // sb.AppendLine();
            
            // Control styles
            AppendControlStyles(sb);
            
            sb.AppendLine("</Styles>");
            
            return sb.ToString();
        }

        private void AppendColorResources(StringBuilder sb, ObservableCollection<ColorProperty> colors)
        {
            foreach (var color in colors)
            {
                var resourceKey = color.Name.Replace(" ", "") + "Color";
                sb.AppendLine($"    <Color x:Key=\"{resourceKey}\">{color.HexValue}</Color>");
            }
        }

        private void AppendBrushResources(StringBuilder sb, ObservableCollection<ColorProperty> colors)
        {
            foreach (var color in colors)
            {
                var colorKey = color.Name.Replace(" ", "") + "Color";
                var brushKey = color.Name.Replace(" ", "") + "Brush";
                sb.AppendLine($"    <SolidColorBrush x:Key=\"{brushKey}\" Color=\"{{DynamicResource {colorKey}}}\"/>");
            }
        }

        private void AppendControlStyles(StringBuilder sb)
        {
            sb.AppendLine("  <!-- Control Styles -->");
            sb.AppendLine();
            
            // Window style
            sb.AppendLine("  <Style Selector=\"Window\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource BackgroundPrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // Button styles
            sb.AppendLine("  <Style Selector=\"Button.primary\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource PrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextOnPrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource PrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"1\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"6\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"16,8\"/>");
            sb.AppendLine("    <Setter Property=\"FontWeight\" Value=\"SemiBold\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Button.primary:pointerover\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource PrimaryHoverBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource PrimaryHoverBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Button.secondary\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource SecondaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextOnPrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource SecondaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"1\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"6\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"16,8\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Button.secondary:pointerover\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource SecondaryHoverBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource SecondaryHoverBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // Success button style
            sb.AppendLine("  <Style Selector=\"Button.success\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource SuccessBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextOnPrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource SuccessBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"1\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"6\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"16,8\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Button.success:pointerover\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource SuccessHoverBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource SuccessHoverBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // Icon button style
            sb.AppendLine("  <Style Selector=\"Button.icon\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource BackgroundSecondaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource BackgroundTertiaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"1\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"50\"/>");
            sb.AppendLine("    <Setter Property=\"Width\" Value=\"40\"/>");
            sb.AppendLine("    <Setter Property=\"Height\" Value=\"40\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"0\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // Card styles
            sb.AppendLine("  <Style Selector=\"Border.card\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource CardBackgroundBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource BackgroundSecondaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"1\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"8\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"16\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Border.card-compact\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource CardBackgroundBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource BackgroundSecondaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"1\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"6\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"12\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Border.card-elevated\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource CardBackgroundBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource BackgroundSecondaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"1\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"8\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"16\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // Text input styles
            sb.AppendLine("  <Style Selector=\"TextBox.modern\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource SurfaceBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource BackgroundSecondaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"1\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"4\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"8\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"TextBox.modern:focus\">");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource PrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"2\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // ComboBox styles
            sb.AppendLine("  <Style Selector=\"ComboBox.modern\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource SurfaceBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource BackgroundSecondaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"1\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"4\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"8\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // Typography classes - Avalonia 11.3 compatible
            sb.AppendLine("  <!-- Typography Classes -->");
            sb.AppendLine("  <Style Selector=\"TextBlock.heading-large\">");
            sb.AppendLine("    <Setter Property=\"FontSize\" Value=\"32\"/>");
            sb.AppendLine("    <Setter Property=\"FontWeight\" Value=\"Bold\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"TextBlock.heading-medium\">");
            sb.AppendLine("    <Setter Property=\"FontSize\" Value=\"24\"/>");
            sb.AppendLine("    <Setter Property=\"FontWeight\" Value=\"SemiBold\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"TextBlock.heading-small\">");
            sb.AppendLine("    <Setter Property=\"FontSize\" Value=\"18\"/>");
            sb.AppendLine("    <Setter Property=\"FontWeight\" Value=\"SemiBold\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"TextBlock.body-large\">");
            sb.AppendLine("    <Setter Property=\"FontSize\" Value=\"16\"/>");
            sb.AppendLine("    <Setter Property=\"FontWeight\" Value=\"Normal\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"TextBlock.body-medium\">");
            sb.AppendLine("    <Setter Property=\"FontSize\" Value=\"14\"/>");
            sb.AppendLine("    <Setter Property=\"FontWeight\" Value=\"Normal\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"TextBlock.body-small\">");
            sb.AppendLine("    <Setter Property=\"FontSize\" Value=\"12\"/>");
            sb.AppendLine("    <Setter Property=\"FontWeight\" Value=\"Normal\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextSecondaryBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // Caption style - Avalonia 11.3 compatible (no TextTransform or LetterSpacing)
            sb.AppendLine("  <Style Selector=\"TextBlock.caption\">");
            sb.AppendLine("    <Setter Property=\"FontSize\" Value=\"11\"/>");
            sb.AppendLine("    <Setter Property=\"FontWeight\" Value=\"SemiBold\"/>");
            sb.AppendLine("    <Setter Property=\"Foreground\" Value=\"{DynamicResource TextTertiaryBrush}\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // Highlight cards
            sb.AppendLine("  <Style Selector=\"Border.highlight-primary\">");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource PrimaryBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"2\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Border.highlight-success\">");
            sb.AppendLine("    <Setter Property=\"BorderBrush\" Value=\"{DynamicResource SuccessBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"BorderThickness\" Value=\"2\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            // Status badges
            sb.AppendLine("  <Style Selector=\"Border.badge-success\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource SuccessBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"12\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"8,4\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Border.badge-error\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource ErrorBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"12\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"8,4\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Border.badge-warning\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource WarningBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"12\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"8,4\"/>");
            sb.AppendLine("  </Style>");
            sb.AppendLine();
            
            sb.AppendLine("  <Style Selector=\"Border.badge-info\">");
            sb.AppendLine("    <Setter Property=\"Background\" Value=\"{DynamicResource InfoBrush}\"/>");
            sb.AppendLine("    <Setter Property=\"CornerRadius\" Value=\"12\"/>");
            sb.AppendLine("    <Setter Property=\"Padding\" Value=\"8,4\"/>");
            sb.AppendLine("  </Style>");
        }

        private async Task ApplyCustomThemeToApplicationAsync(string themeXaml)
        {
            try
            {
                // // Save the theme XAML to a temporary file
                // var tempDir = Path.Combine(Path.GetTempPath(), "ProxyStudio", "Themes");
                // Directory.CreateDirectory(tempDir);
                // var tempThemeFile = Path.Combine(tempDir, "preview_theme.axaml");
                //
                // await File.WriteAllTextAsync(tempThemeFile, themeXaml);
                //
                // // Check if we have the enhanced theme service methods available
                // var themeServiceType = _themeService.GetType();
                // var applyCustomMethod = themeServiceType.GetMethod("ApplyCustomThemeAsync");
                //
                // if (applyCustomMethod != null)
                // {
                //     // Use the enhanced theme service method if available
                //     await (Task)applyCustomMethod.Invoke(_themeService, new object[] { themeXaml, "Preview Theme" });
                //     _logger.LogDebug("Applied theme using enhanced theme service");
                // }
                // else
                // {
                    // Fallback: Apply the theme directly using Avalonia's style system
                    await ApplyThemeDirectlyAsync(themeXaml);
                    _logger.LogDebug("Applied theme using direct method");
                //}
                
                _logger.LogInformation("Applied custom theme preview: {ThemeName}", ThemeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply custom theme to application");
                throw;
            }
        }

        private async Task ApplyThemeDirectlyAsync(string themeXaml)
        {
            try
            {
                var app = Avalonia.Application.Current;

                if (app?.Styles == null) return;

                // Write XAML to temp file
                var tempDir = Path.Combine(Path.GetTempPath(), "ProxyStudio", "Themes");
                Directory.CreateDirectory(tempDir);
                var tempThemePath = Path.Combine(tempDir, "preview_theme.axaml");

                await File.WriteAllTextAsync(tempThemePath, themeXaml);

                var styleUri = new Uri($"file:///{tempThemePath.Replace('\\', '/')}");

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Remove existing preview styles
                    var oldPreview = app.Styles
                        .OfType<StyleInclude>()
                        .Where(s => s.Source != null && s.Source.ToString().Contains("preview_theme.axaml"))
                        .ToList();

                    foreach (var style in oldPreview)
                        app.Styles.Remove(style);

                    // Create and load the style include
                    var styleInclude = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("file:///"))
                    {
                        Source = styleUri
                    };

                    app.Styles.Add(styleInclude);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme from memory fallback");
                throw;
            }
        }

        partial void OnSelectedBaseThemeChanged(ThemeDefinition? value)
        {
            if (value != null)
            {
                StatusMessage = $"Base theme changed to: {value.Name}";
                
                // Only update colors if collections are initialized
                if (PrimaryColors != null && BackgroundColors != null && TextColors != null && StatusColors != null)
                {
                    // Automatically update colors when base theme changes
                    ResetColorsFromBaseTheme(value);
                    
                    // Mark as modified so user can save
                    CanSaveTheme = true;
                    
                    _logger.LogDebug("Automatically updated color palette for base theme: {ThemeName}", value.Name);
                }
                else
                {
                    _logger.LogDebug("Skipping color update - collections not yet initialized");
                }
            }
        }

        partial void OnThemeNameChanged(string value)
        {
            CanSaveTheme = !string.IsNullOrWhiteSpace(value);
        }
    }
}