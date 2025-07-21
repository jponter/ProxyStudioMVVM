# ProxyStudio Development Summary & Implementation Guide

## 📋 Project Overview
ProxyStudio is a desktop application built with Avalonia UI (.NET 9) for creating high-quality proxy cards for tabletop games. The application generates professional PDFs with precise 2.5" × 3.5" card dimensions at configurable DPI settings.

## 🎯 Core Requirements Implemented
- **Fixed Card Dimensions**: Always exactly 2.5" × 3.5" regardless of DPI
- **High-DPI PDF Generation**: 150-1200 DPI support with proper image scaling
- **Progress Reporting**: Real-time feedback during PDF generation
- **Settings Persistence**: XML-based configuration that survives app restarts
- **Live Preview**: Real-time preview with zoom and page navigation
- **Professional Output**: PDFs with cutting lines and customizable layouts

## 🔧 Major Technical Implementation Details

### **1. High-DPI PDF Generation**
**Problem Solved**: Original implementation scaled images at startup, causing fixed file sizes regardless of DPI setting.

**Solution Implemented**:
- Store images at high resolution (600 DPI base: 1500×2100 pixels)
- Scale images dynamically during PDF generation via `ProcessImageForHighDpiPdf()`
- Use `DrawCard()` method that calls high-DPI processing for each card
- **Critical**: Removed graphics context scaling to maintain exact 2.5"×3.5" dimensions

**Key Files Modified**:
- `PdfGenerationService.cs`: Complete rewrite with proper DPI handling
- `MainViewModel.cs`: Updated `AddTestCards()` to store high-res images

**Result**: File sizes now scale correctly:
- 300 DPI: ~0.43 MB (8 cards)
- 600 DPI: ~8.5 MB (8 cards)
- 1200 DPI: ~30 MB (8 cards)

### **2. Progress Reporting System**
**Problem Solved**: PDF generation was slow with no user feedback.

**Solution Implemented**:
- Added `IProgress<PdfGenerationProgress>` interface to `IPdfGenerationService`
- Created `PdfGenerationProgress` class with detailed progress tracking
- Updated UI with progress bars, time estimates, and current operation status
- Added per-card processing progress with individual card names

**Key Files Modified**:
- `IPdfGenerationService.cs`: Added progress parameter to `GeneratePdfAsync()`
- `PrintViewModel.cs`: Added progress reporting properties and UI updates
- `PrintingView.axaml`: Added progress UI components
- `PdfGenerationService.cs`: Added `DrawCardGridWithProgress()` method

### **3. Settings Persistence Issues**
**Problem Solved**: Settings were being overwritten during initialization, reverting to defaults.

**Solution Implemented**:
- Added `_isInitializing` flag to prevent saving during construction
- Updated all property change handlers to check flag before saving
- Enhanced `LoadSettings()` to load ALL config values (was missing several)
- Fixed constructor to use neutral defaults that don't conflict with config

**Key Files Modified**:
- `PrintViewModel.cs`: Complete rewrite with initialization protection
- All property change handlers updated with `!_isInitializing` checks

**Critical Constructor Changes**:
```csharp
// OLD (caused issues):
CardSpacing = 10m;
EnsureMinimumPrintDpi = true;

// NEW (fixed):
CardSpacing = 0m;
EnsureMinimumPrintDpi = false;
```

### **4. File Size Estimation System**
**Problem Solved**: Theoretical calculations were wildly inaccurate (estimated 12MB vs actual 0.43MB).

**Solution Implemented**:
- Used piecewise linear interpolation based on actual test data
- Exact accuracy at known points (300, 600, 1200 DPI)
- Smooth interpolation between known values

**Formula**:
```csharp
// 300-600 DPI: Linear interpolation between 0.054 and 1.063 MB per card
// 600-1200 DPI: Linear interpolation between 1.063 and 3.75 MB per card
// Outside range: Reasonable extrapolation
```

### **5. Configuration Architecture**
**Current Structure**:
```csharp
AppConfig
├── GlobalBleedEnabled
├── Window settings (width, height, position, state)
└── PdfSettings
    ├── PrintDpi (300 default)
    ├── EnsureMinimumPrintDpi (true)
    ├── MinimumPrintDpi (300)
    ├── Layout settings (cards per row/column, spacing)
    ├── Cutting line settings
    ├── Preview settings (separate from print)
    └── Output settings (path, filename)
```

## 🏗️ Architecture Overview

### **MVVM Pattern**:
- `MainViewModel`: Main application state and card management
- `PrintViewModel`: PDF generation and print settings
- `Card` & `CardCollection`: Model classes with proper change notification
- Views: `MainView.axaml`, `PrintingView.axaml`

### **Services**:
- `IPdfGenerationService`: PDF creation with progress reporting
- `IConfigManager`: XML-based settings persistence
- Design-time implementations for both services

### **Key Dependencies**:
- **UI**: Avalonia 11.3.2 with Fluent theme
- **PDF**: PDFsharp 6.2.0 for PDF generation
- **Images**: SixLabors.ImageSharp 3.1.10 for high-quality image processing
- **MVVM**: CommunityToolkit.Mvvm 8.4.0 for property notifications and commands

## 🚨 Critical Implementation Notes

### **1. Card Dimensions Are Sacred**
- Cards are ALWAYS exactly 2.5" × 3.5" (180pt × 252pt in PDFsharp)
- DPI only affects image resolution/quality, never physical size
- Never apply graphics context scaling - handle DPI through image processing only

### **2. Image Processing Pipeline**:
```
Source Images (any size) 
→ Resize to 600 DPI base (1500×2100) at startup
→ Store as high-quality PNG in Card.ImageData
→ During PDF generation: ProcessImageForHighDpiPdf()
→ Scale to exact target DPI (e.g., 750×1050 for 300 DPI)  
→ Save as JPEG (95% quality) or PNG (600+ DPI)
```

### **3. Settings Loading Order**:
```
Constructor → Set neutral defaults → LoadSettings() → Override with config values → Set _isInitializing = false
```

### **4. Progress Reporting**:
- Total steps = cards + pages + setup + finalization
- Report progress per card during processing
- Include time estimates and current operation
- Update UI on main thread using Progress<T>

## 🐛 Known Issues & Limitations

### **Fixed Issues**:
- ✅ DPI scaling now works correctly
- ✅ Settings persistence fixed
- ✅ File size estimation accurate
- ✅ Progress reporting implemented
- ✅ Cards maintain exact dimensions

### **Current Limitations**:
- No cancellation support for PDF generation (UI ready, not implemented)
- Preview uses System.Drawing for rendering (works but could be optimized)
- Font resolver warnings in PDFsharp (harmless but noisy in logs)

## 🔄 Testing Verification

### **DPI Scaling Test**:
Generate PDFs at different DPI settings and verify:
- File sizes scale correctly (quadratically with DPI)
- Cards print at exactly 2.5" × 3.5"
- Image quality improves with higher DPI

### **Settings Persistence Test**:
1. Change settings (DPI, orientation, spacing, etc.)
2. Close and restart application
3. Verify all settings loaded correctly
4. Check debug logs show "LoadSettings completed" with correct values

### **Progress Reporting Test**:
Generate PDF and verify:
- Progress bar shows 0-100% completion
- Time estimates appear and are reasonable
- Current operation and card names display
- UI remains responsive during generation

## 📁 Key Files Summary

### **Core Application**:
- `MainViewModel.cs`: Card management, calls PrintViewModel.RefreshCardInfo() after loading cards
- `MainView.axaml`: Main UI with tabs for Cards/Printing/Settings

### **Print System**:
- `PrintViewModel.cs`: Complete rewrite with initialization protection
- `PrintingView.axaml`: Print controls with progress reporting UI
- `PdfGenerationService.cs`: High-DPI PDF generation with progress

### **Configuration**:
- `AppConfig.cs`: Added PrintDpi, EnsureMinimumPrintDpi, MinimumPrintDpi
- `ConfigManager.cs`: XML serialization/deserialization

### **Models**:
- `Card.cs`: Stores high-resolution image data
- `CardCollection.cs`: Observable collection with utility methods

## 🔮 Future Enhancements Ready
- **Cancellation Support**: UI and progress infrastructure ready
- **Custom Card Templates**: Architecture supports extensible card layouts
- **Batch Processing**: Progress system scales to multiple operations
- **Advanced Preview**: Could add per-page DPI preview options

---

**Last Updated**: January 2025  
**Status**: Fully functional with high-quality PDF generation and comprehensive settings management