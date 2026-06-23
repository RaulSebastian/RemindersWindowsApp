# RemindersApp

A native Windows desktop application that provides a seamless interface to iCloud Reminders, built with WPF and WebView2.

## Overview

RemindersApp is a lightweight Windows application that wraps the iCloud Reminders web interface in a native WPF window. It provides a clean, focused experience for managing your iCloud reminders without the overhead of a full browser.

## Features

- **Native Windows Integration**: Built with WPF for native Windows look and feel
- **Dark Header**: Custom-styled dark header matching iCloud design with Apple logo, "iCloud" and "Reminders" branding
- **Security**: Restricts navigation to iCloud Reminders domain only
- **Link Filtering**: Hides non-reminder links to keep focus on reminders
- **Auto-save**: Remembers login credentials for seamless returning users
- **Clean UI**: Hides iCloud header chrome for distraction-free experience
- **Loading Indicators**: Shows status and loading information during navigation

## Technology Stack

- **Framework**: .NET 10.0 (Windows)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Web Engine**: WebView2 (Chromium-based)
- **Language**: C#

## Prerequisites

- Windows 10 or later
- .NET 10 SDK (for building)
- WebView2 Runtime (automatically installed with the app on first run)

## Building

```bash
cd src/RemindersApp
dotnet build -c Release
```

## Running

After building, the executable is located at:
> src/RemindersApp/bin/Release/net10.0-windows/RemindersApp.exe

Or run directly with:

```bash
dotnet run -c Release --project src/RemindersApp/RemindersApp.csproj
```

## Key Components

### MainWindow

The main application window that hosts the WebView2 control and manages:

- WebView2 configuration and initialization
- Navigation security and filtering
- Window chrome and controls
- Script injection for UI customization

### NavigationGuard

Handles URL validation to ensure navigation stays within allowed iCloud domains:

- Restricts to iCloud Reminders domain
- Allows authentication domains
- Blocks external navigation

### UI Customization Scripts

JavaScript injected into the WebView2 to:

- Hide the iCloud header chrome
- Filter and hide non-reminder navigation links
- Handle dynamic content updates via MutationObserver

## Development

### Running Tests

```bash
dotnet test tests/RemindersApp.Tests/RemindersApp.Tests.csproj
```

## Known Limitations

- Requires internet connection for iCloud Reminders
- Single-window experience (no multi-window support)
- No offline support

## License

Private project. See individual files for licensing information.

## Support

For issues or feature requests, please check the test suite and existing implementation.
