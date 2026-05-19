# Configuration Guide

## Overview

ElBruno.OllamaMonitor stores configuration in a JSON file that you can edit directly or modify via CLI commands.

## Configuration File Location

```
%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json
```

On Windows:
- `%LOCALAPPDATA%` typically expands to `C:\Users\<YourUsername>\AppData\Local`
- So the full path is usually: `C:\Users\<YourUsername>\AppData\Local\ElBruno\OllamaMonitor\settings.json`

The file is created automatically with default values on first run.

## Default Configuration

```json
{
  "endpoint": "http://localhost:11434",
  "unloadStrategy": 0,
  "refreshIntervalSeconds": 2,
  "startMinimizedToTray": true,
  "showFloatingWindowOnStart": false,
  "enableGpuMetrics": true,
  "enableDiskMetrics": true,
  "highCpuThresholdPercent": 80,
  "highMemoryThresholdGb": 16,
  "highGpuThresholdPercent": 85,
  "enableNotifications": true,
  "notificationEvents": 271,
  "notificationDebounceSeconds": 30
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

### `startMinimizedToTray`

**Type:** `bool`  
**Default:** `true`

Whether the app starts minimized to the system tray (hidden from desktop).

- `true`: App launches hidden; use the tray icon to open the details window or mini monitor
- `false`: App launches with the details window visible

### `showFloatingWindowOnStart`

**Type:** `bool`  
**Default:** `false`

Whether to show the standard details window automatically when the app starts.

- Works independently of `startMinimizedToTray`
- Useful if you want the details visible on startup

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
   %LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json
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
  "startMinimizedToTray": true,
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
