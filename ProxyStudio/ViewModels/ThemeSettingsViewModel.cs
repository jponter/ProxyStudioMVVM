using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProxyStudio.Services;

namespace ProxyStudio.ViewModels
{
    public partial class ThemeSettingsViewModel : ViewModelBase
    {
        private readonly IThemeService _themeService;
        
        [ObservableProperty] private ThemeDefinition? _selectedTheme;
        [ObservableProperty] private bool _isApplyingTheme;

        public ObservableCollection<ThemeDefinition> AvailableThemes { get; }

        public ThemeSettingsViewModel(IThemeService themeService)
        {
            _themeService = themeService;
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
            await _themeService.ApplyThemeAsync(theme.Type);
        }

        [RelayCommand]
        private async Task ResetToSavedThemeAsync()
        {
            var savedTheme =  _themeService.LoadThemePreference();
            await _themeService.ApplyThemeAsync(savedTheme);
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Type == savedTheme);
        }
    }
}