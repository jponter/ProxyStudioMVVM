using System;
using Avalonia.Controls;

namespace ProxyStudio.Helpers
{
    public class AppConfig
    {
        // Existing window settings
        public int WindowWidth { get; set; } = 1400;
        public int WindowHeight { get; set; } = 900;
        public int WindowLeft { get; set; } = 100;
        public int WindowTop { get; set; } = 100;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        
        // Existing card settings
        public bool GlobalBleedEnabled { get; set; } = false;
        
        // PDF Generation Settings
        public PdfSettings PdfSettings { get; set; } = new PdfSettings();
    }

    public class PdfSettings
    {
        // Page settings
        public string PageSize { get; set; } = "A4";
        public bool IsPortrait { get; set; } = true;
        
        // Card layout - Fixed to 3x3 by default
        public int CardsPerRow { get; set; } = 3;
        public int CardsPerColumn { get; set; } = 3;
        public float CardSpacing { get; set; } = 0f; // No spacing for cutting
        
        // Print Resolution Settings - NEW
        public int PrintDpi { get; set; } = 300; // High-quality print resolution
        public bool EnsureMinimumPrintDpi { get; set; } = true; // Enforce minimum 300 DPI
        public int MinimumPrintDpi { get; set; } = 300; // Minimum allowed DPI
        
        // Cutting lines
        public bool ShowCuttingLines { get; set; } = true;
        public string CuttingLineColor { get; set; } = "#FF0000"; // Red
        public bool IsCuttingLineDashed { get; set; } = false;
        public float CuttingLineExtension { get; set; } = 10f;
        public float CuttingLineThickness { get; set; } = 2f; // Thicker for visibility
        
        // Margins
        public float TopMargin { get; set; } = 20f;
        public float BottomMargin { get; set; } = 20f;
        public float LeftMargin { get; set; } = 20f;
        public float RightMargin { get; set; } = 20f;
        
        // Preview settings (separate from print settings)
        public int PreviewDpi { get; set; } = 150; // Lower for performance
        public int PreviewQuality { get; set; } = 85;
        
        // Output settings
        public string DefaultOutputPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        public string DefaultFileName { get; set; } = "ProxyCards";
    }
}