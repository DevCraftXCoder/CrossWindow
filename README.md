# CrossWindow

Move and swap windows across monitors with directional hotkeys. Windows desktop utility.

## Phase 1 — CLI Prototype

**Requires:** [.NET 8 SDK](https://aka.ms/dotnet/download) (Windows)

```powershell
# Build
dotnet build CrossWindow.sln

# Detect monitors + active window
dotnet run --project CrossWindow.CLI -- info

# Move active window right
dotnet run --project CrossWindow.CLI -- move right

# Swap active window with the window on the left monitor
dotnet run --project CrossWindow.CLI -- swap left

# Publish single-file exe
dotnet publish CrossWindow.CLI -c Release -r win-x64 --self-contained false -o dist
# → dist/crosswindow.exe
```

## Directions

| Arg | Direction |
|-----|-----------|
| `left` / `l` | Left monitor |
| `right` / `r` | Right monitor |
| `up` / `u` | Monitor above |
| `down` / `d` | Monitor below |
| `ul` | Upper-left |
| `ur` | Upper-right |
| `dl` | Lower-left |
| `dr` | Lower-right |
| `opposite` / `o` | Farthest monitor |
| `next` / `n` | Cycle next |
| `prev` / `p` | Cycle previous |

## Project Structure

```
CrossWindow/
├── CrossWindow.sln
├── CrossWindow.Core/          # Win32 wrappers + all logic
│   ├── Enums/Direction.cs
│   ├── Models/                # MonitorInfo, WindowInfo, MoveResult, Geometry
│   ├── Win32/NativeMethods.cs # P/Invoke layer
│   └── Services/
│       ├── MonitorManager.cs  # Monitor enumeration + direction algorithm
│       ├── WindowManager.cs   # Active window + SetWindowPos
│       └── MovementEngine.cs  # MoveActiveWindow + SwapActiveWindow
└── CrossWindow.CLI/           # Phase 1 test binary
    └── Program.cs
```

## Swap / Flip-Flop

`crosswindow swap <dir>` finds the foreground window on the adjacent monitor and
swaps both windows: each moves to the other's screen, preserving relative
position and size. If no window is found on the target monitor, it falls back to
a plain move.

## System Tray App (Phase 4)

```powershell
# Run the tray app
dotnet run --project CrossWindow.Tray

# Publish single-file exe
dotnet publish CrossWindow.Tray -c Release -r win-x64 --self-contained false -o dist
# → dist/crosswindow-tray.exe
```

The tray icon lives in the system notification area. Right-click for the context menu:

| Item | Action |
|------|--------|
| **Pause / Resume** | Toggles all hotkeys on/off. Icon turns gray when paused. Double-click the icon also toggles. |
| **Edit Config** | Opens `%APPDATA%\CrossWindow\crosswindow.json` in your default editor |
| **Reload Config** | Re-reads and re-registers hotkeys from disk without restarting |
| **Start with Windows** | Writes/removes the `HKCU\...\Run\CrossWindow` registry key |
| **Exit** | Unregisters all hotkeys and quits |

Config file location: `%APPDATA%\CrossWindow\crosswindow.json` (created on first run with defaults).

## Snap Zones (Phase 5)

Snap the active window to a predefined zone on its current monitor.

```powershell
# Snap via CLI
dotnet run --project CrossWindow.CLI -- snap left
dotnet run --project CrossWindow.CLI -- snap tl
dotnet run --project CrossWindow.CLI -- snap center
```

| Arg | Zone |
|-----|------|
| `left` / `l` | Left half |
| `right` / `r` | Right half |
| `top` / `t` | Top half |
| `bottom` / `b` | Bottom half |
| `tl` | Top-left quarter |
| `tr` | Top-right quarter |
| `bl` | Bottom-left quarter |
| `br` | Bottom-right quarter |
| `max` / `m` | Maximize (full work area) |
| `center` / `c` | Centered 70% |

Default snap hotkeys: **Ctrl+Shift+Win** + arrow (halves), numpad corner (quarters), **M** (maximize), **C** (center).

## Swap Mode (Phase 5)

Toggle swap mode with **Ctrl+Shift+Alt+S**. While active:
- All **Move** hotkeys act as **Swap** instead.
- The tray icon turns **gold** and the tooltip reads "CrossWindow (swap mode)".
- A second press restores normal behavior and the icon returns to pink.

Swap mode resets when the app is paused or config is reloaded.

## Distribution (v1.0)

Build a self-contained executable (no .NET install required on target machine):

```powershell
dotnet publish CrossWindow.Tray -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true -o dist-standalone
```

Ship the contents of `dist-standalone/` — the `.exe` plus the handful of WPF native DLLs alongside it.

On first launch, CrossWindow shows a balloon tip with the key hotkeys. After that it starts silently.

## Roadmap

| Phase | What | Status |
|-------|------|--------|
| 1 ✅ | CLI prototype — monitor detection, move, swap | Done |
| 2 ✅ | Global hotkeys (`RegisterHotKey`) + JSON config | Done |
| 3 ✅ | Diagonal + opposite hotkeys, PerMonitorV2 DPI-aware | Done |
| 4 ✅ | System tray app (WPF + WinForms `NotifyIcon`) | Done |
| 5 ✅ | Snap zones (10 presets), swap mode toggle | Done |
| 6 ✅ | Version metadata, first-run wizard, self-contained publish | Done |

## See Also

Similar and complementary tools in the Windows window-management space:

| Project | What it does |
|---------|-------------|
| [microsoft/PowerToys](https://github.com/microsoft/PowerToys) | Microsoft's power-user toolkit — FancyZones covers snap layouts |
| [LGUG2Z/komorebi](https://github.com/LGUG2Z/komorebi) | Tiling window manager for Windows with full BSP layout engine |
| [glzr-io/glazewm](https://github.com/glzr-io/glazewm) | i3-inspired tiling window manager for Windows |
| [chriskiehl/Gooey](https://github.com/chriskiehl/Gooey) | Turn CLI apps into Windows GUIs — useful pattern for hotkey config UIs |
| [AutoHotkey/AutoHotkey](https://github.com/AutoHotkey/AutoHotkey) | Scripting engine that inspired the hotkey design space CrossWindow lives in |

Also from this author:

| Project | What it does |
|---------|-------------|
| [DevCraftXCoder/SIC](https://github.com/DevCraftXCoder/SIC) | AI-powered penetration testing MCP framework — 150+ security tools |
| [DevCraftXCoder/AttackMap](https://github.com/DevCraftXCoder/AttackMap) | Live attack map visualization |
| [DevCraftXCoder/Admin-Dashboard](https://github.com/DevCraftXCoder/Admin-Dashboard) | Multi-panel admin console with SIC integration |
