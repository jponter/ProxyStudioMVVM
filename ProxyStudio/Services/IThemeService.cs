using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProxyStudio.Services
{
    public enum ThemeType
    {
        DarkProfessional,
        LightClassic,
        HighContrast,
        Gaming,
        Minimal
    }

    public class ThemeDefinition
    {
        public ThemeType Type { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string ResourcePath { get; set; } = "";
        public bool IsDark { get; set; }
        public string PreviewImagePath { get; set; } = "";
    }

    public interface IThemeService
    {
        ThemeType CurrentTheme { get; }
        IReadOnlyList<ThemeDefinition> AvailableThemes { get; }
    
        Task ApplyThemeAsync(ThemeType theme);
        bool SaveThemePreference(ThemeType theme); // Remove async
        ThemeType LoadThemePreference(); // Remove async
    
        event EventHandler<ThemeType> ThemeChanged;
        
        /// <summary>
        /// Applies a custom theme from XAML content
        /// </summary>
        Task ApplyCustomThemeAsync(string themeXaml, string themeName);
    
        /// <summary>
        /// Replaces an existing theme file with new content
        /// </summary>
        Task ReplaceThemeFileAsync(ThemeType themeType, string themeXaml);
    
        /// <summary>
        /// Gets the themes directory path
        /// </summary>
        string GetThemesDirectory();
    }
}