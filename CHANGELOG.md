# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] - 2026-05-08

### Added

- Transparent desktop sticky note window (dark theme, rounded corners, semi-transparent)
- Daily reset: items automatically reset to unchecked at midnight
- Edit items: add, delete, rename, reorder
- Tray icon: double-click to show window, right-click menu
- Auto-start on boot: toggle via tray menu
- Window position memory with screen boundary check
- State persistence: `%APPDATA%\Rhythm\state.json`
- Self-contained single-file executable (no .NET runtime required)

### Technical

- WPF with .NET 10 (`net10.0-windows`)
- State machine layer (pure C#, testable)
- Atomic file writes via temp+rename
- Win32 interop: `HWND_BOTTOM`, `WS_EX_TOOLWINDOW`, `WM_WINDOWPOSCHANGING`
- xUnit tests with coverlet coverage