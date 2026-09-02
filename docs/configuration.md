# Configuration Guide

## Overview

ItsAlways710.OllamaMonitor stores configuration in a JSON file that you can edit directly or modify via CLI commands.

## Configuration File Location

```
%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\settings.json
```

On Windows:
- `%LOCALAPPDATA%` typically expands to `C:\Users\<YourUsername>\AppData\Local`
- So the full path is usually: `C:\Users\<YourUsername>\AppData\Local\ItsAlways710\OllamaMonitor\settings.json`

The file is created automatically with default values on first run.

## Default Configuration

```json
{
  "endpoint": "http://localhost:11434",
  "unloadStrategy": 0,
  "refreshIntervalSeconds": 2,
  "showFloatingWindowOnStart": false,
  "enableGpuMetrics": true,
  "enableDiskMetrics": true,
  "highCpuThresholdPercent": 80,
  "highMemoryThresholdGb": 16,
  "highGpuThresholdPercent": 85,
  "enableVerboseLogging": false,
  "enableNotifications": true,
  "notificationEvents": 271,
  "notificationDebounceSeconds": 30,
  "showCpuInMiniMonitor": false,
  "showMemoryInMiniMonitor": false,
  "showContextInMiniMonitor": false,
  "showOllamaLogsInMiniMonitor": false
}
```

## Configuration Options

### `endpoint`

**Type:** `string`  
**Default:** `http://localhost:11434`

The HTTP endpoint where Ollama is running. Modify this if:
- Ollama is running on a different port
- Ollama is running on a different machine (e.g., VM, remote server)

**Examples:**
```json
"endpoint": "http://localhost:11434"      // Local default
"endpoint": "http://192.168.1.100:11434"  // Remote machine
"endpoint": "http://ollama.local:11434"   // DNS name
```

### `refreshIntervalSeconds`

**Type:** `int` (1 or greater)  
**Default:** `2`

How often (in seconds) the app polls the Ollama API and system metrics.

- Lower values (1–2s): More responsive, slightly more CPU
- Higher values (5–10s): Less responsive, lighter load

**Examples:**
```json
"refreshIntervalSeconds": 1   // Frequent polling
"refreshIntervalSeconds": 5   // Moderate polling
"refreshIntervalSeconds": 10  // Low frequency
```

### `unloadStrategy`

**Type:** `enum` (`0=Auto`, `1=Cli`, `2=Api`)  
**Default:** `0` (`Auto`)

Controls how the monitor unloads/stops running models:

- `Auto`: Uses CLI `ollama stop` for local endpoints; falls back to API unload for remote endpoints.
- `Cli`: Always uses `ollama stop` (local endpoints only).
- `Api`: Always uses API unload (`/api/generate` with `keep_alive=0`).

### `launchAtWindowsStartup`

**Where:** Settings → General ("Launch at Windows Startup" toggle)
**Default:** Off

Registers Ollama Monitor under the per-user Windows Run key
(`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `OllamaMonitor`)
so it starts automatically at Windows sign-in.

Unlike the settings above, this one is **not stored in `settings.json`** —
the registry entry itself is the setting:

- Enabling writes the entry; disabling deletes it
- The checkbox reflects the registry's actual state whenever Settings
  opens, so removing the entry externally (e.g. Task Manager → Startup apps)
  is picked up the next time you open Settings
- At sign-in the app starts in the system tray; the floating mini monitor
  appears alongside only when `showFloatingWindowOnStart` is enabled

### `showFloatingWindowOnStart`

**Type:** `bool`  
**Default:** `false`

Whether to show the floating mini monitor automatically when the app starts.

- Useful if you want the mini monitor visible on startup

### `enableGpuMetrics`

**Type:** `bool`  
**Default:** `true`

Whether to attempt to collect NVIDIA GPU metrics.

- Requires `nvidia-smi` to be available on PATH
- If not available, this setting has no effect (GPU data will show as "N/A")
- Set to `false` to skip GPU polling entirely

### `enableDiskMetrics`

**Type:** `bool`  
**Default:** `true`

Whether to collect disk read/write metrics for the Ollama process.

- Currently best-effort; may show "N/A" on some Windows versions
- Set to `false` to disable disk metric collection

### `highCpuThresholdPercent`

**Type:** `double` (0–100)  
**Default:** `80`

The CPU usage percentage threshold that triggers the **Orange** tray icon state (HighUsage).

When Ollama's CPU usage exceeds this threshold and a model is loaded, the tray icon turns orange to indicate active, resource-intensive work.

**Examples:**
```json
"highCpuThresholdPercent": 50   // Aggressive threshold
"highCpuThresholdPercent": 80   // Moderate (default)
"highCpuThresholdPercent": 95   // Conservative
```

### `highMemoryThresholdGb`

**Type:** `double`  
**Default:** `16`

The RAM usage (in GB) threshold that triggers the **Orange** tray icon state.

When Ollama's memory usage exceeds this threshold, the tray icon turns orange.

**Examples:**
```json
"highMemoryThresholdGb": 4      // Tight constraint
"highMemoryThresholdGb": 16     // Moderate (default)
"highMemoryThresholdGb": 32     // Permissive
```

### `highGpuThresholdPercent`

**Type:** `double` (0–100)  
**Default:** `85`

The GPU usage percentage threshold that triggers the **Orange** state.

When GPU utilization exceeds this threshold, the tray icon turns orange.

**Examples:**
```json
"highGpuThresholdPercent": 70   // Aggressive
"highGpuThresholdPercent": 85   // Moderate (default)
"highGpuThresholdPercent": 99   // Conservative
```

### `enableVerboseLogging`

**Type:** `bool`  
**Default:** `false`

Enables detailed diagnostic-level logging in the app log (lines tagged `[VERBOSE]`).

Settings window: **Settings → General → "Enable verbose (debug) logging"**.

- **Off (default):** only the regular INFO/WARN/ERROR lines are written
- **On:** diagnostic captures are logged too - for example, the Mini Monitor topmost guard records the demoting actor's identity (reference window pid/process/class/title, sender thread, foreground window) as a `(forensics)` line beside each event
- Useful while actively investigating an issue; turn off again afterwards to keep the log low-noise

Log location: `%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\logs\`

Changes apply live on the next refresh cycle (no restart needed).

### `enableNotifications`

**Type:** `bool`  
**Default:** `true`

Enables Windows toast notifications for configured events.

### `notificationEvents`

**Type:** `flags` (`int`)  
**Default:** `271`

Bitmask of enabled notification event types. The default includes:
- Ollama offline/online
- Model loaded/unloaded
- Model operation failed

### `notificationDebounceSeconds`

**Type:** `int`  
**Default:** `30`

Minimum interval between repeated notifications of the same event type.

### `showCpuInMiniMonitor`

**Type:** `bool`  
**Default:** `false`

Whether to display CPU usage in the Mini Monitor window.

- `true`: Shows CPU metrics in the Mini Monitor
- `false`: CPU metrics hidden

Changes apply live on the next refresh cycle (no restart needed).

### `showMemoryInMiniMonitor`

**Type:** `bool`  
**Default:** `false`

Whether to display memory usage in the Mini Monitor window.

- `true`: Shows memory metrics in the Mini Monitor
- `false`: Memory metrics hidden

### `showContextInMiniMonitor`

**Type:** `bool`  
**Default:** `false`

Whether to display live context-window usage in the Mini Monitor window.

Values are parsed from Ollama's server log (`n_ctx_slot`, `task.n_tokens`, and `tg` tokens/second lines), tracked per concurrent task:

- `true`: Shows a "Context: …" line with per-task tokens used, total slot, percentage, and tokens/second
- `false`: Context metrics hidden

Changes apply live on the next refresh cycle (no restart needed).

### `showOllamaLogsInMiniMonitor`

**Type:** `bool`  
**Default:** `false`

Whether to display a collapsible Ollama logs panel in the Mini Monitor window.

When enabled:
- A collapsible "📋 Logs" panel appears showing the last 5 lines of Ollama server output
- The panel starts collapsed; click to expand and view logs
- Logs update in real-time as Ollama runs
- Log source is hybrid:
  - If the app started Ollama: captures redirected stdout/stderr from the `ollama serve` process
  - If Ollama was already running: tails the most recently written of the known Ollama log locations —
    `%USERPROFILE%\.ollama\logs\server.log` (CLI/server install) or `%LOCALAPPDATA%\Ollama\server.log` (Ollama for Windows desktop app)

Changes apply live on the next refresh cycle (no restart needed).

## Editing Configuration

### Option 1: CLI Commands

In Phase 1, you can use `ollamamon config` commands to manage the endpoint and refresh interval:

```bash
# View current configuration
ollamamon config

# Change the Ollama endpoint
ollamamon config set endpoint http://192.168.1.100:11434

# Change refresh interval to 5 seconds
ollamamon config set refresh-interval 5

# Reset to default settings
ollamamon config reset
```

**Note:** To change thresholds, GPU metrics, or disk metrics settings, use Option 2 (direct file edit) below.

### Option 2: Direct File Edit

1. Open the settings file in a text editor (Notepad, Visual Studio Code, etc.):
   ```
   %LOCALAPPDATA%\ItsAlways710\OllamaMonitor\settings.json
   ```

2. Edit the JSON values

3. Save the file

4. Restart the application for changes to take effect

**Example:**
```json
{
  "endpoint": "http://192.168.1.50:11434",
  "unloadStrategy": 0,
  "refreshIntervalSeconds": 3,
  "showFloatingWindowOnStart": false,
  "enableGpuMetrics": true,
  "enableDiskMetrics": true,
  "highCpuThresholdPercent": 75,
  "highMemoryThresholdGb": 12,
  "highGpuThresholdPercent": 80,
  "enableNotifications": true,
  "notificationEvents": 271,
  "notificationDebounceSeconds": 30
}
```

## Tray Icon States

The tray icon color reflects the current state, which is partly determined by your threshold settings:

| State | Color | Trigger | CPU/RAM/GPU |
|-------|-------|---------|---------|
| NotReachable | Gray | Ollama API unreachable | — |
| Running | Green | API reachable, no model | Low |
| ModelLoaded | Blue | Model loaded | Low |
| HighUsage | Orange | Model running | Exceeds threshold |
| Error | Red | Unexpected error | — |

The thresholds you set control when the app transitions from `ModelLoaded` to `HighUsage`.

## Troubleshooting Configuration

### Settings file is corrupted or missing

If the settings file is corrupted, the app will log an error and attempt to use defaults. To reset:

```bash
ollamamon config reset
```

This recreates the file with default values.

### Changes don't take effect immediately

Configuration changes require an app restart. Either:
- Close the app from the tray menu
- Run `ollamamon` again to launch a fresh instance

### "Endpoint unreachable" message

If you see "Endpoint unreachable" in the floating window:

1. Verify Ollama is actually running: `ollama serve`
2. Check the endpoint setting: `ollamamon config` 
3. Test connectivity: `curl http://localhost:11434/api/version` (or your configured endpoint)
4. If Ollama is on a different machine, use that IP/hostname instead

### GPU metrics show "N/A"

This is normal if:
- NVIDIA GPU is not installed
- `nvidia-smi` is not installed or not on PATH
- GPU metrics are disabled in settings

To enable GPU metrics:

```bash
ollamamon config set gpu-metrics true
```

Then verify `nvidia-smi` is available:

```bash
nvidia-smi
```

If `nvidia-smi` is not found, install NVIDIA drivers from [nvidia.com](https://www.nvidia.com/Download/driverDetails.aspx).

## Advanced: Custom Refresh Interval

If you want to balance responsiveness and CPU usage:

- **1–2 seconds:** Highly responsive but slightly more CPU (good for demo/presentation)
- **3–5 seconds:** Reasonable balance
- **10+ seconds:** Light-weight monitoring (good for background use)

```bash
ollamamon config set refresh-interval 10
```

## Advanced: Remote Ollama Monitoring

To monitor Ollama running on another machine:

1. Ensure Ollama is accessible from your machine (firewall rules, etc.)
2. Set the endpoint to the remote machine:
   ```bash
   ollamamon config set endpoint http://<remote-ip>:11434
   ```
3. Launch the app: `ollamamon`

**Note:** Remote monitoring is best-effort in Phase 1. For production use, consider Phase 2 features.

---

**Next Steps:**
- [Development Guide](development-guide.md) — Build and modify the app
- [Troubleshooting](troubleshooting.md) — Common issues and fixes
- [Architecture Guide](architecture.md) — Technical deep dive
