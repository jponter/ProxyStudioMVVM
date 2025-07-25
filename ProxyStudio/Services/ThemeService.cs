using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;

namespace ProxyStudio.Services
{
    public class ThemeService : IThemeService
    {
        private readonly IConfigManager _configManager;
        private readonly ILogger<ThemeService> _logger;
        private ThemeType _currentTheme = ThemeType.DarkProfessional;

        public ThemeType CurrentTheme => _currentTheme;

        public IReadOnlyList<ThemeDefinition> AvailableThemes { get; } = new List<ThemeDefinition>
        {
            new() {
                Type = ThemeType.DarkProfessional,
                Name = "Dark Professional",
                Description = "Modern dark theme optimized for long work sessions",
                ResourcePath = "avares://ProxyStudio/Themes/DarkProfessional.axaml",
                IsDark = true,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/dark-professional.png"
            },
            new() {
                Type = ThemeType.LightClassic,
                Name = "Light Classic",
                Description = "Clean light theme with traditional styling",
                ResourcePath = "avares://ProxyStudio/Themes/LightClassic.axaml",
                IsDark = false,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/light-classic.png"
            },
            new() {
                Type = ThemeType.HighContrast,
                Name = "High Contrast",
                Description = "High contrast theme for accessibility",
                ResourcePath = "avares://ProxyStudio/Themes/HighContrast.axaml",
                IsDark = true,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/high-contrast.png"
            },
            new() {
                Type = ThemeType.Gaming,
                Name = "Gaming RGB",
                Description = "Vibrant theme with gaming-inspired colors",
                ResourcePath = "avares://ProxyStudio/Themes/Gaming.axaml",
                IsDark = true,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/gaming.png"
            },
            new() {
                Type = ThemeType.Minimal,
                Name = "Minimal",
                Description = "Clean, distraction-free interface",
                ResourcePath = "avares://ProxyStudio/Themes/Minimal.axaml",
                IsDark = false,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/minimal.png"
            }
        };

        public event EventHandler<ThemeType>? ThemeChanged;

        public ThemeService(IConfigManager configManager, ILogger<ThemeService> logger)
        {
            _configManager = configManager;
            _logger = logger;
        }

        
        // public async Task ApplyThemeAsync(ThemeType theme)
        // {
        //     try
        //     {
        //         _logger.LogInformation("TEST: Would apply theme: {ThemeName}", theme);
        //         _currentTheme = theme;
        //         ThemeChanged?.Invoke(this, theme);
        //         _logger.LogInformation("TEST: Theme application completed");
        //         return; // Skip actual theme application for testing
        //
        //         // Comment out the actual theme application temporarily
        //         /*
        //         var themeDefinition = AvailableThemes.FirstOrDefault(t => t.Type == theme);
        //         // ... rest of method
        //         */
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Failed to apply theme: {ThemeName}", theme);
        //         throw;
        //     }
        // }
        
        public async Task ApplyThemeAsync(ThemeType theme)
        {
            try
            {
                _logger.LogInformation("Applying theme: {ThemeName}", theme);
                
                var themeDefinition = AvailableThemes.FirstOrDefault(t => t.Type == theme);
                if (themeDefinition == null)
                {
                    _logger.LogWarning("Theme not found: {ThemeName}, falling back to default", theme);
                    theme = ThemeType.DarkProfessional;
                    themeDefinition = AvailableThemes.First(t => t.Type == theme);
                }

                // Remove existing custom themes
                var app = Application.Current;
                if (app?.Styles != null)
                {
                    var customThemes = app.Styles
                        .OfType<StyleInclude>()
                        .Where(s => s.Source?.ToString().Contains("/Themes/") == true)
                        .ToList();

                    foreach (var customTheme in customThemes)
                    {
                        app.Styles.Remove(customTheme);
                    }
                }

                // Add new theme
                var styleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                {
                    Source = new Uri(themeDefinition.ResourcePath)
                };

                app?.Styles.Add(styleInclude);

                _currentTheme = theme;
                ThemeChanged?.Invoke(this, theme);
                
                _logger.LogInformation("Successfully applied theme: {ThemeName}", theme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme: {ThemeName}", theme);
                throw;
            }
        }

        public bool SaveThemePreference(ThemeType theme)
        {
            try
            {
                _configManager.Config.SelectedTheme = theme;
                _configManager.SaveConfig();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save theme preference");
                return false;
            }
        }

        public ThemeType LoadThemePreference()
        {
            try
            {
                var config = _configManager.LoadConfig();
                return config.SelectedTheme;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load theme preference, using default");
                return ThemeType.DarkProfessional;
            }
        }
    }
}