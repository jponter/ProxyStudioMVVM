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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProxyStudio.Services
{
    public enum ThemeType
    {
        DarkProfessional,
        LightClassic,
        DarkRed,
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