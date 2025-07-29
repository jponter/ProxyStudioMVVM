// ProxyStudio/ViewModels/ThemeEditorViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProxyStudio.Helpers;
using ProxyStudio.Services;

namespace ProxyStudio.ViewModels
{
    public partial class ThemeEditorViewModel : ViewModelBase
    {
        private readonly IThemeService _themeService;
        private readonly IConfigManager _configManager;

        [ObservableProperty] private bool _isCustomThemeMode;
        [ObservableProperty] private string _customThemeName = "My Custom Theme";
        [ObservableProperty] private bool _isPreviewMode;

        public ObservableCollection<ColorProperty> CustomColors { get; } = new();

        public ThemeEditorViewModel(IThemeService themeService, IConfigManager configManager)
        {
            _themeService = themeService;
            _configManager = configManager;
            
            InitializeCustomColors();
        }

        private void InitializeCustomColors()
        {
            CustomColors.Clear();
            
            // Add customizable color properties
            var colorProperties = new[]
            {
                new ColorProperty("Primary Color", Colors.Blue, "Main accent color for buttons and highlights"),
                new ColorProperty("Background Color", Colors.White, "Main background color"),
                new ColorProperty("Surface Color", Colors.LightGray, "Card and panel backgrounds"),
                new ColorProperty("Text Primary", Colors.Black, "Main text color"),
                new ColorProperty("Text Secondary", Colors.Gray, "Secondary text and labels"),
                new ColorProperty("Success Color", Colors.Green, "Success states and confirmations"),
                new ColorProperty("Warning Color", Colors.Orange, "Warning states and alerts"),
                new ColorProperty("Error Color", Colors.Red, "Error states and critical actions"),
                new ColorProperty("Border Color", Colors.LightGray, "Default border color"),
                new ColorProperty("Focus Border", Colors.Blue, "Focused element border color")
            };

            foreach (var colorProp in colorProperties)
            {
                colorProp.PropertyChanged += OnColorPropertyChanged;
                CustomColors.Add(colorProp);
            }
        }

        private void OnColorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsPreviewMode && e.PropertyName == nameof(ColorProperty.Color))
            {
                _ = Task.Run(async () => await GeneratePreviewAsync());
            }
        }

        [RelayCommand]
        private async Task GeneratePreviewAsync()
        {
            try
            {
                IsPreviewMode = true;
                
                // Generate custom theme XAML
                var customThemeXaml = GenerateCustomThemeXaml();
                
                // Create temporary theme file
                var tempThemePath = await SaveTemporaryThemeAsync(customThemeXaml);
                
                // Apply preview theme
                await ApplyCustomThemeAsync(tempThemePath);
            }
            catch (Exception ex)
            {
                // Handle error
            }
        }

        [RelayCommand]
        private async Task SaveCustomThemeAsync()
        {
            try
            {
                var customThemeXaml = GenerateCustomThemeXaml();
                var savedThemePath = await SaveCustomThemeAsync(customThemeXaml, CustomThemeName);
                
                // Add to available themes list
                // Implementation depends on how you want to manage custom themes
                
                IsCustomThemeMode = false;
            }
            catch (Exception ex)
            {
                // Handle error
            }
        }

        [RelayCommand]
        private async Task ResetToBaseThemeAsync()
        {
            var savedTheme =  _themeService.LoadThemePreference();
            await _themeService.ApplyThemeAsync(savedTheme);
            IsPreviewMode = false;
        }

        private string GenerateCustomThemeXaml()
        {
            var xaml = $@"<Styles xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
  
  <Style.Resources>";

            foreach (var colorProp in CustomColors)
            {
                xaml += $@"
    <Color x:Key=""{colorProp.Key}"">{colorProp.Color}</Color>";
            }

            xaml += @"
  </Style.Resources>

  <!-- Apply custom colors to all controls -->
  <Style Selector=""Window"">
    <Setter Property=""Background"" Value=""{DynamicResource BackgroundColorKey}""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource TextPrimaryKey}""/>
  </Style>

  <Style Selector=""Button"">
    <Setter Property=""Background"" Value=""{DynamicResource PrimaryColorKey}""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource TextPrimaryKey}""/>
    <Setter Property=""BorderBrush"" Value=""{DynamicResource PrimaryColorKey}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""CornerRadius"" Value=""6""/>
    <Setter Property=""Padding"" Value=""12,8""/>
    <Setter Property=""FontWeight"" Value=""SemiBold""/>
  </Style>

  <Style Selector=""TextBox"">
    <Setter Property=""Background"" Value=""{DynamicResource SurfaceColorKey}""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource TextPrimaryKey}""/>
    <Setter Property=""BorderBrush"" Value=""{DynamicResource BorderColorKey}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""CornerRadius"" Value=""4""/>
    <Setter Property=""Padding"" Value=""8""/>
  </Style>

  <Style Selector=""TextBox:focus"">
    <Setter Property=""BorderBrush"" Value=""{DynamicResource FocusBorderKey}""/>
    <Setter Property=""BorderThickness"" Value=""2""/>
  </Style>

  <!-- Add more control styles as needed -->
  
</Styles>";

            return xaml;
        }

        private async Task<string> SaveTemporaryThemeAsync(string xaml)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "ProxyStudio", "temp_theme.axaml");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
            await File.WriteAllTextAsync(tempPath, xaml);
            return tempPath;
        }

        private async Task<string> SaveCustomThemeAsync(string xaml, string themeName)
        {
            var customThemesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProxyStudio", "CustomThemes");
            
            Directory.CreateDirectory(customThemesDir);
            
            var fileName = $"{themeName.Replace(" ", "_")}.axaml";
            var filePath = Path.Combine(customThemesDir, fileName);
            
            await File.WriteAllTextAsync(filePath, xaml);
            return filePath;
        }

        private async Task ApplyCustomThemeAsync(string themePath)
        {
            // Implementation to apply custom theme from file path
            // This would need to be added to your theme service
        }
    }

    public partial class ColorProperty : ObservableObject
    {
        [ObservableProperty] private string _name;
        [ObservableProperty] private Color _color;
        [ObservableProperty] private string _description;

        public string Key => Name.Replace(" ", "") + "Key";

        public ColorProperty(string name, Color color, string description)
        {
            _name = name;
            _color = color;
            _description = description;
        }
    }
}