/*
ProxyStudio - A cross-platform proxy management application.
Copyright (C) 2025 James Ponter

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
*/

// Updated EnhancedThemeService.cs - Supports new ThemeDictionaries format
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

    public class UpdatedEnhancedThemeService : IThemeService
    {
        private readonly IConfigManager _configManager;
        private readonly ILogger<UpdatedEnhancedThemeService> _logger;
        private ThemeType _currentTheme = ThemeType.DarkProfessional;
        private readonly ThemeTransitionManager _transitionManager;

        public ThemeType CurrentTheme => _currentTheme;
        public SeasonalTheme CurrentSeasonalTheme { get; private set; } = SeasonalTheme.None;

        public IReadOnlyList<ThemeDefinition> AvailableThemes { get; } = new List<ThemeDefinition>
        {
            new()
            {
                Type = ThemeType.DarkProfessional,
                Name = "Dark Professional",
                Description = "Modern dark theme optimized for long work sessions",
                ResourcePath = "avares://ProxyStudio/Themes/DarkProfessional.axaml",
                IsDark = true,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/dark-professional.png"
            },
            new()
            {
                Type = ThemeType.LightClassic,
                Name = "Light Classic",
                Description = "Clean light theme with traditional styling",
                ResourcePath = "avares://ProxyStudio/Themes/LightClassic.axaml",
                IsDark = false,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/light-classic.png"
            },
            new()
            {
                Type = ThemeType.DarkRed,
                Name = "Dark Red",
                Description = "Clean light theme with traditional styling",
                ResourcePath = "avares://ProxyStudio/Themes/DarkRed.axaml",
                IsDark = true,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/DarkRed.png"
            },

            new()
            {
                Type = ThemeType.HighContrast,
                Name = "High Contrast",
                Description = "High contrast theme for accessibility",
                ResourcePath = "avares://ProxyStudio/Themes/HighContrast.axaml",
                IsDark = true,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/high-contrast.png"
            },
            new()
            {
                Type = ThemeType.Gaming,
                Name = "Gaming RGB",
                Description = "Vibrant theme with gaming-inspired RGB lighting",
                ResourcePath = "avares://ProxyStudio/Themes/Gaming.axaml",
                IsDark = true,
                PreviewImagePath = "avares://ProxyStudio/Assets/Previews/gaming.png"
            },
            new()
            {
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

        public UpdatedEnhancedThemeService(IConfigManager configManager, ILogger<UpdatedEnhancedThemeService> logger)
        {
            _configManager = configManager;
            _logger = logger;
            _transitionManager = new ThemeTransitionManager();

            // Check for seasonal themes on startup
            CheckSeasonalThemes();
        }

        /// <summary>
        /// Apply theme with support for new ThemeDictionaries format
        /// </summary>
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

                // Apply theme immediately (animation support can be added later)
                await ApplyThemeWithThemeDictionaries(themeDefinition);

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

        /// <summary>
        /// Apply theme that uses ThemeDictionaries format
        /// </summary>
        private async Task ApplyThemeWithThemeDictionaries(ThemeDefinition themeDefinition)
        {
            var app = Application.Current;
            if (app?.Styles == null)
            {
                _logger.LogWarning("Application or Styles collection is null");
                return;
            }

            try
            {
                // Remove existing custom theme styles (but keep FluentTheme and system styles)
                var customThemes = app.Styles
                    .OfType<StyleInclude>()
                    .Where(s => s.Source?.ToString().Contains("Themes/") == true ||
                                s.Source?.ToString().Contains("temp") == true)
                    .ToList();

                foreach (var customTheme in customThemes)
                {
                    app.Styles.Remove(customTheme);
                    _logger.LogDebug("Removed existing theme: {Source}", customTheme.Source);
                }

                // Add the new theme
                var styleInclude = new StyleInclude(new Uri("avares://ProxyStudio/"))
                {
                    Source = new Uri(themeDefinition.ResourcePath)
                };

                app.Styles.Add(styleInclude);
                _logger.LogDebug("Added new theme: {ResourcePath}", themeDefinition.ResourcePath);

                // Set the appropriate theme variant based on the theme
                var themeVariant = themeDefinition.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

                // Apply to main window if available
                if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    desktop.MainWindow.RequestedThemeVariant = themeVariant;
                    _logger.LogDebug("Set main window theme variant to: {ThemeVariant}", themeVariant);
                }

                // Apply globally to application
                app.RequestedThemeVariant = themeVariant;
                _logger.LogDebug("Set application theme variant to: {ThemeVariant}", themeVariant);

                // Small delay to ensure theme is applied
                //await Task.Delay(50);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme with ThemeDictionaries: {ThemeName}", themeDefinition.Name);
                throw;
            }
        }

        /// <summary>
        /// FIXED: Apply custom theme with proper ThemeDictionaries support
        /// </summary>
        public async Task ApplyCustomThemeAsync(string themeXaml, string themeName)
        {
            try
            {
                _logger.LogInformation("Applying custom theme: {ThemeName}", themeName);

                // Validate that the theme contains ThemeDictionaries
                if (!themeXaml.Contains("ThemeDictionaries"))
                {
                    _logger.LogWarning("Custom theme does not contain ThemeDictionaries, may not work correctly");
                    // Convert old format to new format if needed
                    themeXaml = ConvertLegacyThemeToThemeDictionaries(themeXaml);
                }

                // Create temporary theme file
                var tempDir = Path.Combine(Path.GetTempPath(), "ProxyStudio", "Themes");
                Directory.CreateDirectory(tempDir);
                var tempThemeFile = Path.Combine(tempDir, $"{SanitizeFileName(themeName)}.axaml");

                await File.WriteAllTextAsync(tempThemeFile, themeXaml);

                var app = Application.Current;
                if (app?.Styles == null) return;

                // Remove existing custom themes
                var customThemes = app.Styles
                    .OfType<StyleInclude>()
                    .Where(s => s.Source?.ToString().Contains("temp") == true ||
                                s.Source?.ToString().Contains("ProxyStudio/Themes/") == true)
                    .ToList();

                foreach (var customTheme in customThemes)
                {
                    app.Styles.Remove(customTheme);
                }

                // Apply new custom theme
                var styleInclude = new StyleInclude(new Uri("file:///"))
                {
                    Source = new Uri(tempThemeFile)
                };

                // ✅ CRITICAL: Insert theme BEFORE ModernDesignClasses so resources are available
                var modernDesignIndex = app.Styles
                    .OfType<StyleInclude>()
                    .Select((style, index) => new { style, index })
                    .FirstOrDefault(x => x.style.Source?.ToString().Contains("ModernDesign") == true)?.index;

                if (modernDesignIndex.HasValue)
                {
                    app.Styles.Insert(modernDesignIndex.Value, styleInclude);
                    _logger.LogDebug("Inserted custom theme before ModernDesignClasses at index {Index}",
                        modernDesignIndex.Value);
                }
                else
                {
                    app.Styles.Add(styleInclude);
                    _logger.LogDebug("Added custom theme at end of styles");
                }

                // Set the appropriate theme variant
                var themeVariant = DetermineThemeVariantFromXaml(themeXaml);
                if (app.RequestedThemeVariant != themeVariant)
                {
                    app.RequestedThemeVariant = themeVariant;
                    _logger.LogDebug("Set RequestedThemeVariant to: {Variant}", themeVariant);
                }

                _logger.LogInformation("Successfully applied custom theme: {ThemeName}", themeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply custom theme: {ThemeName}", themeName);
                throw;
            }
        }

        /// <summary>
        /// Convert legacy theme format to modern ThemeDictionaries format
        /// </summary>
        private string ConvertLegacyThemeToThemeDictionaries(string legacyXaml)
        {
            try
            {
                _logger.LogInformation("Converting legacy theme to ThemeDictionaries format");

                // This is a simplified conversion - you might need to enhance based on your legacy format
                var sb = new StringBuilder();
                sb.AppendLine("<Styles xmlns=\"https://github.com/avaloniaui\"");
                sb.AppendLine("        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
                sb.AppendLine();
                sb.AppendLine("  <Styles.Resources>");
                sb.AppendLine("    <ResourceDictionary>");
                sb.AppendLine("      <ResourceDictionary.ThemeDictionaries>");
                sb.AppendLine("        <ResourceDictionary x:Key=\"Dark\">");

                // Extract existing color/brush definitions and wrap them in ThemeDictionaries
                // This is a basic implementation - enhance as needed
                var lines = legacyXaml.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("<Color") || line.Contains("<SolidColorBrush"))
                    {
                        sb.AppendLine($"          {line.Trim()}");
                    }
                }

                sb.AppendLine("        </ResourceDictionary>");
                sb.AppendLine("      </ResourceDictionary.ThemeDictionaries>");
                sb.AppendLine("    </ResourceDictionary>");
                sb.AppendLine("  </Styles.Resources>");
                sb.AppendLine("</Styles>");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert legacy theme, using original");
                return legacyXaml;
            }
        }

        /// <summary>
        /// Replace existing theme file with new content
        /// </summary>
        public async Task ReplaceThemeFileAsync(ThemeType themeType, string themeXaml)
        {
            try
            {
                var themeDefinition = AvailableThemes.FirstOrDefault(t => t.Type == themeType);
                if (themeDefinition == null)
                {
                    throw new ArgumentException($"Theme type {themeType} not found");
                }

                // Get the themes directory
                var themesDir = GetThemesDirectory();
                var themeFileName = Path.GetFileName(new Uri(themeDefinition.ResourcePath).LocalPath);
                var themeFilePath = Path.Combine(themesDir, themeFileName);

                // Backup existing theme
                var backupPath = $"{themeFilePath}.backup.{DateTime.Now:yyyyMMdd_HHmmss}";
                if (File.Exists(themeFilePath))
                {
                    File.Copy(themeFilePath, backupPath);
                    _logger.LogInformation("Backed up existing theme to: {BackupPath}", backupPath);
                }

                // Write new theme content
                Directory.CreateDirectory(Path.GetDirectoryName(themeFilePath) ?? themesDir);
                await File.WriteAllTextAsync(themeFilePath, themeXaml);

                _logger.LogInformation("Replaced theme file: {ThemeFilePath}", themeFilePath);

                // Apply the updated theme
                await ApplyThemeAsync(themeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replace theme file for: {ThemeType}", themeType);
                throw;
            }
        }

        /// <summary>
        /// Get themes directory path
        /// </summary>
        public string GetThemesDirectory()
        {
            // Try to get themes directory relative to application
            var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var themesDir = Path.Combine(appDir ?? ".", "Themes");

            // If themes directory doesn't exist, create it
            if (!Directory.Exists(themesDir))
            {
                Directory.CreateDirectory(themesDir);
                _logger.LogInformation("Created themes directory: {ThemesDir}", themesDir);
            }

            return themesDir;
        }

        /// <summary>
        /// Save theme preference to configuration
        /// </summary>
        public bool SaveThemePreference(ThemeType theme)
        {
            try
            {
                _configManager.Config.SelectedTheme = theme;
                _configManager.SaveConfig();
                _logger.LogDebug("Saved theme preference: {Theme}", theme);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save theme preference");
                return false;
            }
        }

        /// <summary>
        /// Load theme preference from configuration
        /// </summary>
        public ThemeType LoadThemePreference()
        {
            try
            {
                var config = _configManager.LoadConfig();
                _logger.LogDebug("Loaded theme preference: {Theme}", config.SelectedTheme);
                return config.SelectedTheme;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load theme preference, using default");
                return ThemeType.DarkProfessional;
            }
        }

        /// <summary>
        /// Check and apply seasonal themes
        /// </summary>
        private void CheckSeasonalThemes()
        {
            var now = DateTime.Now;
            var detectedSeason = SeasonalTheme.None;

            // Christmas: December 1 - January 6
            if (now.Month == 12 || (now.Month == 1 && now.Day <= 6))
                detectedSeason = SeasonalTheme.Christmas;
            // Halloween: October 15-31
            else if (now.Month == 10 && now.Day >= 15)
                detectedSeason = SeasonalTheme.Halloween;
            // Valentine's: February 10-20
            else if (now.Month == 2 && now.Day >= 10 && now.Day <= 20)
                detectedSeason = SeasonalTheme.Valentine;
            // Summer: June-August
            else if (now.Month >= 6 && now.Month <= 8)
                detectedSeason = SeasonalTheme.Summer;
            // Spring: March-May
            else if (now.Month >= 3 && now.Month <= 5)
                detectedSeason = SeasonalTheme.Spring;

            if (detectedSeason != SeasonalTheme.None)
            {
                CurrentSeasonalTheme = detectedSeason;
                SeasonalThemeChanged?.Invoke(this, detectedSeason);
                _logger.LogInformation("Detected seasonal theme: {Season}", detectedSeason);
            }
        }

        /// <summary>
        /// Apply seasonal theme overlay (if available)
        /// </summary>
        private async Task ApplySeasonalThemeAsync(SeasonalTheme seasonalTheme)
        {
            try
            {
                // This is a placeholder for seasonal theme functionality
                // You could implement seasonal overlays or modifications here
                _logger.LogInformation("Seasonal theme feature: {Season} (not yet implemented)", seasonalTheme);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply seasonal theme: {Season}", seasonalTheme);
            }
        }

        /// <summary>
        /// Determine the appropriate theme variant from XAML content
        /// </summary>
        private ThemeVariant DetermineThemeVariantFromXaml(string themeXaml)
        {
            // Look for Dark/Light keys in ThemeDictionaries
            if (themeXaml.Contains("x:Key=\"Dark\""))
            {
                return ThemeVariant.Dark;
            }
            else if (themeXaml.Contains("x:Key=\"Light\""))
            {
                return ThemeVariant.Light;
            }

            // Fallback - analyze color brightness to determine if it's a dark theme
            try
            {
                // Look for background color in the XAML
                var backgroundMatch = System.Text.RegularExpressions.Regex.Match(
                    themeXaml, @"BackgroundColor['""]>#([A-Fa-f0-9]{6})");

                if (backgroundMatch.Success)
                {
                    var colorHex = backgroundMatch.Groups[1].Value;
                    var color = Color.Parse($"#{colorHex}");
                    var brightness = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;

                    return brightness < 0.5 ? ThemeVariant.Dark : ThemeVariant.Light;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to determine theme variant from XAML, defaulting to Dark");
            }

            return ThemeVariant.Dark;
        }

        /// <summary>
        /// Sanitize filename for safe file operations
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName;
        }

        /// <summary>
        /// Theme transition manager for smooth theme changes (placeholder for future enhancement)
        /// </summary>
        public class ThemeTransitionManager
        {
            public async Task TransitionToThemeAsync(ThemeDefinition themeDefinition)
            {
                // Placeholder for smooth theme transitions
                // Could implement fade effects, color transitions, etc.
                await Task.Delay(100);
            }
        }
    }
}