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
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ProxyStudio.Helpers;
using ProxyStudio.Models;
using ProxyStudio.Services;

namespace ProxyStudio.ViewModels
{
    public class DesignTimeMainViewModel : MainViewModel
    {
        public DesignTimeMainViewModel() : base(new DesignTimeConfigManager())
        {
            try
            {
                // Only run in actual design mode to avoid runtime issues
                if (!Design.IsDesignMode)
                    return;
                    
                // Add some design-time specific setup if needed
                // The base constructor will already call AddTestCards() and initialize PrintViewModel
                DebugHelper.WriteDebug("DesignTimeMainViewModel created successfully");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteDebug($"Error in DesignTimeMainViewModel: {ex.Message}");
                // In design mode, create minimal working state
                if (Design.IsDesignMode)
                {
                    // Clear existing cards and add simple design-time cards
                    Cards.RemoveAllCards();
                    for (int i = 0; i < 3; i++)
                    {
                        var designCard = new Card($"Design Card {i + 1}", $"ID{i + 1}", "query");
                        Cards.AddCard(designCard);
                    }
                }
            }
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
            updateAction.Invoke(Config);
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
    /// <summary>
    /// Design-time implementation of IPdfGenerationService
    /// </summary>
    public class DesignTimePdfService : IPdfGenerationService
    {
        public Task<byte[]> GeneratePdfAsync(CardCollection cards, PdfGenerationOptions options, IProgress<PdfGenerationProgress>? progress = null)
        {
            // Simulate some progress reporting for design-time
            if (progress != null)
            {
                var progressInfo = new PdfGenerationProgress
                {
                    CurrentStep = 1,
                    TotalSteps = 1,
                    CurrentOperation = "Design-time PDF generation",
                    CurrentCardName = "Sample Card",
                    CurrentPage = 1,
                    TotalPages = 1
                };
                progress.Report(progressInfo);
            }
        
            return Task.FromResult(new byte[1024]); // Return dummy PDF data
        }

        public Task<Bitmap> GeneratePreviewImageAsync(CardCollection cards, PdfGenerationOptions options)
        {
            // Return null for design time - the UI should handle this gracefully
            return Task.FromResult<Bitmap>(null!);
        }
    }
}