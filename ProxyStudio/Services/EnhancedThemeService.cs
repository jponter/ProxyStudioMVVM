// ProxyStudio/Services/EnhancedThemeService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using ProxyStudio.Helpers;

namespace ProxyStudio.Services
{
    public enum SeasonalTheme
    {
        None,
        Christmas,
        Halloween,
        Valentine,
        Summer,
        Spring
    }

    public class EnhancedThemeService : IThemeService
    {
        private readonly IConfigManager _configManager;
        private readonly ILogger<EnhancedThemeService> _logger;
        private ThemeType _currentTheme = ThemeType.DarkProfessional;
        private readonly ThemeTransitionManager _transitionManager;

        public ThemeType CurrentTheme => _currentTheme;
        public SeasonalTheme CurrentSeasonalTheme { get; private set; } = SeasonalTheme.None;

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
                Description = "Vibrant theme with gaming-inspired RGB lighting",
                ResourcePath = "avares://ProxyStudio/Themes/Gaming.axaml",
                IsDark = true,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/gaming.png"
            },
            new() {
                Type = ThemeType.Minimal,
                Name = "Minimal",
                Description = "Clean, distraction-free monochrome interface",
                ResourcePath = "avares://ProxyStudio/Themes/Minimal.axaml",
                IsDark = false,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/minimal.png"
            }
        };

        public event EventHandler<ThemeType>? ThemeChanged;
        public event EventHandler<SeasonalTheme>? SeasonalThemeChanged;

        public EnhancedThemeService(IConfigManager configManager, ILogger<EnhancedThemeService> logger)
        {
            _configManager = configManager;
            _logger = logger;
            _transitionManager = new ThemeTransitionManager();
            
            // Check for seasonal themes on startup
            CheckSeasonalThemes();
        }

        public async Task ApplyThemeAsync(ThemeType theme)
        {
            bool animated = true;
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

                if (animated)
                {
                    await _transitionManager.TransitionToThemeAsync(themeDefinition);
                }
                else
                {
                    ApplyThemeImmediate(themeDefinition);
                }

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

        private void ApplyThemeImmediate(ThemeDefinition themeDefinition)
        {
            var app = Application.Current;
            if (app?.Styles == null) return;

            // Remove existing custom themes
            var customThemes = app.Styles
                .OfType<StyleInclude>()
                .Where(s => s.Source?.ToString().Contains("/Themes/") == true)
                .ToList();

            foreach (var customTheme in customThemes)
            {
                app.Styles.Remove(customTheme);
            }

            // Add new theme
            var styleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
            {
                Source = new Uri(themeDefinition.ResourcePath)
            };

            app.Styles.Add(styleInclude);

            // Apply seasonal overlay if active
            if (CurrentSeasonalTheme != SeasonalTheme.None)
            {
                ApplySeasonalOverlay(CurrentSeasonalTheme);
            }
        }

        public async Task ApplySeasonalThemeAsync(SeasonalTheme seasonalTheme)
        {
            try
            {
                _logger.LogInformation("Applying seasonal theme: {SeasonalTheme}", seasonalTheme);
                
                CurrentSeasonalTheme = seasonalTheme;
                
                if (seasonalTheme != SeasonalTheme.None)
                {
                    ApplySeasonalOverlay(seasonalTheme);
                }
                else
                {
                    RemoveSeasonalOverlay();
                }

                SeasonalThemeChanged?.Invoke(this, seasonalTheme);
                
                // Save seasonal preference
                _configManager.Config.SeasonalTheme = seasonalTheme;
                await _configManager.SaveConfigAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply seasonal theme: {SeasonalTheme}", seasonalTheme);
                throw;
            }
        }

        private void ApplySeasonalOverlay(SeasonalTheme seasonal)
        {
            var app = Application.Current;
            if (app?.Styles == null) return;

            // Remove existing seasonal themes
            RemoveSeasonalOverlay();

            var seasonalPath = seasonal switch
            {
                SeasonalTheme.Christmas => "avares://ProxyStudio/Themes/Seasonal/Christmas.axaml",
                SeasonalTheme.Halloween => "avares://ProxyStudio/Themes/Seasonal/Halloween.axaml",
                SeasonalTheme.Valentine => "avares://ProxyStudio/Themes/Seasonal/Valentine.axaml",
                SeasonalTheme.Summer => "avares://ProxyStudio/Themes/Seasonal/Summer.axaml",
                SeasonalTheme.Spring => "avares://ProxyStudio/Themes/Seasonal/Spring.axaml",
                _ => null
            };

            if (seasonalPath != null)
            {
                var seasonalInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                {
                    Source = new Uri(seasonalPath), 
                    //Name = "SeasonalTheme"
                };
                app.Styles.Add(seasonalInclude);
            }
        }

        private void RemoveSeasonalOverlay()
        {
            var app = Application.Current;
            if (app?.Styles == null) return;

            var seasonalThemes = app.Styles
                .OfType<StyleInclude>()
                .Where(s => s.Source.ToString().Contains("Seasonal"))
                .ToList();

            foreach (var seasonal in seasonalThemes)
            {
                app.Styles.Remove(seasonal);
            }
        }

        private void CheckSeasonalThemes()
        {
            var now = DateTime.Now;
            var autoSeasonal = _configManager.Config.AutoSeasonalThemes;
            
            if (!autoSeasonal) return;

            SeasonalTheme detectedSeason = SeasonalTheme.None;

            // Christmas: December 1-31, January 1-7
            if ((now.Month == 12) || (now.Month == 1 && now.Day <= 7))
                detectedSeason = SeasonalTheme.Christmas;
            // Halloween: October 15-31
            else if (now.Month == 10 && now.Day >= 15)
                detectedSeason = SeasonalTheme.Halloween;
            // Valentine's: February 1-14
            else if (now.Month == 2 && now.Day <= 14)
                detectedSeason = SeasonalTheme.Valentine;
            // Summer: June-August
            else if (now.Month >= 6 && now.Month <= 8)
                detectedSeason = SeasonalTheme.Summer;
            // Spring: March-May
            else if (now.Month >= 3 && now.Month <= 5)
                detectedSeason = SeasonalTheme.Spring;

            if (detectedSeason != SeasonalTheme.None)
            {
                _ = Task.Run(async () => await ApplySeasonalThemeAsync(detectedSeason));
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
                var config =  _configManager.LoadConfig();
                return config.SelectedTheme;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load theme preference, using default");
                return ThemeType.DarkProfessional;
            }
        }
    }

    public class ThemeTransitionManager
    {
        public async Task TransitionToThemeAsync(ThemeDefinition newTheme)
        {
            var app = Application.Current;
            if (app?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) 
                return;

            var mainWindow = desktop.MainWindow;
            if (mainWindow == null) return;

            // Create fade out animation
            var fadeOut = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = new CubicEaseInOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter(Visual.OpacityProperty, 0.3) }
                    }
                }
            };

            // Apply fade out
            await fadeOut.RunAsync(mainWindow);

            // Change theme while faded
            await Task.Delay(50); // Brief pause
            ApplyThemeImmediate(newTheme);
            await Task.Delay(50); // Let theme settle

            // Create fade in animation
            var fadeIn = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(300),
                Easing = new CubicEaseInOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters = { new Setter(Visual.OpacityProperty, 0.3) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                    }
                }
            };

            // Apply fade in
            await fadeIn.RunAsync(mainWindow);
        }

        private void ApplyThemeImmediate(ThemeDefinition themeDefinition)
        {
            var app = Application.Current;
            if (app?.Styles == null) return;

            // Remove existing custom themes
            var customThemes = app.Styles
                .OfType<StyleInclude>()
                .Where(s => s.Source?.ToString().Contains("/Themes/") == true)
                .ToList();

            foreach (var customTheme in customThemes)
            {
                app.Styles.Remove(customTheme);
            }

            // Add new theme
            var styleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
            {
                Source = new Uri(themeDefinition.ResourcePath)
            };

            app.Styles.Add(styleInclude);
        }
    }
}