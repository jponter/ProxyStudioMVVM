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
        Task<bool> SaveThemePreferenceAsync(ThemeType theme);
        Task<ThemeType> LoadThemePreferenceAsync();
        
        event EventHandler<ThemeType> ThemeChanged;
    }
}