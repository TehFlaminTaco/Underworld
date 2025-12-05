# Underworld

A cross-platform DooM launcher for managing source ports, IWADs, mods, and game profiles with dedicated save directories.

![Version](https://img.shields.io/badge/version-1.3.1-blue)
![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20Windows%20%7C%20macOS-lightgrey)

---

## For Users

### What is Underworld?

Underworld is a graphical launcher for DooM and DooM-engine games. It helps you:

- **Manage Source Ports** - Keep track of multiple DooM executables (GZDoom, Zandronum, etc.)
- **Organize IWADs** - Auto-discover your game IWADs (DOOM.WAD, DOOM2.WAD, etc.)
- **Select Mods** - Browse and select PWADs/PK3s with metadata display
- **Create Profiles** - Save different mod configurations with separate save directories
- **Launch Games** - One-click launching with proper command-line arguments

### Installation

#### Download from Releases

1. Go to the [Releases](https://github.com/TehFlaminTaco/Underworld/releases) page
2. Download the appropriate package for your platform:
   - **Linux**: `Underworld-linux-x64.zip`
   - **Windows**: `Underworld-win-x64.zip`
   - **macOS**: `Underworld-osx-x64.zip`
3. Extract and run the `Underworld` executable

#### Build from Source

See the [Developer Guide](#for-developers) section below.

### Getting Started

1. **Add Source Ports**
   - Click the "Add" button next to the Executables dropdown
   - Select your DooM executable(s) (e.g., `gzdoom`, `chocolate-doom`)

2. **Configure Data Directories**
   - Go to `Data → Manage Data Folders`
   - Add directories where your WAD files are located
   - Underworld also reads `DOOMWADDIR` and `DOOMWADPATH` environment variables

3. **Select Your Game**
   - Choose an IWAD from the dropdown (auto-detected from data directories)
   - Browse available mods in the left grid
   - Select mods to add them to your load list (right grid)

4. **Create a Profile** (Optional)
   - Click "New Profile" to save your current configuration
   - Profiles have separate save directories: `./saves/{ProfileName}/`
   - Lock profiles to prevent accidental changes

5. **Launch!**
   - Click "Run Game" to start DooM with your selected configuration

### Features

- ✅ **Cross-Platform** - Runs on Linux, Windows, and macOS
- ✅ **Auto-Discovery** - Finds IWADs and mods from configured directories
- ✅ **Profile System** - Multiple configurations with isolated save directories
- ✅ **WAD Metadata** - Displays map counts and mod information
- ✅ **Search Filters** - Quickly find mods in large collection

### System Requirements

- **.NET 9.0 Runtime** (included in self-contained builds)
- **Linux**: X11 or Wayland display server
- **Windows**: Windows 10 or later
- **macOS**: macOS 10.15 or later

---

## For Developers

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Your favorite code editor (VS Code, Rider, Visual Studio)

### Building

```bash
# Clone the repository
git clone https://github.com/TehFlaminTaco/Underworld.git
cd Underworld

# Build
dotnet build Underworld/Underworld.csproj

# Run
dotnet run --project Underworld/Underworld.csproj
```

### Testing

```bash
# Run all tests
dotnet test Underworld.Tests/Underworld.Tests.csproj && \
dotnet test Underworld.ViewModelTests/Underworld.ViewModelTests.csproj

# 21 tests total (18 ViewModel tests + 3 Manager tests)
```

### Project Structure

```
Underworld/
├── Underworld/                    # Main application (MVVM architecture)
│   ├── Models/                    # Data models (ExecutableItem, Profile, IWad, etc.)
│   ├── ViewModels/                # ViewModels with ObservableObject
│   ├── Views/                     # Avalonia XAML views
│   └── Assets/                    # Icons and resources
├── Underworld.Tests/              # Unit tests for managers
├── Underworld.ViewModelTests/     # ViewModel integration tests
```

### Technology Stack

- **Framework**: Avalonia UI 11.3.9 (cross-platform XAML-based UI)
- **Architecture**: MVVM with CommunityToolkit.Mvvm 8.2.1
- **Testing**: xUnit 2.5.3
- **Target**: .NET 9.0
- **Config**: Single JSON file (`Underworld.config.json`)

### Key Features for Contributors

- **Observable Properties**: Uses `[ObservableProperty]` source generators
- **Config Persistence**: Static `Config.Setup<T>(key, default)` pattern
- **WAD Discovery**: Automatic scanning and caching with metadata extraction
- **Cross-Platform Validation**: Platform-specific executable detection
- **Profile Management**: Isolated save directories per profile

### Publishing

```bash
# Publish for Linux
dotnet publish Underworld/Underworld.csproj -c Release -r linux-x64 --self-contained

# Publish for Windows
dotnet publish Underworld/Underworld.csproj -c Release -r win-x64 --self-contained

# Publish for macOS
dotnet publish Underworld/Underworld.csproj -c Release -r osx-x64 --self-contained
```

### Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

---

## AI Disclaimer

AI (GitHub Copilot with Claude Sonnet 3.5) was used to assist in the creation of this project, primarily to help with UI integration, MVVM architecture, and code scaffolding.

Additionally, ChatGPT was used to help design the logo.

I encourage contributors to improve upon the AI-generated code where appropriate.
