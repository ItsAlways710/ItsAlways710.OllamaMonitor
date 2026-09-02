# Architecture Guide

## Overview

ItsAlways710.OllamaMonitor is a .NET WPF desktop application built for Windows with a system tray interface, a standard details window, and a compact always-on-top mini monitor.

The architecture is split into two tracks:

- **Platform Layer (Tank)** — HTTP client, Ollama API integration, process metrics, CLI commands, configuration
- **Desktop Layer (Trinity)** — WPF UI, system tray integration, visual state management, window lifecycle

## Project Structure

```
src/ItsAlways710.OllamaMonitor/
├── Cli/
│   ├── CliCommand.cs              # Command model
│   ├── CliCommandKind.cs          # Command type enum
│   ├── CliCommandParser.cs        # Parse args -> CliCommand
│   └── CliCommandRunner.cs        # Execute commands (help, config, reset, etc.)
├── Configuration/
│   ├── AppSettings.cs             # Settings data model (JSON-serializable)
│   └── AppSettingsService.cs      # Load/save settings from disk
├── Diagnostics/
│   ├── StatusFormatter.cs         # Format snapshots as readable text
│   ├── ClipboardService.cs        # Copy diagnostics to clipboard
│   └── DiagnosticsLogService.cs   # Event logging
├── Helpers/
│   ├── ProcessLauncher.cs         # Launch URLs / external commands
│   └── ...
├── Interop/
│   └── ...                        # P/Invoke / Windows interop
├── Models/
│   ├── OllamaMonitorState.cs      # State enum (Gray, Green, Blue, Orange, Red)
│   ├── OllamaMonitorSnapshot.cs   # Aggregated status snapshot
│   ├── OllamaModelSnapshot.cs     # Model info
│   └── ResourceSnapshot.cs        # CPU, RAM, GPU metrics
├── Ollama/
│   ├── OllamaClient.cs            # HTTP client for Ollama API
│   ├── OllamaStatusService.cs     # Aggregate Ollama state
│   ├── OllamaStatus.cs            # API response models
│   └── OllamaModelInfo.cs         # Model details
├── Services/
│   ├── ProcessMetricsService.cs   # CPU, RAM from Ollama process
│   ├── NvidiaSmiMetricsService.cs # GPU metrics via nvidia-smi
│   ├── OllamaCliService.cs        # Local ollama CLI integration (ps/stop/pull/rm/cp/serve)
│   ├── TrayIconService.cs         # System tray lifecycle
│   ├── TrayStatusMapper.cs        # OllamaMonitorState -> icon color
│   └── TrayMenuBuilder.cs         # Context menu construction
├── ViewModels/
│   └── MainWindowViewModel.cs     # UI state, refresh logic
├── App.xaml / App.xaml.cs         # WPF Application entry point
├── MainWindow.xaml / MainWindow.xaml.cs      # Standard details window
└── MiniMonitorWindow.xaml / .cs              # Compact always-on-top monitor
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
      └─ OllamaStatusService.GetStatusAsync()
          ├─ OllamaClient.GetVersionAsync()
          ├─ OllamaClient.GetTagsAsync()
          ├─ OllamaClient.GetProcessesAsync()
          ├─ ProcessMetricsService.GetMetricsAsync()
          └─ NvidiaSmiMetricsService.GetGpuMetricsAsync()
      └─ Determine OllamaMonitorState (Gray/Green/Blue/Orange/Red)
      └─ Update UI bindings
      └─ Update tray icon color (TrayStatusMapper)
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

- Direct file edit
- CLI: `ollamamon config set <key> <value>`
- Settings dialog (Phase 2)

Key settings:

| Key | Type | Default | Purpose |
|-----|------|---------|---------|
| `endpoint` | string | `http://localhost:11434` | Ollama API endpoint |
| `refreshIntervalSeconds` | int | `2` | Polling interval |
| `launchAtWindowsStartup` | bool (UI) | Off | Launch at sign-in via HKCU Run key (Settings → General; not stored in settings.json) |
| `showFloatingWindowOnStart` | bool | `false` | Show the mini monitor on startup |
| `enableGpuMetrics` | bool | `true` | Include GPU metrics |
| `enableDiskMetrics` | bool | `true` | Include disk I/O metrics |
| `highCpuThresholdPercent` | double | `80` | CPU% to trigger HighUsage state |
| `highMemoryThresholdGb` | double | `16` | RAM GB to trigger HighUsage state |
| `highGpuThresholdPercent` | double | `85` | GPU% to trigger HighUsage state |

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

### TrayIconService

Manages system tray lifecycle, context menu, and state-driven icon updates.

- Shows/hides the details window and mini monitor
- Provides "Copy diagnostics", "Open Ollama URL", "Exit" menu items
- Updates icon color based on `OllamaMonitorState`

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

## Testing Checklist (Phase 1)

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

The solution now includes `tests/ItsAlways710.OllamaMonitor.Tests` with coverage for:
- unload strategy behavior (`Auto`, `Cli`, remote/local gating)
- CLI stop + API fallback behavior
- running model lookup strategy
- new settings defaults for feature flags

Run tests with:

```bash
dotnet test ItsAlways710.OllamaMonitor.sln
```

---

**Next Phase (Phase 2):** additional UI-level tests and historical charts.
