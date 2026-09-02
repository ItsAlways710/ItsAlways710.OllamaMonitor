# Development Guide

## Prerequisites

- **Windows 10 / Windows 11**
- **.NET 10 SDK** — Download from [dotnet.microsoft.com](https://dotnet.microsoft.com)
- **Visual Studio 2022** or **Visual Studio Code** (optional but recommended)
- **Git** for cloning the repository
- **Ollama** running locally (to test the app)

## Repository Setup

### Clone the Repository

```bash
git clone https://github.com/ItsAlways710/ItsAlways710.OllamaMonitor.git
cd ItsAlways710.OllamaMonitor
```

### Restore Dependencies

```bash
dotnet restore
```

### Verify .NET Version

```bash
dotnet --version
```

Should be .NET 10.0.x or later.

## Build and Run

### Build the Project

```bash
dotnet build
```

To build in Release mode:

```bash
dotnet build -c Release
```

### Run the App

```bash
dotnet run --project src/ItsAlways710.OllamaMonitor/
```

The app will launch. Check the system tray for the icon.

### Run CLI Commands During Development

```bash
# Show help
dotnet run --project src/ItsAlways710.OllamaMonitor/ -- --help

# Show current config
dotnet run --project src/ItsAlways710.OllamaMonitor/ -- config

# Set endpoint
dotnet run --project src/ItsAlways710.OllamaMonitor/ -- config set endpoint http://localhost:11434

# Reset config
dotnet run --project src/ItsAlways710.OllamaMonitor/ -- config reset
```

## Project Structure

```
src/ItsAlways710.OllamaMonitor/
├── Cli/
│   ├── CliCommand.cs              # Command model
│   ├── CliCommandKind.cs          # Command kinds enum
│   ├── CliCommandParser.cs        # Parse command-line args
│   └── CliCommandRunner.cs        # Execute commands (help, config, reset)
├── Configuration/
│   ├── AppSettings.cs             # Settings model (JSON-serializable)
│   ├── AppSettingsService.cs      # Load/save settings file
│   ├── ModelUnloadStrategy.cs     # Auto / Cli / Api strategy
│   └── SettingsValidator.cs       # Shared CLI + Settings UI validation
├── Diagnostics/
│   └── DiagnosticsLogService.cs   # Event + verbose logging
├── Helpers/
│   ├── BoolToColorConverter.cs    # WPF value converter
│   ├── ProcessLauncher.cs         # Launch URLs / processes
│   ├── SnapshotFormatter.cs       # Format snapshots as text
│   └── StatusTextHelper.cs        # Status line / tooltip formatting
├── Interop/
│   ├── ConsoleManager.cs          # Console attach for tool output
│   ├── NativeMethods.cs           # P/Invoke declarations
│   ├── RefWindowForensics.cs      # Topmost-guard forensics capture
│   ├── TopmostGuardPolicy.cs      # WndProc demotion policy
│   └── WindowInterop.cs           # Window message hooks
├── Models/
│   ├── ContextWindowSample.cs     # Per-task context sample
│   ├── MiniContextLine.cs         # Fitted mini monitor context line
│   ├── NotificationEventType.cs   # Notification event flags
│   ├── OllamaMonitorState.cs      # State enum (NotReachable → Error)
│   ├── OllamaMonitorSnapshot.cs   # Aggregated status
│   ├── OllamaModelSnapshot.cs     # Model info
│   └── ResourceSnapshot.cs        # CPU/RAM/GPU metrics
├── Ollama/
│   ├── OllamaApiCallResult.cs     # Typed result wrapper
│   ├── OllamaClient.cs            # HTTP client for Ollama API
│   ├── OllamaModelStore.cs        # Model identity tracking
│   └── Contracts/                 # API response models
├── Services/
│   ├── AutoLaunchService.cs       # Launch-at-sign-in Run-key registration
│   ├── ContextTrackingService.cs  # Live context tracking + attribution
│   ├── GpuUsageGraph.cs           # Mini monitor GPU sparkline
│   ├── IOllamaCliService.cs       # CLI abstraction
│   ├── IOllamaLogService.cs       # Log tailing abstraction
│   ├── NvidiaSmiMetricsService.cs # GPU metrics
│   ├── OllamaCliService.cs        # Local ollama CLI (ps/stop/pull/rm/serve)
│   ├── OllamaLogService.cs        # Log source: redirect or file tail
│   ├── OllamaStatusService.cs     # Aggregate Ollama state
│   ├── OsMetricsService.cs        # System-wide CPU/memory (kernel32)
│   ├── ProcessMetricsService.cs   # Per-process CPU/RAM/disk I/O
│   ├── ThemeService.cs            # Dark/light/system themes
│   ├── TrayIconService.cs         # System tray lifecycle and menu
│   └── WindowsNotificationService.cs # Toast notifications
├── ViewModels/
│   ├── AsyncRelayCommand.cs / RelayCommand.cs / ViewModelBase.cs
│   ├── MainWindowViewModel.cs     # Shared UI state, refresh loop
│   └── SettingsWindowViewModel.cs # Settings dialog state
├── AppPaths.cs                    # Config/log paths
├── App.xaml / App.xaml.cs         # WPF Application entry
├── MainWindow.xaml / .cs          # Details window
├── MiniMonitorWindow.xaml / .cs   # Compact always-on-top monitor
├── SettingsWindow.xaml / .cs      # Settings dialog
├── Resources/                     # Theme resource dictionaries
└── Assets/TrayIcons/              # State icons (gray/green/blue/orange/red)

tests/ItsAlways710.OllamaMonitor.Tests/   (141 tests)
├── AppSettingsTests.cs                        # Defaults + deserialization
├── AutoLaunchServiceTests.cs                  # Run-key registration
├── CompactGpuSummaryTests.cs                  # GPU text fitting
├── ContextTrackingServiceTests.cs             # Token/slot parsing
├── ContextTrackingServiceAttributionTests.cs  # Runner→model attribution
├── DiagnosticsLogServiceTests.cs              # Verbose gate contract
├── MiniMonitorPositionTests.cs                # Position persistence
├── OllamaLogServiceTests.cs                   # Hybrid log source
├── OllamaModelStoreTests.cs                   # Model identity tracking
├── OllamaStatusServiceTests.cs                # Unload strategy + fallback
├── OsMetricsServiceTests.cs                   # System-wide metrics
├── RealStoreAttributionTests.cs               # Attribution against a real log line
├── RefWindowForensicsTests.cs                 # Identity capture
├── StatusTextHelperTests.cs                   # Status line formatting
└── TopmostGuardPolicyTests.cs                 # Demotion policy
```

## Key Development Areas

### Adding a New CLI Command

1. Add a new variant to `CliCommandKind` enum in `Cli/CliCommandKind.cs`:
   ```csharp
   public enum CliCommandKind
   {
       // ... existing
       MyNewCommand
   }
   ```

2. Update `Cli/CliCommandParser.cs` to recognize the new command in the parser logic

3. Handle it in `Cli/CliCommandRunner.cs`:
   ```csharp
   if (command.Kind == CliCommandKind.MyNewCommand)
   {
       // Your logic here
       return 0; // success
   }
   ```

4. Update help text in `HelpCommand.cs` if needed

5. Test: `dotnet run --project src/ItsAlways710.OllamaMonitor/ -- <your-new-command>`

### Modifying Metrics Collection

To add or change what metrics are collected:

1. Update `Models/ResourceSnapshot.cs` to add new fields
2. Modify `Services/ProcessMetricsService.cs` or create a new service
3. Call the new service from `Ollama/OllamaStatusService.cs` in the `GetStatusAsync` method
4. Update `MainWindowViewModel.cs` to display the new metric if needed

### Changing Tray Icon Behavior

Tray icon logic lives in these files:

- `Services/TrayStatusMapper.cs` — Maps `OllamaMonitorState` to colors/icons
- `Services/TrayIconService.cs` — Manages lifecycle and menu
- `Services/TrayMenuBuilder.cs` — Constructs context menu items

To change icon colors or add menu items, modify these files.

### Updating Configuration Settings

1. Add a new field to `Configuration/AppSettings.cs`:
   ```csharp
   public int MyNewSetting { get; init; } = 123;
   ```

2. Update the default in `Configuration/AppSettingsService.cs` if needed

3. Add CLI command to set it (see "Adding a New CLI Command" above)

4. Use it in your service code via the injected `AppSettings`

## Logging and Diagnostics

Logs are written to:

```
%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\logs\
```

Use `DiagnosticsLogService` to write logs:

```csharp
_diagnostics.WriteInfo("This is an info message");
_diagnostics.WriteError("An error occurred", exception);
_diagnostics.WriteWarning("A warning");
```

Diagnostic-level logging is gated behind the `enableVerboseLogging` setting (off by
default; the app applies it to `DiagnosticsLogService.IsVerboseEnabled` at startup
and on every refresh tick, so the Settings toggle works live):

```csharp
_diagnostics.WriteVerbose("detailed state useful only while investigating");
```

- `WriteVerbose` is a complete no-op (no file I/O) while the flag is off
- When the detail requires expensive capture (process lookups, foreign window identity),
  check `IsVerboseEnabled` **before** the capture so the work itself is skipped -
  see `MiniMonitorWindow.LogTopmostEvent`

To view logs, open the logs directory with Windows Explorer or your editor.

## Debugging

### In Visual Studio

1. Open the solution: `ItsAlways710.OllamaMonitor.sln`
2. Set breakpoints in your code
3. Press **F5** to debug
4. The app will launch; breakpoints will be hit

### In Visual Studio Code

1. Install the C# extension
2. Open the project folder
3. Press **F5** to debug (or select "Run and Debug")
4. Select ".NET 10" runtime

### Attach to Running Process

If the app is already running:

1. In Visual Studio: **Debug → Attach to Process**
2. Search for `ItsAlways710.OllamaMonitor` process
3. Click **Attach**

## Testing Checklist

Before submitting a pull request or release:

- [ ] **Build passes:** `dotnet build`
- [ ] **No warnings:** Check build output
- [ ] **App launches:** `dotnet run --project src/ItsAlways710.OllamaMonitor/`
- [ ] **Tray icon appears** and updates every 2 seconds
- [ ] **Floating window shows** real-time data (click tray icon)
- [ ] **CLI help works:** `dotnet run ... -- --help`
- [ ] **Config commands work:** `dotnet run ... -- config`
- [ ] **Settings persist** after restart
- [ ] **Handles offline Ollama gracefully** (gray tray, "Not Reachable" message)
- [ ] **GPU metrics** appear if nvidia-smi available, or show "N/A" otherwise
- [ ] **Context menu** has Copy, Open URL, Refresh, Exit

## Automated Tests

Run all tests:

```bash
dotnet test ItsAlways710.OllamaMonitor.sln
```

Current automated coverage includes (141 tests):
- Context tracking: token/slot parsing, runner→model attribution (incl. a real Ollama log line)
- Ollama model store and running-model lookup strategy
- Unload strategy behavior (`Auto`, `Cli`, remote/local gating) + stop validation
- Ollama log service (hybrid source selection, log tailing)
- Diagnostics logging (verbose gate contract)
- Auto-launch (Run-key registration)
- Mini monitor position persistence
- System-wide metrics (OsMetrics)
- Topmost guard policy and reference window forensics
- Status line and snapshot formatting
- Settings defaults and deserialization

## Common Development Tasks

### Increase Logging Detail

Diagnostic-only detail: use `_diagnostics.WriteVerbose(...)` - it is gated on the
`enableVerboseLogging` setting (off by default) and the Settings toggle applies live.

Always-on logs: add `WriteInfo`/`WriteWarning` calls where the event itself is always
worth recording (e.g. during startup in `App.xaml.cs`).

### Test with Offline Ollama

Stop Ollama:
```bash
# On Windows, if running as service:
sc stop ollama
# Or if running in terminal, press Ctrl+C
```

Run the app—it should show gray tray icon and "Not Reachable" status.

### Test with Remote Ollama

Set endpoint to a different machine:
```bash
dotnet run --project src/ItsAlways710.OllamaMonitor/ -- config set endpoint http://192.168.1.100:11434
```

### Test with Different Models

Load a different model in Ollama:
```bash
ollama pull llama2
ollama run llama2
```

The app should update within the refresh interval.

## Packaging for Distribution

### Create NuGet Package

Use the publish script; it packs the tool, builds the desktop payload, and injects it
into the package:

```powershell
pwsh .\build\Pack-Tool.ps1 -Configuration Release
```

The package lands in `artifacts\packages\` and the desktop payload in
`artifacts\desktop-publish\`.

### Publish to NuGet

(Requires a NuGet API key and publishing rights — see [Publishing Guide](publishing.md))

```bash
dotnet nuget push artifacts/packages/ItsAlways710.OllamaMonitor.0.13.0.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json
```

Once published, users can install via:
```bash
dotnet tool install --global ItsAlways710.OllamaMonitor
```

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Make your changes
4. Test thoroughly (see Testing Checklist)
5. Commit with a clear message
6. Push and create a pull request

## Architecture Deep Dive

For a detailed understanding of how the app is structured, see [Architecture Guide](architecture.md).

## Troubleshooting Development Issues

### Build fails with "System.Windows" errors

Ensure the .NET 10 SDK with the **.NET desktop development** workload is installed
(`global.json` pins 10.0.203 with `rollForward: latestFeature`):

- Visual Studio 2026, or VS 2022 with the ".NET desktop development" workload added
- Or: `dotnet --list-sdks` should show a 10.0.x SDK

### TrayIcon doesn't appear

Check:
1. Logs in `%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\logs\`
2. Windows → Settings → Taskbar → Taskbar items → Ensure your app isn't hidden
3. Try clicking the "Show hidden icons" arrow in the tray

### HTTP client timeout

If you see "Request timeout" messages, check:
1. Is Ollama running? `ollama serve` in a terminal
2. Is the endpoint correct? `ollamamon config`
3. Is there a firewall issue?

---

**Next Steps:**
- [Architecture Guide](architecture.md) — Technical details
- [Configuration Guide](configuration.md) — User settings
- [Release Notes](release-notes.md) — Version history
