// ProxyStudio/Services/SystemAccentColorService.cs
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Media;
using Microsoft.Extensions.Logging;

namespace ProxyStudio.Services
{
    public interface ISystemAccentColorService
    {
        Color GetSystemAccentColor();
        Task<Color> GetSystemAccentColorAsync();
        event EventHandler<Color> AccentColorChanged;
    }

    public class SystemAccentColorService : ISystemAccentColorService
    {
        private readonly ILogger<SystemAccentColorService> _logger;
        private Color _currentAccentColor = Colors.Blue;

        public event EventHandler<Color>? AccentColorChanged;

        public SystemAccentColorService(ILogger<SystemAccentColorService> logger)
        {
            _logger = logger;
            
            // Start monitoring system accent color changes
            _ = Task.Run(MonitorSystemAccentColor);
        }

        public Color GetSystemAccentColor()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetWindowsAccentColor();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return GetMacAccentColor();
                }
                else
                {
                    return GetLinuxAccentColor();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get system accent color, using default");
                return Colors.Blue;
            }
        }

        public async Task<Color> GetSystemAccentColorAsync()
        {
            return await Task.Run(GetSystemAccentColor);
        }

        private Color GetWindowsAccentColor()
        {
            try
            {
                // Windows Registry approach
                using var key = Microsoft.Win32.Registry.CurrentUser
                    .OpenSubKey(@"Software\Microsoft\Windows\DWM");
                
                if (key?.GetValue("AccentColor") is int accentColorDWord)
                {
                    var bytes = BitConverter.GetBytes(accentColorDWord);
                    return Color.FromArgb(255, bytes[0], bytes[1], bytes[2]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to read Windows accent color from registry");
            }

            return Colors.Blue;
        }

        private Color GetMacAccentColor()
        {
            // macOS system accent color detection
            // This would require native interop or a library like Avalonia.Native
            return Colors.Blue; // Placeholder
        }

        private Color GetLinuxAccentColor()
        {
            // Linux accent color detection (varies by DE)
            // Could check GNOME/KDE/etc. settings
            return Colors.Blue; // Placeholder
        }

        private async Task MonitorSystemAccentColor()
        {
            while (true)
            {
                try
                {
                    var newColor = await GetSystemAccentColorAsync();
                    
                    if (newColor != _currentAccentColor)
                    {
                        _currentAccentColor = newColor;
                        AccentColorChanged?.Invoke(this, newColor);
                        
                        _logger.LogInformation("System accent color changed to: {Color}", newColor);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring system accent color");
                }

                await Task.Delay(5000); // Check every 5 seconds
            }
        }
    }
}