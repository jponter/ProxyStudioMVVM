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