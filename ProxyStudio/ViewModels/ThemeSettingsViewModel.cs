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

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProxyStudio.Services;

namespace ProxyStudio.ViewModels
{
    public partial class ThemeSettingsViewModel : ViewModelBase
    {
        private readonly IThemeService _themeService;
        private readonly ILogger<ThemeSettingsViewModel> _logger;
        
        [ObservableProperty] private ThemeDefinition? _selectedTheme;
        [ObservableProperty] private bool _isApplyingTheme;

        public ObservableCollection<ThemeDefinition> AvailableThemes { get; }

        public ThemeSettingsViewModel(IThemeService themeService, ILogger<ThemeSettingsViewModel> logger, IErrorHandlingService errorHandlingService)
        {
            _themeService = themeService;
            _logger = logger;
            AvailableThemes = new ObservableCollection<ThemeDefinition>(_themeService.AvailableThemes);
            
            // Set current theme as selected
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Type == _themeService.CurrentTheme);
        }

        [RelayCommand]
        private async Task ApplyThemeAsync(ThemeDefinition theme)
        {
            if (IsApplyingTheme || theme == null) return;

            try
            {
                _logger.LogDebug("Applying theme (Async): {ThemeName}", theme.Name);
                IsApplyingTheme = true;
                await _themeService.ApplyThemeAsync(theme.Type);
                _themeService.SaveThemePreference(theme.Type);
                SelectedTheme = theme;
            }
            finally
            {
                IsApplyingTheme = false;
            }
        }

        [RelayCommand]
        private async Task PreviewThemeAsync(ThemeDefinition theme)
        {
            if (theme == null) return;
            
            // Apply theme temporarily without saving
            _logger.LogDebug("Previewing theme: {ThemeName}", theme.Name);
            await _themeService.ApplyThemeAsync(theme.Type);
        }

        [RelayCommand]
        private async Task ResetToSavedThemeAsync()
        {
            _logger.LogDebug("Resetting to saved theme");
            var savedTheme =  _themeService.LoadThemePreference();
            await _themeService.ApplyThemeAsync(savedTheme);
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Type == savedTheme);
        }
    }
}