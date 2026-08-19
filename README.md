# Alpha Premier Realty Branding Studio

Alpha Premier Realty Branding Studio is a native Windows desktop application for branding property photography. The original repository was a static browser tool; it has been refactored into a real C#/.NET 8 WPF application with XAML UI and a packaged Windows installer.

## Current application

- Native WPF controls and XAML UI; no browser shell or webview.
- Dark charcoal and gold Alpha Premier Realty branding.
- Native multi-file image selection.
- Applies the local Alpha branding frame to each selected photo.
- Preserves the original fixed output contract: images are stretched to `1200x1000`.
- Encodes output as high-quality JPG.
- Generates safe sequential filenames with Windows/ZIP-safe prefix handling.
- Provides live output previews and previous/next full-size preview navigation.
- Saves individual branded JPG files through native Save dialogs.
- Exports all processed images to a ZIP archive.
- Processes files locally; there is no server, cloud upload, database, account, or persistence layer.

## Technology

- C#
- .NET 8
- WPF and XAML
- `SixLabors.ImageSharp` for local image composition and JPEG encoding
- `System.IO.Compression` for ZIP export
- xUnit for focused filename, image-processing, overlay, and ZIP tests

The application project is `src/Alpha.Branding/Alpha.Branding.csproj` and produces a Windows executable (`WinExe`) targeting `net8.0-windows`.

## Download the installer

The latest packaged installer is available from the GitHub Releases page:

[Download the latest Alpha Premier Realty Branding Studio release](https://github.com/Deign86/Alpha_Branding/releases/latest)

Release asset:

```text
Alpha.Branding.Setup.exe
```

The installer is a self-contained `win-x64` native C# bootstrapper. It:

- Requires no separate .NET Desktop Runtime.
- Installs per-user under `%LOCALAPPDATA%\Alpha Premier Realty\Branding Studio`.
- Requires no administrator access, MSIX, certificate, or root-certificate installation.
- Creates a Start Menu shortcut.
- Creates an HKCU Apps & Features uninstall entry.
- Supports uninstall with the installed command:

```powershell
Alpha.Branding.Setup.exe --uninstall
```

The installer is not code-signed, so Windows SmartScreen may show its normal warning for an unsigned executable.

## Build and test the application

From the repository root:

```powershell
dotnet restore Alpha_Branding.sln -r win-x64
dotnet build Alpha_Branding.sln --configuration Release --no-restore
dotnet test Alpha_Branding.sln --configuration Release --no-build
dotnet format Alpha_Branding.sln --verify-no-changes --no-restore
```

Run the application:

```powershell
dotnet run --project src/Alpha.Branding/Alpha.Branding.csproj
```

Create a framework-dependent publish:

```powershell
dotnet publish src/Alpha.Branding/Alpha.Branding.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false
```

## Build the packaged installer

The installer build works from any current directory when given its script path:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\installer\Build-Installer.ps1 `
  -Version 1.0.0.0
```

Output:

```text
artifacts/Alpha.Branding.Setup.exe
```

The installer build publishes the WPF application self-contained for `win-x64`, builds the native single-file bootstrapper, embeds the application payload, and validates the payload trailer before producing the installer executable.

## Repository layout

```text
Alpha_Branding.sln
src/Alpha.Branding/              Native WPF application
  Assets/                        Local logo and branding frame
  Models/                        Processed image model
  Services/                      Filename and image-processing services
  ViewModels/                    Main application workflow
installer/                       Native C# installer bootstrapper and build script
tests/Alpha.Branding.Tests/      xUnit tests
.github/workflows/               Windows build, test, publish, and installer CI
```

## Original-to-native migration

The original repository contained `index.html`, `styles.css`, `script.js`, and four PNG assets. Its browser workflow was migrated to native WPF rather than wrapped in Electron, Tauri, WebView2, or another browser shell. The original branding assets remain preserved in `img/` and the application copies the required logo and overlay into `src/Alpha.Branding/Assets/`.

The native Apply button is intentional: the original visible Apply control was disabled and unused while processing actually happened on file selection. The refactored application makes that operation explicit while preserving the original image dimensions, overlay behavior, naming intent, and local-only workflow.

## Limitations

- Windows is required because the application uses WPF.
- The installer currently targets `win-x64`.
- The installer is unsigned; SmartScreen may warn until a publisher certificate is used.
- No backend, API credentials, production endpoint, database, or cloud service is required.
