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
git clone https://github.com/ItsAlways710/ElBruno.OllamaMonitor.git
cd ElBruno.OllamaMonitor
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
dotnet run --project src/ElBruno.OllamaMonitor/
```

The app will launch. Check the system tray for the icon.

### Run CLI Commands During Development

```bash
# Show help
dotnet run --project src/ElBruno.OllamaMonitor/ -- --help

# Show current config
dotnet run --project src/ElBruno.OllamaMonitor/ -- config

# Set endpoint
dotnet run --project src/ElBruno.OllamaMonitor/ -- config set endpoint http://localhost:11434

# Reset config
dotnet run --project src/ElBruno.OllamaMonitor/ -- config reset
```

## Project Structure

```
src/ElBruno.OllamaMonitor/
├── Cli/
│   ├── CliCommand.cs              # Command model
│   ├── CliCommandKind.cs          # Command types enum
│   ├── CliCommandParser.cs        # Parse command-line args
│   └── CliCommandRunner.cs        # Execute commands
├── Configuration/
│   ├── AppSettings.cs             # Settings model (JSON-serializable)
│   └── AppSettingsService.cs      # Load/save settings file
├── Diagnostics/
│   ├── DiagnosticsLogService.cs   # Event logging
│   ├── StatusFormatter.cs         # Format snapshots as text
│   └── ClipboardService.cs        # Copy to clipboard
├── Helpers/
│   ├── ProcessLauncher.cs         # Launch URLs / processes
│   └── AppPaths.cs                # Config/log paths
├── Interop/
│   └── ...                        # Windows P/Invoke helpers
├── Models/
│   ├── OllamaMonitorState.cs      # State enum (Gray/Green/Blue/Orange/Red)
│   ├── OllamaMonitorSnapshot.cs   # Aggregated status
│   ├── OllamaModelSnapshot.cs     # Model info
│   └── ResourceSnapshot.cs        # CPU/RAM/GPU metrics
├── Ollama/
│   ├── OllamaClient.cs            # HTTP client for Ollama API
│   ├── OllamaStatusService.cs     # Aggregate Ollama state
│   ├── OllamaStatus.cs            # API response models
│   └── OllamaModelInfo.cs         # Model info models
├── Services/
│   ├── ProcessMetricsService.cs   # CPU/RAM metrics
│   ├── NvidiaSmiMetricsService.cs # GPU metrics
│   ├── OllamaCliService.cs        # Local ollama CLI integration (ps/stop/pull/rm/cp/serve)
│   ├── TrayIconService.cs         # System tray lifecycle
│   ├── TrayStatusMapper.cs        # Map state to icon color
│   └── TrayMenuBuilder.cs         # Build context menu
├── ViewModels/
│   └── MainWindowViewModel.cs     # UI state and logic
├── App.xaml / App.xaml.cs         # WPF Application entry
└── MainWindow.xaml / MainWindow.xaml.cs  # Floating details window

tests/ElBruno.OllamaMonitor.Tests/
├── OllamaStatusServiceTests.cs    # Unload strategy + fallback behavior
└── AppSettingsTests.cs            # New defaults (unload strategy, notifications)
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

5. Test: `dotnet run --project src/ElBruno.OllamaMonitor/ -- <your-new-command>`

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
%LOCALAPPDATA%\ElBruno\OllamaMonitor\logs\
```

Use `DiagnosticsLogService` to write logs:

```csharp
_diagnostics.WriteInfo("This is an info message");
_diagnostics.WriteError("An error occurred", exception);
_diagnostics.WriteWarning("A warning");
```

To view logs, open the logs directory with Windows Explorer or your editor.

## Debugging

### In Visual Studio

1. Open the solution: `ElBruno.OllamaMonitor.sln`
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
2. Search for `ElBruno.OllamaMonitor` process
3. Click **Attach**

## Testing Checklist

Before submitting a pull request or release:

- [ ] **Build passes:** `dotnet build`
- [ ] **No warnings:** Check build output
- [ ] **App launches:** `dotnet run --project src/ElBruno.OllamaMonitor/`
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
dotnet test ElBruno.OllamaMonitor.sln
```

Current automated coverage includes:
- unload strategy behavior (`Auto`, `Cli`, remote/local gating)
- CLI-stop/API-fallback behavior
- running-model lookup strategy
- default settings checks for new feature flags

## Common Development Tasks

### Increase Logging Detail

Edit `Services/DiagnosticsLogService.cs` or `App.xaml.cs` to write more info logs during startup.

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
dotnet run --project src/ElBruno.OllamaMonitor/ -- config set endpoint http://192.168.1.100:11434
```

### Test with Different Models

Load a different model in Ollama:
```bash
ollama pull llama2
ollama run llama2
```

The app should update within the refresh interval.

## Packaging for Distribution

### Build Release Version

```bash
dotnet build -c Release
```

### Create NuGet Package

The `.csproj` is configured with `<PackAsTool>true</PackAsTool>`, so you can pack it:

```bash
dotnet pack -c Release
```

This creates a `.nupkg` file in the `bin/Release/` folder.

### Publish to NuGet

(Requires NuGet API key and publishing rights)

```bash
dotnet nuget push bin/Release/ElBruno.OllamaMonitor.0.1.0.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json
```

Once published, users can install via:
```bash
dotnet tool install --global ElBruno.OllamaMonitor
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

Ensure you have the Windows desktop development workload installed:
```bash
dotnet workload install wafxaml
```

Or install Visual Studio with ".NET desktop development" workload.

### TrayIcon doesn't appear

Check:
1. Logs in `%LOCALAPPDATA%\ElBruno\OllamaMonitor\logs\`
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
