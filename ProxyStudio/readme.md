# ProxyStudio - Proxy Card Management Application - ALPHA!!!!

A powerful desktop application for creating and managing proxy cards for tabletop games, built with Avalonia UI and C#.

## 🎯 Overview

ProxyStudio allows users to create, organize, and print collections of proxy cards with customizable layouts and professional PDF output. Perfect for Magic: The Gathering players, board game enthusiasts, or anyone needing to create custom card collections.

## ✨ Features

### Card Management
- **Visual Card Grid** - Browse cards in a responsive 3-column layout
- **Card Editor** - Select and edit individual cards with preview
- **Image Support** - Load and display high-resolution card images
- **Collection Management** - Add, remove, and organize card collections

### PDF Generation & Printing
- **Multi-Page Support** - Automatically creates multiple pages when needed
- **Customizable Layout** - Configurable cards per row/column (1-10 each)
- **Card Spacing** - Adjustable spacing between cards for cutting
- **Cutting Lines** - Optional cutting guides with customizable color, thickness, and style
- **Professional Output** - High-quality PDF generation using PDFsharp

### Preview System
- **Real-Time Preview** - See exactly how your PDF will look
- **Zoom Controls** - Zoom in/out (25%-200%) for detailed inspection
- **Page Navigation** - Browse through multiple pages with Previous/Next buttons
- **Live Updates** - Preview automatically updates when settings change

### User Experience
- **Persistent Settings** - All preferences saved between sessions
- **Design-Time Support** - Full designer preview in development environments
- **Responsive UI** - Clean, modern interface that adapts to different screen sizes
- **Error Handling** - Robust error handling with helpful debug information

## 🛠 Technical Stack

- **Framework**: .NET 9.0
- **UI**: Avalonia UI (Cross-platform)
- **PDF Generation**: PDFsharp
- **Image Processing**: SixLabors.ImageSharp
- **Architecture**: MVVM with CommunityToolkit.Mvvm
- **Configuration**: XML-based settings with automatic persistence

## 🚀 Getting Started

### Prerequisites
- .NET 9.0 SDK
- Windows, macOS, or Linux

### Building
```bash
git clone [repository-url]
cd ProxyStudio
dotnet restore
dotnet build
```

### Running
```bash
dotnet run
```

## 📋 Usage

1. **Load Cards** - Click "Load Test Cards" to start with sample cards
2. **Configure Layout** - Go to the Printing tab and adjust:
    - Cards per row/column
    - Spacing between cards
    - Cutting line options
3. **Preview** - Use zoom and page navigation to review your layout
4. **Generate PDF** - Click "Generate PDF" to create your printable file

## 🎨 Interface Overview

### Cards Tab
- Left panel: Card management actions
- Center: Visual card grid with selection
- Right panel: Selected card editor with properties

### Printing Tab
- Left panel: PDF generation settings and controls
- Right panel: Live preview with zoom and page navigation

### Settings Tab
- Global application preferences
- Bleed settings and other options

## 🔧 Configuration

Settings are automatically saved to:
- **Windows**: `%AppData%/ProxyStudio/AppConfig.xml`
- **macOS/Linux**: `~/.config/ProxyStudio/AppConfig.xml`

## 📖 Card Formats

ProxyStudio uses standard trading card dimensions:
- **Size**: 2.5" × 3.5" (63mm × 88mm)
- **Resolution**: 300 DPI (750×1050 pixels)
- **Format**: Supports common image formats via ImageSharp

## 🤝 Contributing

This project uses modern C# development practices:
- MVVM architecture with proper separation of concerns
- Dependency injection for testability
- Observable properties with automatic change notification
- Comprehensive error handling and logging

## 📄 License

not defined yet

## 🔮 Future Features

- Custom card templates
- Import from various card databases
- Advanced cutting line options
- Batch processing capabilities
- Print job management

---

*Built with ❤️ for the tabletop gaming community*