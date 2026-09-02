# Troubleshooting Guide

## Common Issues and Solutions

### App doesn't start

**Symptom:** Running `ollamamon` does nothing, or the app crashes immediately.

**Solutions:**

1. **Check .NET is installed:**
   ```bash
   dotnet --version
   ```
   If not installed, download .NET 10 from [dotnet.microsoft.com](https://dotnet.microsoft.com).

2. **Check logs:**
   Open the diagnostics log file at:
   ```
   %LOCALAPPDATA%\ItsAlways710\OllamaMonitor\logs\
   ```
   Look for error messages.

3. **Try from command line with verbose output:**
   ```bash
   dotnet run --project src/ItsAlways710.OllamaMonitor/ 2>&1
   ```

4. **Reinstall the global tool:**
   ```bash
   dotnet tool uninstall --global ItsAlways710.OllamaMonitor
   dotnet tool install --global ItsAlways710.OllamaMonitor
   ```

### Tray icon doesn't appear

**Symptom:** App runs but no icon in system tray.

**Solutions:**

1. **Check if the app is running:**
   - Open Task Manager (Ctrl+Shift+Esc)
   - Look for `ItsAlways710.OllamaMonitor` process
   - If it's not there, the app crashed (check logs)

2. **Check Windows tray settings:**
   - Right-click the system tray (clock area)
   - Click "Show hidden icons"
   - Look for the app icon

3. **Check if it's minimized:**
   - Press Alt+Tab to cycle through windows
   - Look for the app window

4. **Review Windows event logs:**
   - Press Win+R, type `eventvwr.msc`
   - Check "Windows Logs → Application" for errors

### Tray icon shows gray (Not Reachable)

**Symptom:** The tray icon is always gray, even though Ollama is running.

**Solutions:**

1. **Verify Ollama is actually running:**
   ```bash
   curl http://localhost:11434/api/version
   ```
   If this fails, Ollama is not running. Start it:
   ```bash
   ollama serve
   ```

2. **Check the configured endpoint:**
   ```bash
   ollamamon config
   ```
   Look for the `endpoint` value. Verify it matches where Ollama is running.

3. **If Ollama is on a remote machine:**
   Get the IP or hostname of that machine, then:
   ```bash
   ollamamon config set endpoint http://<remote-ip>:11434
   ```

4. **Check firewall:**
   If Ollama is on another machine, the firewall may be blocking port 11434.
   - On the Ollama machine, allow inbound on port 11434
   - On your machine, verify you can ping the remote machine

5. **Restart the app:**
   ```bash
   # Close it from the tray menu (Exit)
   # Then launch again:
   ollamamon
   ```

### Tray icon stuck on one color

**Symptom:** Tray icon doesn't change even when you load/unload models or change resource usage.

**Solutions:**

1. **Check the refresh interval:**
   ```bash
   ollamamon config
   ```
   Look for `refreshIntervalSeconds`. Try increasing it:
   ```bash
   ollamamon config set refresh-interval 1
   ```

2. **Manually refresh:**
   - Right-click the tray icon
   - Click "Refresh" (if available)

3. **Restart the app:**
   - Click the tray icon
   - Click "Exit"
   - Run `ollamamon` again

4. **Check logs for errors:**
   Open `%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\logs\` and look for warnings or errors.

### Floating window doesn't show

**Symptom:** You click the tray icon but the floating details window doesn't appear.

**Solutions:**

1. **Check if the window is already open but off-screen:**
   - Try Alt+Tab to see if the window is listed
   - Try moving your mouse to different screen edges (in case it's hidden)

2. **Try manually showing it:**
   - Right-click the tray icon
   - Click "Show" or similar option (if available)

3. **Check if it's disabled in settings:**
   ```bash
   ollamamon config
   ```
   Look for `showFloatingWindowOnStart`. Try:
   ```bash
   ollamamon config set show-floating-window true
   ```

4. **Check logs for rendering errors:**
   Open the logs directory and look for WPF-related errors.

### GPU metrics show "N/A"

**Symptom:** The floating window shows "GPU: N/A" even though you have an NVIDIA GPU.

**Solutions:**

1. **Check if GPU metrics are enabled:**
   ```bash
   ollamamon config
   ```
   Look for `enableGpuMetrics`. If it's `false`, enable it:
   ```bash
   ollamamon config set enable-gpu-metrics true
   ```

2. **Check if nvidia-smi is installed:**
   ```bash
   nvidia-smi
   ```
   If not found, install or update NVIDIA drivers from [nvidia.com](https://www.nvidia.com/Download/driverDetails.aspx).

3. **Check nvidia-smi manually:**
   ```bash
   nvidia-smi --query-gpu=name,utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits
   ```
   If this command fails, the issue is with your NVIDIA drivers, not the app.

4. **Try different nvidia-smi path:**
   The app looks for `nvidia-smi` on PATH. If it's installed somewhere else, add it to PATH.

### Settings file is corrupted

**Symptom:** App shows errors about invalid JSON or settings.

**Solution:**

Reset to defaults:

```bash
ollamamon config reset
```

This deletes and recreates the settings file with default values.

If you want to manually edit, open:
```
%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\settings.json
```

Verify it's valid JSON. Use a JSON validator at [jsonlint.com](https://www.jsonlint.com) if unsure.

### Config commands don't work

**Symptom:** Running `ollamamon config set endpoint ...` returns an error or shows nothing.

**Solutions:**

1. **Check syntax:**
   ```bash
   # Correct:
   ollamamon config set endpoint http://localhost:11434
   ollamamon config set refresh-interval 2
   
   # Incorrect (missing arguments):
   ollamamon config set endpoint
   ollamamon config set
   ```

2. **Check key names:**
   Valid keys are:
   - `endpoint`
   - `refresh-interval`
   - Other advanced keys (see Configuration Guide)

3. **Try from the same directory:**
   ```bash
   cd %LOCALAPPDATA%\ItsAlways710\OllamaMonitor
   ollamamon config
   ```

4. **Check logs:**
   Open `%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\logs\` for error details.

### High CPU usage

**Symptom:** The app is using 20%+ CPU even when idle.

**Solutions:**

1. **Increase refresh interval:**
   ```bash
   ollamamon config set refresh-interval 10
   ```
   Higher interval = less frequent polling = lower CPU.

2. **Disable GPU metrics:**
   ```bash
   ollamamon config set enable-gpu-metrics false
   ```
   GPU polling can be expensive; try disabling it temporarily.

3. **Disable disk metrics:**
   ```bash
   ollamamon config set enable-disk-metrics false
   ```

4. **Check if Ollama process itself is using CPU:**
   Open Task Manager and look at the `ollama` process. If it's using high CPU, it's not an issue with this app.

5. **Check logs:**
   Open logs for any repeated errors that might trigger constant retries.

### App crashes randomly

**Symptom:** The app works for a while, then suddenly closes.

**Solutions:**

1. **Check logs immediately after crash:**
   Open `%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\logs\` and look for the last entries. They should indicate the error.

2. **Reduce refresh interval:**
   ```bash
   ollamamon config set refresh-interval 5
   ```
   If a bug only triggers during fast polling, reducing frequency can help.

3. **Disable GPU metrics:**
   GPU polling is the most likely culprit for random crashes. Try:
   ```bash
   ollamamon config set enable-gpu-metrics false
   ```

4. **Check event viewer:**
   - Press Win+R, type `eventvwr.msc`
   - Go to "Windows Logs → Application"
   - Look for crash events related to the app

5. **Reinstall:**
   ```bash
   dotnet tool uninstall --global ItsAlways710.OllamaMonitor
   dotnet tool install --global ItsAlways710.OllamaMonitor
   ```

### "Request timeout" or "Connection refused" errors

**Symptom:** Tray icon flashes red or you see "Timeout" in the floating window.

**Solutions:**

1. **Verify Ollama is running:**
   ```bash
   curl http://localhost:11434/api/version
   ```

2. **Check endpoint is correct:**
   ```bash
   ollamamon config
   ```

3. **Increase HTTP timeout in code (if building from source):**
   Edit `App.xaml.cs`, find:
   ```csharp
   _httpClient.Timeout = TimeSpan.FromSeconds(5);
   ```
   Increase the value (e.g., to 10 seconds) if your network is slow.

4. **Check network:**
   - If Ollama is remote, test connectivity: `ping <ollama-ip>`
   - Check for firewall rules blocking port 11434

### License or "Not a valid global tool" error

**Symptom:** When installing, you see a license or validation error.

**Solution:**

Ensure you're using .NET 10:
```bash
dotnet --version
```

Then try installing again:
```bash
dotnet tool install --global ItsAlways710.OllamaMonitor
```

If still failing, check your NuGet configuration:
```bash
dotnet nuget list source
```

---

## Getting Help

1. **Check the logs:** `%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\logs\`
2. **Read the Configuration Guide:** [Configuration](configuration.md)
3. **Review the Architecture:** [Architecture Guide](architecture.md)
4. **Open an issue on GitHub** with logs and steps to reproduce

---

## Advanced Debugging

### Enable verbose logging

Turn on **Settings → General → "Enable verbose (debug) logging"** (or set
`"enableVerboseLogging": true` in `settings.json`; applies live, no restart). This
adds detailed `[VERBOSE]` lines - e.g. the Mini Monitor topmost guard recording
which process/actor demoted a window - that are suppressed by default.

Log location: `%LOCALAPPDATA%\ItsAlways710\OllamaMonitor\logs\`

### Manually test the Ollama API

```bash
# Test connectivity
curl http://localhost:11434/api/version

# Get loaded models
curl http://localhost:11434/api/tags

# Get running processes
curl http://localhost:11434/api/ps
```

If any of these fail, Ollama isn't responding. Restart it and try again.

### Test nvidia-smi

```bash
# List GPU info
nvidia-smi

# Test with same format as app
nvidia-smi --query-gpu=name,utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits
```

If this fails, update your NVIDIA drivers.

---

**Questions?** See the [FAQ in README](../README.md) or [Development Guide](development-guide.md).
