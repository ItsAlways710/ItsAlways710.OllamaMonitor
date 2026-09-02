# Architecture Guide

## Overview

ItsAlways710.OllamaMonitor is a .NET WPF desktop application built for Windows with a system tray interface, a standard details window, a compact always-on-top mini monitor, and a settings dialog.

The architecture is split into two layers:

- **Core** — HTTP client and Ollama API integration, per-process and system-wide metrics, live context tracking, server-log tailing, notifications, CLI, and configuration
- **UI layer** — WPF windows (details, mini monitor, settings), system tray integration, theming, and visual state management

## Project Structure

```
src/ItsAlways710.OllamaMonitor/
├── Cli/
│   ├── CliCommand.cs                # Command model
│   ├── CliCommandKind.cs            # Command type enum
│   ├── CliCommandParser.cs          # Parse args → CliCommand
│   └── CliCommandRunner.cs          # Execute commands (help, config, reset)
├── Configuration/
│   ├── AppSettings.cs               # Settings data model (JSON-serializable)
│   ├── AppSettingsService.cs        # Load/save settings from disk
│   ├── ModelUnloadStrategy.cs       # Auto / Cli / Api unload strategy
│   └── SettingsValidator.cs         # Shared validation for CLI and Settings UI
├── Diagnostics/
│   └── DiagnosticsLogService.cs     # Event logging + gated [VERBOSE] logging
├── Helpers/
│   ├── BoolToColorConverter.cs      # WPF value converter
│   ├── ProcessLauncher.cs           # Launch URLs / external processes
│   ├── SnapshotFormatter.cs         # Format snapshots as copyable text
│   └── StatusTextHelper.cs          # Status line / tray tooltip formatting
├── Interop/
│   ├── ConsoleManager.cs            # Console attach for PackAsTool output
│   ├── NativeMethods.cs             # P/Invoke declarations
│   ├── RefWindowForensics.cs        # Topmost-guard forensic identity capture
│   ├── TopmostGuardPolicy.cs        # WndProc hook policy for the topmost guard
│   └── WindowInterop.cs             # Window message hooks
├── Models/
│   ├── ContextWindowSample.cs       # Per-task context-window sample
│   ├── MiniContextLine.cs           # Context line fitted to mini monitor width
│   ├── NotificationEventType.cs     # Notification event flags
│   ├── OllamaMonitorSnapshot.cs     # Aggregated status snapshot
│   ├── OllamaModelSnapshot.cs       # Model info
│   ├── OllamaMonitorState.cs        # State enum (NotReachable → Error)
│   └── ResourceSnapshot.cs          # CPU, RAM, GPU metrics
├── Ollama/
│   ├── OllamaApiCallResult.cs       # Typed result wrapper
│   ├── OllamaClient.cs              # HTTP client for the Ollama API
│   ├── OllamaModelStore.cs          # Model identity tracking
│   └── Contracts/                   # API response models (version, tags, ps, …)
├── Resources/
│   ├── ThemesDark.xaml              # Dark theme resource dictionary
│   └── ThemesLight.xaml             # Light theme resource dictionary
├── Services/
│   ├── AutoLaunchService.cs         # Launch-at-sign-in Run-key registration
│   ├── ContextTrackingService.cs    # Live context-window tracking + attribution
│   ├── GpuUsageGraph.cs             # Mini monitor GPU sparkline
│   ├── IOllamaCliService.cs         # Local ollama CLI abstraction
│   ├── IOllamaLogService.cs         # Server-log tailing abstraction
│   ├── NvidiaSmiMetricsService.cs   # GPU metrics via nvidia-smi
│   ├── OllamaCliService.cs          # ps/stop/pull/rm/serve via the ollama CLI
│   ├── OllamaLogService.cs          # Log source: process redirect or file tail
│   ├── OllamaStatusService.cs       # Aggregate Ollama state
│   ├── OsMetricsService.cs          # System-wide CPU/memory via kernel32
│   ├── ProcessMetricsService.cs     # Per-process CPU/RAM/disk I/O
│   ├── ThemeService.cs              # Dark/light/system theme resolution
│   ├── TrayIconService.cs           # System tray lifecycle and menu
│   └── WindowsNotificationService.cs # Windows toast notifications
├── ViewModels/
│   ├── AsyncRelayCommand.cs / RelayCommand.cs / ViewModelBase.cs
│   ├── MainWindowViewModel.cs       # Shared UI state, refresh loop, model actions
│   └── SettingsWindowViewModel.cs   # Settings dialog state
├── AppPaths.cs                      # Settings/log path constants
├── App.xaml / App.xaml.cs           # WPF Application entry point
├── MainWindow.xaml / MainWindow.xaml.cs      # Details window
├── MiniMonitorWindow.xaml / MiniMonitorWindow.xaml.cs  # Compact always-on-top monitor
├── SettingsWindow.xaml / SettingsWindow.xaml.cs        # Settings dialog
└── Assets/TrayIcons/                # State icon assets (gray/green/blue/orange/red)
```

## Command Flow

### Application Startup

```
App.OnStartup()
  ├─ Parse CLI args (CliCommandParser)
  ├─ If args = ["--help", "config", "config set", etc.]
  │   └─ Run CLI command (CliCommandRunner) → exit
  └─ Else
      └─ LaunchTrayApplication()
```

### Tray Application Bootstrap

```
LaunchTrayApplication()
  ├─ Load settings (AppSettingsService)
  ├─ Create HttpClient (singleton)
  ├─ Create service stack:
  │   ├─ OllamaClient
  │   ├─ ProcessMetricsService
  │   ├─ NvidiaSmiMetricsService
  │   └─ OllamaStatusService (aggregates all three)
  ├─ Create MainWindow, MiniMonitorWindow, and MainWindowViewModel
  ├─ Create TrayIconService
  ├─ Start DispatcherTimer (refresh loop)
  └─ Show window or minimize to tray (based on settings)
```

### Refresh Loop

Every **N seconds** (default 2, configurable):

```
DispatcherTimer.Tick
  └─ MainWindowViewModel.RefreshAsync()
      ├─ Reload settings from disk (changes apply live — no restart)
      ├─ OllamaStatusService.GetStatusAsync()
      │   ├─ OllamaClient.GetVersionAsync()
      │   ├─ OllamaClient.GetRunningModelsAsync()
      │   ├─ OllamaClient.GetTagsAsync()
      │   ├─ ProcessMetricsService.GetMetricsAsync()    (per llama-server, summed)
      │   ├─ NvidiaSmiMetricsService.GetGpuMetricsAsync()
      │   └─ OsMetricsService.GetMetricsAsync()         (system-wide CPU/memory)
      ├─ Context tracking state (from server-log tail via OllamaLogService)
      ├─ Determine OllamaMonitorState (Gray/Green/Blue/Orange/Red)
      ├─ Raise notification events (state changes, model load/unload, thresholds)
      ├─ Update UI bindings
      └─ Update tray icon (TrayIconService)
```

## State Model

### OllamaMonitorState

Determines the tray icon color:

```csharp
public enum OllamaMonitorState
{
    NotReachable,    // Gray   — API unreachable
    Running,         // Green  — API reachable, no model
    ModelLoaded,     // Blue   — Model loaded, low usage
    HighUsage,       // Orange — Model running, high usage
    Error            // Red    — Unexpected error
}
```

### State Determination Logic

1. **Can we reach the Ollama API?**
   - No → `NotReachable`
   
2. **Is a model loaded or running?**
   - No → `Running`
   - Yes, CPU/GPU low → `ModelLoaded`
   - Yes, CPU/GPU > threshold → `HighUsage`

3. **Any errors?**
   - Yes → `Error` (overrides other states)

## Configuration

Settings are stored as JSON at:

```
%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\settings.json
```

Editable via:

- Direct file edit (a running app re-reads the file on every refresh cycle)
- CLI: `ollamamon config set endpoint <url>` and `ollamamon config set refresh-interval <seconds>`
- Settings window (tray menu → **Settings…**): notifications, general toggles, and mini monitor display

All settings:

| Key | Type | Default | Purpose |
|-----|------|---------|---------|
| `endpoint` | string | `http://localhost:11434` | Ollama API endpoint |
| `unloadStrategy` | enum | `0` (Auto) | Model unload strategy: Auto / Cli / Api |
| `refreshIntervalSeconds` | int | `2` | Polling interval |
| `showFloatingWindowOnStart` | bool | `false` | Show the mini monitor on startup |
| `enableGpuMetrics` | bool | `true` | Include GPU metrics |
| `enableDiskMetrics` | bool | `true` | Include disk I/O metrics |
| `highCpuThresholdPercent` | double | `80` | CPU% to trigger HighUsage state |
| `highMemoryThresholdGb` | double | `16` | RAM GB to trigger HighUsage state |
| `highGpuThresholdPercent` | double | `85` | GPU% to trigger HighUsage state |
| `enableVerboseLogging` | bool | `false` | Gate detailed `[VERBOSE]` diagnostic log lines |
| `enableNotifications` | bool | `true` | Master toggle for Windows notifications |
| `notificationEvents` | flags | `271` | Which events notify |
| `notificationDebounceSeconds` | int | `30` | Minimum interval between repeat notifications |
| `showCpuInMiniMonitor` | bool | `false` | Show CPU in the mini monitor |
| `showMemoryInMiniMonitor` | bool | `false` | Show memory in the mini monitor |
| `showContextInMiniMonitor` | bool | `false` | Show the live context-usage line |
| `showOllamaLogsInMiniMonitor` | bool | `false` | Show the collapsible Ollama logs panel |
| `miniMonitorLeft` / `miniMonitorTop` | double? | (omitted) | Last saved mini monitor position |

`launchAtWindowsStartup` is separate: the HKCU Run-key entry itself is the setting
(Settings → General), so it never appears in `settings.json`.

## Key Classes

### OllamaStatusService

Aggregates Ollama API state, process metrics, and GPU metrics into a single `OllamaMonitorSnapshot`.

```csharp
Task<OllamaMonitorSnapshot> GetStatusAsync(CancellationToken cancellationToken)
```

Returns:
- API version and reachability
- Loaded models and details
- Running processes
- Resource metrics
- State determination

### ProcessMetricsService

Polls Ollama's processes for CPU and memory usage using `System.Diagnostics.Process`.

Process selection: one `llama-server.exe` runs per loaded model, and that is where inference
CPU/RAM actually live, so when one or more are running their CPU%, memory, and disk I/O are
**summed** across all of them (aggregate CPU% may exceed 100 on multi-core machines). The
displayed process label stays `ollama`. When none are running (no models loaded), it falls
back to the `ollama.exe` wrapper process(es), picking the longest-running one (warning on
multiples) so the idle state still reports something.

```csharp
Task<ProcessMetricsResult> GetMetricsAsync(bool enableDiskMetrics, CancellationToken cancellationToken)
```

Returns CPU%, RAM (bytes), private memory, and disk I/O if enabled.

### NvidiaSmiMetricsService

Best-effort GPU metrics via `nvidia-smi` CLI tool.

```csharp
Task<GpuMetrics?> GetGpuMetricsAsync(CancellationToken cancellationToken)
```

Returns GPU utilization%, VRAM used/total if available. Fails gracefully if nvidia-smi not found.

### OsMetricsService

Polls whole-machine (OS-level) usage via native `kernel32` calls — the "(System)" half of the CPU/Memory lines.

```csharp
Task<OsMetricsResult> GetMetricsAsync(CancellationToken cancellationToken)
```

- CPU% is a two-sample `GetSystemTimes` delta (first sample returns null, like `ProcessMetricsService`)
- Memory% is a `GlobalMemoryStatusEx` read: (total − available) / total RAM

### AutoLaunchService

Registers/removes the app at Windows sign-in via the per-user Run key
(`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `OllamaMonitor`).

- The registry value is the single source of truth for the "Launch at Windows Startup" setting (not stored in settings.json)
- `IsEnabled()` compares the stored command against the current executable path, so a stale entry reads as off
- Apply/remove logic is isolated behind `IStartupRegistryStore`, which `RunKeyRegistryStore` implements

### ContextTrackingService

Tracks live context-window usage per concurrent task from Ollama server log lines,
fed via `OllamaLogService.LogLineReceived`. Parses the
`n_ctx_slot` / `task.n_tokens` / `tg` slot lines (per-task tokens used, slot size,
tokens/second) and the `starting llama-server` model-load line for runner-to-model
attribution (model digest + runner port).

### OllamaLogService

Provides Ollama server log text from a hybrid source:
- App launched Ollama → captures the `ollama serve` stdout/stderr redirection
- Ollama was already running → tails the newest of the known log files
  (`%USERPROFILE%\.ollama\logs\server.log`, `%LOCALAPPDATA%\Ollama\server.log`)

Feeds the mini monitor logs panel and `ContextTrackingService`.

### ThemeService

Resolves the active theme (Dark / Light / System) and applies the matching resource
dictionary (`Resources/ThemesDark.xaml` / `ThemesLight.xaml`).

### WindowsNotificationService

Sends Windows toast notifications for the configured `NotificationEventType` events,
with per-type debouncing.

### TrayIconService

Manages system tray lifecycle, context menu, and state-driven icon updates.

- Menu: Show Details, Show Mini Monitor, Settings…, Refresh, Copy Status, Open Ollama API, Open Config Folder, Visit HomePage, Exit
- Double-click on the tray icon opens the mini monitor
- Updates the tray icon based on `OllamaMonitorState` (Assets/TrayIcons, `SystemIcons` fallback)

### MainWindowViewModel

Binds UI to `OllamaMonitorSnapshot`. Handles:

- Status display formatting
- Button clicks (refresh, copy, open URL)
- Window show/hide
- Data binding for the details window and mini monitor

## CLI Commands

All commands are parsed and executed by `CliCommandRunner`:

| Command | Effect |
|---------|--------|
| `ollamamon` | Launch tray app |
| `ollamamon --help` | Show help text |
| `ollamamon config` | Print current settings |
| `ollamamon config set endpoint <url>` | Change Ollama endpoint |
| `ollamamon config set refresh-interval <seconds>` | Change polling interval |
| `ollamamon config reset` | Reset to defaults |

## Error Handling

- **Ollama API unreachable** → Graceful fallback, `NotReachable` state, no app crash
- **Process metrics unavailable** → Show "N/A", continue monitoring
- **GPU metrics unavailable** → Skip GPU data, log warning, continue
- **Settings file corrupted** → Load defaults, attempt recovery
- **Unhandled exceptions** → Logged to diagnostics file, app continues

## Deployment

The project is packaged as a **.NET global tool**:

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>ollamamon</ToolCommandName>
```

Install via:

```bash
dotnet tool install --global ItsAlways710.OllamaMonitor
```

This places the executable in the user's PATH and creates the `ollamamon` command.

## Manual Verification Checklist

- [ ] Build succeeds with `dotnet build`
- [ ] App launches to tray
- [ ] Tray icon appears and updates
- [ ] Details window shows real-time data
- [ ] Mini monitor stays on top and can be dragged/closed
- [ ] `ollamamon config` works
- [ ] `ollamamon --help` works
- [ ] Settings persist across restarts
- [ ] GPU metrics appear if nvidia-smi available
- [ ] App handles Ollama offline gracefully
- [ ] Context menu has Copy, Open URL, Exit

---

## Automated Tests

The solution includes `tests/ItsAlways710.OllamaMonitor.Tests` (141 tests) covering:
- Context tracking: token parsing, slot/task attribution, model attribution from real log lines
- Ollama model store and running-model lookup strategy
- Unload strategy behavior (`Auto`, `Cli`, remote/local gating) + stop validation
- Ollama log service (hybrid source selection, log tailing)
- Diagnostics logging (verbose gate contract, INFO/WARN always written)
- Auto-launch (Run-key registration)
- Mini monitor position persistence
- System-wide metrics (OsMetrics)
- Topmost guard policy and reference window forensics
- Status line and snapshot formatting
- Settings defaults and deserialization

Run tests with:

```bash
dotnet test ItsAlways710.OllamaMonitor.sln
```

