using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using ProxyStudio.Services;

namespace ProxyStudio.ViewModels
{
    public class DesignTimeMainViewModel : MainViewModel
    {
        public DesignTimeMainViewModel() : base(new DesignTimeConfigManager(), new DesignTimePdfService())
        {
            // Add some design-time specific setup if needed
            // The base constructor will already call AddTestCards() and initialize PrintViewModel
        }
    }
    
    /// <summary>
    /// Design-time implementation of IConfigManager that provides dummy data
    /// </summary>
    public class DesignTimeConfigManager : IConfigManager
    {
        public AppConfig Config { get; } = new AppConfig();
        
        public Task SaveConfigAsync()
        {
            return Task.CompletedTask;
        }

        public Task<AppConfig> LoadConfigAsync()
        {
            return Task.FromResult(Config);
        }

        public void UpdateConfig(Action<AppConfig> updateAction)
        {
            updateAction?.Invoke(Config);
        }

        public AppConfig LoadConfig()
        {
            return Config;
        }

        public void SaveConfig()
        {
            // Do nothing in design time
        }
    }

    /// <summary>
    /// Design-time implementation of IPdfGenerationService
    /// </summary>
    public class DesignTimePdfService : IPdfGenerationService
    {
        public Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options)
        {
            return Task.FromResult(new byte[0]);
        }

        public Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options)
        {
            // Return null for design time - the UI should handle this gracefully
            return Task.FromResult<Bitmap>(null!);
        }
    }
}