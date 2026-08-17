# Alpha Branding Installer

Build the self-contained per-user Windows installer from any directory:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1 -Version 1.0.0.0
```

The exact output is `artifacts/Alpha.Branding.Setup.exe`. It is a native WPF self-extracting bootstrapper (`WinExe`) containing the complete self-contained `win-x64` publish output.

Features:
- Native branded WPF graphical setup wizard (no command prompt window).
- Installs per-user under `%LOCALAPPDATA%\Alpha Premier Realty\Branding Studio`.
- Requires no administrator access, certificates, or MSIX.
- Real-time progress bar, status updates, and post-install application launch checkbox.
- Creates a Start Menu shortcut and Apps & Features uninstall entry in HKCU.
- Graphical uninstaller UI when run with `--uninstall`.
- Unattended/silent installation and uninstallation via `--silent` or `/S`.
