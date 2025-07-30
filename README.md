Todo: 

implement API for google drive requests

change all logging to use serilog sinks


# ProxyStudio Alpha

A powerful desktop application for creating and managing proxy cards for tabletop games, built with Avalonia UI and C#.

## 🥰 ##
Big thanks to malacath-92 for the advice and support! Check out his proxy app https://github.com/Malacath-92/Proxy-PDF-Maker

and the original og Alex Taxiera whos proxy print setup was my original goto for proxy pdf generation and inspired me to write my own!

## 🎯 Overview

ProxyStudio allows users to create, organize, and print collections of proxy cards with customizable layouts and professional PDF output. Perfect for Magic: The Gathering players, board game enthusiasts, or anyone needing to create custom card collections.

## ✨ Features

### Card Management
- **Visual Card Grid** - Browse cards in a responsive 3-column layout
- **MPC XML Loading** - Drag an exported XML from MPC Fill
- **Single Card Loading** - Drag a JPG/PNG onto the card management screen
- 


### PDF Generation & Printing
- **Multi-Page Support** - Automatically creates multiple pages when needed
- **Automatic Layout** - portait (3x3) or landscape (4x2)
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
- **Error Handling** - Robust error handling with helpful debug information - ehhhh kinda
- **Cached Image Downloading from MPCFill** currently cached in the user data folder
- **Bleed Edges** supported and can be removed from any card (enabled by default for MPCFill import)

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
- Windows Only currently, I need to remove some platform-specific code so it compiles on Linux & MacOS - gladly accepting help for this.




## 🎨 Interface Overview

### Cards Tab
- Left panel: Card management actions
- Center: Visual card grid with selection
- Right panel: Selected card editor with bleed properties

### Printing Tab
- Left panel: PDF generation settings and controls
- Right panel: Live preview with zoom and page navigation

### Settings Tab
- Global application preferences
- Global Bleed settings and other options

## 🔧 Configuration

Settings are automatically saved to:
- **Windows**: `%AppData%/ProxyStudio/AppConfig.xml`
- **macOS/Linux**: `~/.config/ProxyStudio/AppConfig.xml`

## 📖 Card Formats

ProxyStudio uses standard trading card dimensions:
- **Size**: 2.5" × 3.5" (63mm × 88mm)
- **Resolution**: 300 DPI (750×1050 pixels), 600 and 1200 DPI pdf generation also supported, but this is SLOWWWWWWWW!
- **Format**: Supports common image formats via ImageSharp

## 🤝 Contributing

This project uses modern C# development practices:
- MVVM architecture with proper separation of concerns
- Dependency injection for testability
- Observable properties with automatic change notification
- Comprehensive error handling and logging

## 📄 License

not decided yet

## 🔮 Future Features

- Custom card templates - maybe, probably not though as this is heavily MTG focussed
- Import from various card databases - DONE
- Advanced cutting line options - DONE



---

*Built with ❤️ for the tabletop gaming community*
