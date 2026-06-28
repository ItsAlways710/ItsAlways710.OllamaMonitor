# Implementation Plan: Mini Monitor Optional Display Controls

**Author:** Neo (Lead Architect)  
**Date:** 2026-06-28  
**Status:** Draft — Pending elbruno sign-off on open questions

---

## Summary

Three changes to MiniMonitorWindow:
1. CPU/Memory display toggled via settings (DISABLED by default)
2. Ollama logs display toggled via settings
3. If logs enabled, show in a collapsible panel (last 5 lines)

---

## Architecture Context

| Artifact | Path |
|----------|------|
| Mini Monitor XAML | `src/ElBruno.OllamaMonitor/MiniMonitorWindow.xaml` |
| Mini Monitor code-behind | `src/ElBruno.OllamaMonitor/MiniMonitorWindow.xaml.cs` |
| **Shared ViewModel** | `src/ElBruno.OllamaMonitor/ViewModels/MainWindowViewModel.cs` |
| AppSettings record | `src/ElBruno.OllamaMonitor/Configuration/AppSettings.cs` |
| Settings service | `src/ElBruno.OllamaMonitor/Configuration/AppSettingsService.cs` |
| Settings Window XAML | `src/ElBruno.OllamaMonitor/Windows/SettingsWindow.xaml` |
| Settings Window VM | `src/ElBruno.OllamaMonitor/ViewModels/SettingsWindowViewModel.cs` |
| Ollama CLI service | `src/ElBruno.OllamaMonitor/Services/OllamaCliService.cs` |
| App bootstrap | `src/ElBruno.OllamaMonitor/App.xaml.cs` (lines 73-130) |

**Critical finding:** `MiniMonitorWindow.DataContext = _mainWindowViewModel` (App.xaml.cs:99). The mini monitor shares `MainWindowViewModel`—no separate VM exists.

---

## Change 1: Optional CPU/Memory in Mini Monitor

### 1.1 Settings Model

**File:** `Configuration/AppSettings.cs`

```csharp
public bool ShowCpuInMiniMonitor { get; init; } = false;   // DISABLED by default
public bool ShowMemoryInMiniMonitor { get; init; } = false; // DISABLED by default
```

### 1.2 ViewModel Additions

**File:** `ViewModels/MainWindowViewModel.cs`

Add two computed visibility properties:

```csharp
private bool _showCpuInMiniMonitor;
private bool _showMemoryInMiniMonitor;

public bool ShowCpuInMiniMonitor
{
    get => _showCpuInMiniMonitor;
    private set => SetProperty(ref _showCpuInMiniMonitor, value);
}

public bool ShowMemoryInMiniMonitor
{
    get => _showMemoryInMiniMonitor;
    private set => SetProperty(ref _showMemoryInMiniMonitor, value);
}
```

Initialize from settings in the constructor (or a `LoadSettings` helper called at startup).  
Update when settings are reloaded (see §5 live-apply).

### 1.3 XAML Changes

**File:** `MiniMonitorWindow.xaml` — wrap CpuText and MemoryText TextBlocks with `Visibility` bindings:

```xml
<TextBlock Text="{Binding CpuText}" ...
           Visibility="{Binding ShowCpuInMiniMonitor, Converter={StaticResource BoolToVisibilityConverter}}" />
<TextBlock Text="{Binding MemoryText}" ...
           Visibility="{Binding ShowMemoryInMiniMonitor, Converter={StaticResource BoolToVisibilityConverter}}" />
```

Add `BooleanToVisibilityConverter` to Window.Resources (WPF built-in).

### 1.4 Settings UI

**File:** `Windows/SettingsWindow.xaml` — under "Metrics Collection" section, add two new checkboxes (editable, not read-only):

```xml
<TextBlock Style="{StaticResource FieldLabelStyle}" Text="Show CPU in Mini Monitor"/>
<CheckBox x:Name="ShowCpuInMiniMonitorCheckBox" IsChecked="{Binding ShowCpuInMiniMonitor}" .../>

<TextBlock Style="{StaticResource FieldLabelStyle}" Text="Show Memory in Mini Monitor"/>
<CheckBox x:Name="ShowMemoryInMiniMonitorCheckBox" IsChecked="{Binding ShowMemoryInMiniMonitor}" .../>
```

**File:** `ViewModels/SettingsWindowViewModel.cs` — add corresponding properties + wire in `LoadSettings()` and `SaveAsync()`.

---

## Change 2: Optional Ollama Logs Display

### 2.1 Settings Model

**File:** `Configuration/AppSettings.cs`

```csharp
public bool ShowOllamaLogsInMiniMonitor { get; init; } = false; // DISABLED by default
```

### 2.2 ViewModel Additions

**File:** `ViewModels/MainWindowViewModel.cs`

```csharp
private bool _showOllamaLogsInMiniMonitor;
public bool ShowOllamaLogsInMiniMonitor
{
    get => _showOllamaLogsInMiniMonitor;
    private set => SetProperty(ref _showOllamaLogsInMiniMonitor, value);
}
```

### 2.3 Settings UI

Same pattern as §1.4 — add a checkbox under the existing mini-monitor toggles.

---

## Change 3: Collapsible Logs Panel (Last 5 Lines)

### 3.1 XAML — Collapsible Panel

**File:** `MiniMonitorWindow.xaml`

Insert a new row (Row 3, shift footer to Row 4) between the metrics StackPanel and the footer Grid:

```xml
<RowDefinition Height="Auto" />  <!-- new logs row -->
```

Panel content:

```xml
<Expander Grid.Row="3"
          Header="📋 Logs"
          IsExpanded="{Binding IsLogsPanelExpanded}"
          Visibility="{Binding ShowOllamaLogsInMiniMonitor, Converter={StaticResource BoolToVisibilityConverter}}"
          Foreground="#FFD1D5DB"
          Margin="0,8,0,0">
    <ItemsControl ItemsSource="{Binding OllamaLogLines}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding}"
                           Foreground="#FFA3A3A3"
                           FontSize="10"
                           FontFamily="Consolas"
                           TextTrimming="CharacterEllipsis" />
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</Expander>
```

Window height should increase slightly (~50px when expanded). Consider `SizeToContent="Height"` or increasing Height from 230 → 310 when logs are visible.

### 3.2 ViewModel — Log Buffer

**File:** `ViewModels/MainWindowViewModel.cs`

```csharp
private bool _isLogsPanelExpanded;
public bool IsLogsPanelExpanded
{
    get => _isLogsPanelExpanded;
    set => SetProperty(ref _isLogsPanelExpanded, value);
}

public ObservableCollection<string> OllamaLogLines { get; } = new();
```

Trimming logic (called by log service callback):

```csharp
private void AppendLogLine(string line)
{
    // Must be called on UI thread (Dispatcher)
    OllamaLogLines.Add(line);
    while (OllamaLogLines.Count > 5)
        OllamaLogLines.RemoveAt(0);
}
```

### 3.3 ⚠️ Logs Data Source — OPEN DESIGN QUESTION

**Finding:** No existing source of Ollama process logs exists in the codebase today.

- `OllamaCliService.StartOllama()` (line 93-108) starts `ollama serve` with `CreateNoWindow=true` but does NOT redirect stdout/stderr.
- `DiagnosticsLogService` is the app's own internal log writer, not an Ollama log consumer.
- The Ollama API (`/api/...`) has no log-streaming endpoint.
- No file-watching of Ollama's log directory exists.

**Recommended approach (needs sign-off):**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A. `ollama logs` CLI** | New service that periodically runs `ollama logs --last 5` (if the CLI supports it) or reads Ollama's log file (`%USERPROFILE%\.ollama\logs\server.log` on Windows). | No process ownership needed; works for externally started Ollama. | Polling latency; file path varies by OS/version. |
| **B. Redirect stdout/stderr from managed `ollama serve`** | Modify `OllamaCliService.StartOllama()` to redirect output, subscribe via `OutputDataReceived`/`ErrorDataReceived`. | Real-time; simple. | Only works when *this* app starts Ollama; not when Ollama is already running externally. |
| **C. Hybrid (recommended)** | If the monitor started Ollama → use redirected output (Option B). Otherwise → tail Ollama's log file at the platform-known path. | Covers both scenarios. | Slightly more complex; must handle file not found gracefully. |

**Recommended: Option C (Hybrid)** — introduce an `OllamaLogService` (Tank) that:
1. If `OllamaCliService` owns the process → subscribe to redirected stderr.
2. Otherwise → tail `%USERPROFILE%\.ollama\logs\server.log` (last 5 lines, poll every 2-5 s).
3. Expose event `Action<string> OnLogLine` consumed by ViewModel.
4. Threading: raise events on background thread; ViewModel dispatches to UI thread.

---

## 4. Settings Persistence & Migration

### 4.1 Backward Compatibility

`AppSettings` is a C# `record` with `init` default values. `System.Text.Json` deserialization with missing keys uses the record defaults (`false` for all new flags). **No migration code needed** — existing `settings.json` files automatically gain disabled features.

### 4.2 Save Flow

`SettingsWindowViewModel.SaveAsync()` already uses the reload-then-merge-then-save pattern. Add the three new properties to the `with` expression in that method.

---

## 5. Live-Apply Strategy

**Approach:** Settings changes apply **on next refresh cycle** (every 2s by default), NOT requiring app restart.

Implementation:
- `MainWindowViewModel.RefreshAsync()` already calls `_settingsService.LoadAsync()` each cycle (verified at line ~300+ for threshold checks).
- Add reads of `ShowCpuInMiniMonitor`, `ShowMemoryInMiniMonitor`, `ShowOllamaLogsInMiniMonitor` from loaded settings in `RefreshAsync()` → update the bound bool properties.
- The `OllamaLogService` starts/stops tailing based on the flag (lazy start on first `true`, stop polling on `false`).

This avoids the "restart required" UX for these lightweight toggles.

---

## 6. Testing Notes (Switch)

| # | Test Case | Expected |
|---|-----------|----------|
| 1 | Fresh install (no settings.json) → open mini monitor | CPU/Memory/Logs NOT visible |
| 2 | Enable CPU + Memory in Settings → mini monitor updates within 2s | CPU/Memory rows appear |
| 3 | Disable CPU in Settings → mini monitor hides CPU within 2s | CPU row gone, Memory remains |
| 4 | Enable Logs toggle, Ollama running | Logs panel visible (collapsed) |
| 5 | Expand logs panel | Shows up to 5 lines, monospace |
| 6 | Logs buffer > 5 lines received | Oldest lines evicted, always ≤ 5 |
| 7 | Disable Logs toggle while panel expanded | Panel hides immediately |
| 8 | Ollama not running + logs enabled | Panel visible but empty or shows "No logs" |
| 9 | Settings.json edited externally (CLI) → app picks up | Changes reflect within refresh interval |
| 10 | Upgrade from old settings.json (no new keys) | Defaults applied (all disabled), no crash |

---

## 7. Slice Breakdown & Ownership

| Slice | Owner | Dependencies | Est. |
|-------|-------|--------------|------|
| **S1: Settings model + persistence** | Tank | None | 1h |
| **S2: Settings UI (3 checkboxes)** | Trinity | S1 | 1h |
| **S3: Mini monitor CPU/Memory visibility** | Trinity | S1 | 1h |
| **S4: OllamaLogService (log capture)** | Tank | elbruno sign-off on Option C | 3-4h |
| **S5: Mini monitor logs panel (XAML + VM)** | Trinity | S1, S4 | 2h |
| **S6: Smoke test execution** | Switch | S3, S5 | 1h |
| **S7: Docs update (configuration.md)** | Morpheus | S1 | 30m |

**Recommended order:** S1 → S2 + S3 (parallel) → S4 → S5 → S6 + S7 (parallel)

**Critical path:** S4 (log service) is the highest-risk slice — depends on design decision.

---

## 8. Open Questions for @elbruno

1. **Logs source (§3.3):** Approve Option C (hybrid: redirected stdout when we own process, file tail otherwise)? Or prefer simpler Option A (file-tail only)?

2. **Ollama log file path:** Is `%USERPROFILE%\.ollama\logs\server.log` the correct path on your setup? (Varies by Ollama version/install method.)

3. **Window resizing:** Should the mini monitor grow vertically when logs are expanded (SizeToContent), or stay fixed size with internal scroll?

4. **Log content filtering:** Show raw Ollama log lines, or filter to INFO+ / strip timestamps?

5. **Scope of "Ollama Logs":** Should this show ALL Ollama server output, or only lines matching specific patterns (requests, errors)?

---

## 9. Non-Goals

- No log persistence/export from mini monitor
- No log level filtering UI (v1)
- No CPU/Memory history sparklines in this slice (existing separate feature)
- No changes to the main DetailWindow metrics display
