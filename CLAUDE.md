# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Development Commands

### Standard Development
```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build -c Release

# Run the application (requires .NET 9.0 runtime)
dotnet run --project AvaRoomAssign/AvaRoomAssign.csproj
```

### AOT (Ahead-of-Time) Compilation
```bash
# PowerShell script for AOT single-file compilation (recommended)
powershell -ExecutionPolicy Bypass -File Build-AOT.ps1

# Batch script alternative
build-aot.bat

# Manual AOT compilation
dotnet publish AvaRoomAssign/AvaRoomAssign.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishAot=true
```

### Project Structure Commands
```bash
# Clean build artifacts
dotnet clean AvaRoomAssign/AvaRoomAssign.csproj

# Force terminate any running instances
taskkill /F /IM AvaRoomAssign.exe 2>nul
```

## Architecture Overview

This is a C# .NET 9.0 Avalonia UI desktop application for automated room rental selection, optimized for AOT compilation and single-file deployment.

### Core Architecture Pattern
- **UI Framework**: Avalonia UI 11.3.2 with MVVM pattern using CommunityToolkit.Mvvm
- **Automation Modes**: Dual-mode operation (Selenium WebDriver + HTTP API)
- **Configuration**: JSON-based with AOT-friendly source generation
- **Logging**: Custom AOT-compatible logging system with UI thread marshalling

### Key Architectural Components

#### Models Layer (`AvaRoomAssign.Models`)
- **ConfigManager**: AOT-optimized JSON configuration with source generators
- **HouseCondition**: Property change tracking for community selection criteria
- **ISelector**: Interface for automation strategies (DriverSelector/HttpSelector)
- **LogManager**: Thread-safe, UI-aware logging system for AOT environments

#### ViewModels Layer (`AvaRoomAssign.ViewModels`) 
- **MainWindowViewModel**: Central coordination of UI state and automation workflows
- **Property Binding**: Compiled bindings enabled (`AvaloniaUseCompiledBindingsByDefault=true`)

#### Automation Strategies
1. **DriverSelector**: Selenium WebDriver automation (Chrome/Edge)
2. **HttpSelector**: Direct HTTP API calls with three operational modes:
   - Standard condition-based filtering
   - Pre-fetched room ID acceleration (performance optimization)
   - Manual room ID targeting (precision mode)

### AOT Optimization Features

#### Compilation Configuration
- **Trimming**: Aggressive full trimming (`TrimMode=full`) with custom descriptors
- **Linking**: `ILLink.Descriptors.xml` preserves critical types for reflection
- **Serialization**: JSON source generation via `ConfigJsonContext`
- **UI Binding**: Compile-time binding generation for performance

#### Runtime Considerations
- No reflection-based enum processing - manual switch statements used
- Thread-safe logging with UI dispatcher marshalling
- Cached configuration loading to minimize I/O operations
- Custom data converters for MVVM binding scenarios

## Development Guidelines

### Working with Configuration
- Configuration auto-saves on property changes
- Located at `%APPDATA%\AvaRoomAssign\config.json`
- Fallback to application directory for portability
- JSON serialization uses source generators (`ConfigJsonContext`)

### Logging System Usage
```csharp
// AOT-friendly logging - no string interpolation in hot paths
LogManager.Success("Operation completed");
LogManager.Error("Error message", exception);
LogManager.Info($"Status: {status}"); // OK for non-performance critical paths
```

### Adding New Configuration Properties
1. Add property to `AppConfig` class
2. Update `ConfigJsonContext` serialization attributes
3. Add to ViewModel with `[ObservableProperty]`
4. Include in save/load methods

### WebDriver Integration
- ChromeDriver and EdgeDriver versions must match installed browser versions
- Selenium automation runs in background tasks with cancellation token support
- Cookie extraction uses dedicated `CookieManager` utility

### HTTP Mode Architecture
- Three-tier strategy: conditions → pre-fetch → manual IDs
- Room ID caching for performance optimization
- Thread-safe request handling with configurable intervals

## Technical Constraints

### AOT Compilation Requirements
- Avoid dynamic type creation and reflection
- Use source generators for JSON serialization
- Pre-declare all types in `ILLink.Descriptors.xml` if needed
- Manual enum description mapping instead of reflection-based attributes

### UI Thread Safety
- All UI updates must use `Dispatcher.UIThread.Post()` or `InvokeAsync()`
- Observable collections require UI thread for modifications
- Logging system automatically handles thread marshalling

### Performance Considerations
- Configuration caching prevents excessive file I/O
- Log text truncation maintains UI responsiveness (50-line limit)
- Compiled bindings reduce runtime reflection overhead
- Pre-fetched room IDs bypass query delays in HTTP mode

## Deployment Notes

The application supports both standard .NET deployment and AOT single-file deployment:
- **AOT Version**: No runtime dependencies, ~25-40MB single executable
- **Standard Version**: Requires .NET 9.0 runtime, multiple assemblies
- **Target Platform**: Windows x64 (configurable via Runtime Identifier)

Use `Build-AOT.ps1` for production deployments as it provides optimal performance and deployment simplicity.