# Squad Decisions

## Phase 1 Architecture

### Decision: Phase 1 Implementation Contract

**Author:** Neo (Lead Architect)  
**Date:** 2026-04-24  
**Status:** Approved

- **.NET SDK:** 10.0.203 stable (`net10.0-windows` TFM)
- **Project layout:** All source in `src/`, all docs in `docs/`, root holds README + LICENSE + .sln
- **Ownership:** Tank (CLI/services/config), Trinity (WPF/tray), Morpheus (docs), Switch (validation)
- **Target framework & output:** WinExe, PackAsTool, ToolCommandName = "ollamamon"
- **CLI routing:** Simple string matching in Program.cs (no third-party CLI framework)
- **Ollama client:** System.Net.Http (singleton), System.Text.Json deserialization
- **Process metrics:** System.Diagnostics.Process (CPU two-sample, memory, disk I/O)
- **GPU metrics:** Best-effort nvidia-smi (graceful degradation if missing/error)
- **Tray icon colors:** 5-color model (Gray/Green/Blue/Orange/Red)
- **Configuration:** `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json`, default refresh 2s
- **Simplifications Phase 1:** No DI container, no MVVM framework, no unit tests, no logging framework, no settings dialog, no icon resources (generated at runtime)

**High-risk mitigations:**
- H.NotifyIcon.Wpf net10 support: check NuGet, fallback to System.Windows.Forms interop
- WinExe + PackAsTool console output: use P/Invoke AttachConsole if needed
- CPU % two-sample: async measurement, first refresh may show 0%
- Disk I/O per-process: best-effort, mark "N/A" if unavailable
- nvidia-smi spawn: cache GPU data 5s minimum, background thread

---

### Decision: Tank Phase 1 Core Implementation

**Author:** Tank (Platform Developer)  
**Date:** 2026-04-24  
**Status:** Approved

- **Two-project split:** `src\ElBruno.OllamaMonitor` (WPF desktop, `net10.0-windows`) + `src\ElBruno.OllamaMonitor.Tool` (tool shim, `net10.0`)
- **Packaging strategy:** `build\Pack-Tool.ps1` publishes desktop, packs tool, then `build\Inject-DesktopPayload.ps1` enriches .nupkg with desktop payload
- **Shared source linking:** CLI, config, diagnostics, interop modules linked from desktop to tool project (ensures behavioral consistency)
- **Service implementations:**
  - OllamaClient (HTTP), OllamaStatusService (state aggregation)
  - ProcessMetricsService (CPU/memory), GpuMetricsService (nvidia-smi), DiskMetricsService (I/O)
  - AppSettingsService (config file), StatusFormatter (plain text), ClipboardService (copy)
- **CLI routing:** Program.cs owns --help, config (show/set/reset), default launch WPF
- **GPU metrics safety:** Returns "unavailable" state instead of failing when nvidia-smi missing
- **Configuration path:** `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json`

---

### Decision: Morpheus Phase 1 Documentation

**Author:** Morpheus (Documentation Lead)  
**Date:** 2026-04-24  
**Status:** Approved

- **Root documentation:** README.md (concise 5K chars) + LICENSE (placeholder, awaiting license choice)
- **Technical documentation:** architecture.md, configuration.md, development-guide.md, troubleshooting.md (15+ issues), release-notes.md
- **Promotional materials:** blog-post.md (~2500 words), 3 LinkedIn variants, 5 Twitter variants, image-prompts.md (9 detailed prompts)
- **Images:** .gitkeep placeholder (no T2I capability; images deferred)
- **Documentation philosophy:** Practical, developer-focused, Bruno/El Bruno style (friendly, slightly humorous), Phase 1 scoped only
- **Scope decisions:**
  - README concise with links (not exhaustive)
  - License placeholder pending team decision (MIT/Apache 2.0/etc.)
  - Troubleshooting prioritized by frequency/impact (not A-Z)
  - Architecture moderate depth (class names, flows, not line-by-line)
  - Configuration at `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json` (matches implementation)
- **Alignment:** All required docs present, Phase 2 roadmap noted, promotional variants ready for social media

---

### Decision: Switch Phase 1 Validation & Release

**Author:** Switch (Validation & Release Engineering)  
**Date:** 2026-04-24  
**Status:** Approved for Private Release

- **Validation gates all passed:**
  - Solution builds (`dotnet build`)
  - CLI commands functional (help, config show/set/reset)
  - Tool packages correctly (Pack-Tool.ps1 + Inject-DesktopPayload.ps1)
  - Documentation complete (5 technical + root + promotional)
  - No Phase 2+ inventions
  - Ownership boundaries clear
- **Smoke test coverage:**
  - Build verification
  - CLI routing verification
  - Headless tool launch (unreachable endpoint, gray icon state persists)
- **Limitations explicitly documented:**
  - Non-interactive tray verification (manual only)
  - GPU metrics best-effort (nvidia-smi dependency)
  - No remote monitoring Phase 1
  - No unit tests Phase 1
  - Ollama endpoint reachability via error handling (graceful degradation)
- **Approval criteria met:** All acceptance gates passed. Ready for internal release.

---

## Phase 2 Sprint Planning

### Decision: Sprint Backlog Triage & Prioritization
**Author:** Neo (Lead Architect)
**Date:** 2026-04-25
**Status:** Approved

Comprehensive 5-tier sprint breakdown completed:
- **TIER 1 (Critical):** Squad state merge (T1.1), docs cleanup (T1.2)
- **TIER 2 (UI/UX):** Mini monitor improvements (T2.1-T2.5), details window, themes, tray menu (T2.6-T2.8)
- **TIER 3 (CI):** GitHub Actions optimization for version tags (T3.1-T3.3)
- **TIER 4 (QA):** Parity benchmark design & execution (T4.1-T4.2)
- **TIER 5 (Optional):** Team expansion as needed

Estimated scope: ~40-50 story points across 5 weeks.

Open questions resolved:
- **CI Strategy:** Approved Option A (NuGet/build caching, 15-20% minute savings, low risk)
- **Mini Monitor Height:** Approved 310px increase to accommodate sparklines
- **Theme Support:** Approved as Phase 2b addition (light/dark/system)

---

### Decision: CI Optimization Recommendation
**Author:** Tank (Platform Developer)
**Date:** 2026-04-25
**Status:** Approved

GitHub Actions workflow optimization for version tags:
- Current cost: ~11-12 billing minutes per release (windows-latest)
- **Approved approach:** Option A — Aggressive NuGet & build output caching
- Expected savings: 1-2 minutes per release (15-20% reduction)
- Risk assessment: Low (standard practice, minimal cache invalidation)
- Implementation effort: 15 minutes
- Next: Implement after TIER 1 completion

---

### Decision: Squad State Consolidation
**Author:** Tank (Platform Developer)
**Date:** 2026-04-25
**Status:** Approved

All squad state successfully consolidated to `feature/sprint-improvements`:
- `.squad/decisions/` merged
- `.squad/agents/*/history.md` updated with learnings
- `.gitattributes` updated with `merge=union` drivers for append-only files
- PII/credentials verification: **CLEAN**
- Multi-machine development ready for main branch sync

---

### Decision: QA Parity Benchmark Framework
**Author:** Switch (Quality Engineer)
**Date:** 2026-04-25
**Status:** Ready for Execution

Comprehensive benchmark plan designed covering 13 realistic test scenarios:
- Normal workflows: monitor status, pull metrics, view resources
- Edge cases: no Ollama, GPU missing, network lag, rapid state changes
- Configuration changes: settings update, theme switching

Measurement thresholds:
- CPU: ±2%
- Model count: ±1%
- Response times: <5s

Recommendation: Execute after Trinity completes Phase 2b improvements (3-5 day delay) for before/after delta capture. Fallback: run baseline immediately if schedule slips.

---

### Decision: Documentation Audit & Corrections
**Author:** Morpheus (Documentation Lead)
**Date:** 2026-04-25
**Status:** Completed

Audit findings:
- No MCP tool overstatement detected (false positive)
- No "wake-up" feature references (false positive)
- **Real issue found & corrected:** `docs/configuration.md` listed Phase 2+ CLI commands (`high-cpu-threshold`, `high-memory-threshold`, `high-gpu-threshold`) as Phase 1 features
- Correction: Removed non-existent commands, clarified Phase 1 CLI supports only endpoint and refresh-interval

Impact: Low (internal docs, no customer-facing impact from correction)

---

### Decision: Mini Monitor UI Research & Design
**Author:** Trinity (Desktop Developer)
**Date:** 2026-04-25
**Status:** Design Complete, Implementation Ready

Comprehensive research on mini monitor improvements:
- **In-use model indicator:** Data available via `OllamaModelSnapshot.ExpiresAt` property (null/future = active). Implement green bullet styling, no backend changes needed.
- **Usage charts:** Recommend Canvas-based sparklines (no external dependencies, keeps Phase 1 simplicity intact). 30-sample rolling window, lightweight rendering.
- **App icon integration:** Reuse existing tray icon assets (16×16 px), simple XAML placement
- **Always-on-top relocation:** Remove header TextBlock, add to footer status bar (frees ~25px vertical space)
- **Font sizing:** Increase model labels 12px → 14px
- **Details window polish:** Match dark theme, improved spacing/padding, add app icon

Implementation sequence:
- **Phase 2a (6 hrs, quick wins):** Move always-on-top, increase fonts, add icon, implement in-use indicator
- **Phase 2b (10 hrs, advanced):** Build history buffer, Canvas sparklines, polish, test rendering performance

No blockers. No Tank dependencies. No Switch approval needed (no external dependencies).

---

### Decision: Tray Double-Click Opens Mini Monitor
**Author:** Trinity (Desktop Developer)
**Date:** 2026-04-26
**Status:** Implemented

Updated `TrayIconService.cs` line 50 to call `ShowMiniMonitorWindow()` instead of `ShowWindow()` on tray icon double-click events. Mini Monitor is now the default quick-access window from the tray; context menu remains unchanged with explicit access to both windows. Rationale: user preference + Phase 2 focus on Mini Monitor as primary interface. Build verified: ✅ Success.

### Decision: Settings UX Architecture (Tray Menu + Settings Form)

**Author:** Neo (Lead Architect)  
**Date:** 2026-04-28  
**Status:** Approved for Phase 2 Implementation

**Analysis:** Tray context menu "Settings…" entry vs. dedicated Settings form. Both complementary, both feasible.

**Recommendation:** Do both, sequenced.

**Phase 2a (3 PT, Quick-Win):**
- Trinity: Add ToolStripMenuItem("Settings…") to TrayIconService, create SettingsWindow.xaml (read-only display), implement ShowSettingsWindow() in App.xaml.cs
- Display: Endpoint, Refresh Interval, GPU/Disk flags, CPU/Memory/GPU thresholds (all read-only)
- Save handler: Async no-op spinner for Phase 2a

**Phase 2b (3–4 PT, Advanced):**
- Tier 1 editable (Endpoint + RefreshIntervalSeconds): Format validation, numeric spinner range 1–60s
- Tier 2 read-only Phase 2b (StartMinimizedToTray, EnableGpuMetrics, EnableDiskMetrics, thresholds)
- Validation: Endpoint format + optional reachability test (Phase 2b stretch)
- Restart semantics: All require restart Phase 2a; selective reload Phase 2b+

**Settings precedence (CLI + GUI coexistence):**
- Rule: Last-write-wins
- Prevention: Both writers already reload from disk before saving (AppSettingsService)
- Documentation: Add to troubleshooting.md: avoid concurrent CLI + GUI writes
- Why no file locking? Overkill Phase 2; concurrent writes rare in practice

**Ownership & responsibilities:**
- **Trinity:** SettingsWindow.xaml, tray menu integration, window lifecycle, single-instance enforcement
- **Tank:** Validation logic, AppSettingsService method calls, ensure reload-before-save
- **Switch:** Build verification, smoke tests, concurrent CLI+GUI verification

**Technical consensus:**
- Threading: async/await pattern proven in MainWindowViewModel
- Window lifecycle: Follow MainWindow/MiniMonitorWindow patterns
- Architecture clean, no new dependencies, no blockers

**Approval criteria:**
- ✅ Feasible Phase 2 addition (5–7 PT total)
- ✅ Windows standard (tray menu discovery)
- ✅ Reduces support burden (removes CLI dependency for GUI users)
- ✅ Precedence question resolved (last-write-wins acceptable)
- ✅ Phased approach prevents overload (2a quick-win, 2b polish)

---

### Decision: Settings Window Design Specification (Phase 2a + 2b)

**Author:** Neo (Lead Architect)  
**Date:** 2026-04-28  
**Status:** Implemented  

**Specification:**
- **Phase 2a (Read-only display):** SettingsWindow displays all 9 AppSettings keys; Endpoint + RefreshIntervalSeconds editable; remaining 7 fields read-only
- **File layout:** `Windows/SettingsWindow.xaml` + `SettingsWindow.xaml.cs`; namespace `ElBruno.OllamaMonitor`
- **Tray menu:** Add "Settings…" between "Show Mini Monitor" and "Refresh"; use Action delegate to avoid tight coupling
- **Window UX:** Single-column form, 500×580px resizable, sections: Connection, Application Behavior, Metrics Collection, Alert Thresholds
- **Buttons:** Save, Cancel, Reset to Defaults; footer: "⚠ Restart required for changes to take effect"
- **Validation contract:** ValidateEndpoint (empty check, URI parse, http/https scheme), ValidateRefreshInterval (range 1–60 seconds)
- **Save flow (last-write-wins):** LoadAsync → apply edits → SaveAsync (prevents CLI overwrites)
- **Restart requirement:** Phase 2a: all settings; Phase 2b+: selective reload (stretch)
- **Out of scope Phase 2a:** Reachability test, live reload, endpoint reachability button, per-field restart badges

**Implementation outcomes:**
- ✅ Trinity created SettingsWindow (XAML + CodeBehind)
- ✅ Tank created SettingsValidator for centralized validation
- ✅ CLI integrated validation (bug fix: previously accepted invalid values)
- ✅ Build succeeded (0 errors)

---

### Decision: Settings Window Implementation (Trinity)

**Author:** Trinity (Desktop Developer)  
**Date:** 2026-04-28  
**Status:** Shipped  

**Delivered:**
- `Windows/SettingsWindow.xaml` (184 lines): Single-column form, all 9 fields visible, editable TextBox for Endpoint + RefreshIntervalSeconds, read-only controls for Tier-2 fields
- `Windows/SettingsWindow.xaml.cs` (195 lines): Form logic, Save/Cancel/Reset handlers, inline validators (TODO for Tank's SettingsValidator)
- Modified `Services/TrayIconService.cs`: Added "Settings…" menu item, pass Action delegate
- Modified `App.xaml.cs`: Lifecycle management (singleton pattern), ShowSettingsWindow method, OnExit cleanup
- Validation: Inline ValidateEndpoint and ValidateRefreshInterval implemented (TODO markers for integration)
- Last-write-wins pattern: SaveButton_Click implements LoadAsync → merge → SaveAsync

**Build status:** ✅ Success (0 errors, 0 warnings, 2.5s)

**Phase 2a complete:** Settings window with tray menu access, read-only Tier-2 display, editable Endpoint + RefreshIntervalSeconds with validation

---

### Decision: Centralized Settings Validators (Tank)

**Author:** Tank (Platform Developer)  
**Date:** 2026-04-28  
**Status:** Shipped  

**Delivered:**
- `Configuration/SettingsValidator.cs`: Pure static validators (ValidateEndpoint, ValidateRefreshInterval)
- Modified `Cli/CliCommandRunner.cs`: Validation enforcement on `config set` commands (bug fix: CLI now rejects invalid values)
- Verified reload-before-save pattern already correct in AppSettingsService.UpdateEndpointAsync / UpdateRefreshIntervalAsync

**Validators:**
- `ValidateEndpoint(string endpoint)`: Empty check, URI parse (KindAbsolute), http/https scheme requirement
- `ValidateRefreshInterval(int seconds)`: Range check 1–60 seconds inclusive

**CLI bug fix:** Previously CLI saved settings without validation; now exits with code 1 on invalid input, displays error to stderr

**Build status:** ✅ Success (0 errors, 0 warnings, 4.6s)

**Architecture:** Single source of truth for validation; CLI + GUI parity guaranteed

---

### Decision: Settings File Auto-Creation (No Change Required)

**Author:** Tank (Platform Developer)  
**Date:** 2026-04-28  
**Status:** Verified  

**Finding:** AppSettingsService.LoadAsync already creates `settings.json` with default values when file missing.

**Evidence:** `LoadAsync()` (lines 18-23) checks if file exists; if not, instantiates `new AppSettings()`, calls `SaveAsync()` to write defaults to disk, returns defaults object.

**Status:** ✅ Already implemented (no code changes required)

---

### Decision: Settings Window Smoke Test Plan (Switch)

**Author:** Switch (QA)  
**Date:** 2026-04-28  
**Status:** Ready for Execution  

**Test coverage:** 29 manual test cases organized in 8 sections:
1. Tray Menu & Window Launch (4 tests)
2. Read-Only Tier-2 Fields (2 tests)
3. Editable Fields Happy Path Phase 2b (3 tests)
4. Editable Fields Validation Phase 2b (7 tests)
5. Reset/Cancel Buttons (2 tests)
6. CLI Parity & Concurrency Last-Write-Wins (3 tests)
7. Regression Check Tray Double-Click (1 test)
8. Build & Restart Semantics (2 tests)

**Estimated duration:** 15–20 minutes

**Clarifications flagged:** 3 items (Reset behavior, CLI+GUI concurrency observed behavior, MinitorWindow refresh requirement) for team confirmation before execution

**Test data:** Valid/invalid Endpoint examples, RefreshIntervalSeconds boundaries

**Execution protocol:** Manual execution post-Trinity + Tank merge; document pass/fail, resolve clarifications, sign-off with build # verified

**Status:** ✅ Plan ready for execution

---

### Decision: Validator Integration (Trinity 2nd Pass)

**Author:** Trinity (Desktop Developer)  
**Date:** 2026-04-28  
**Status:** Complete  

**Refactoring:** Replaced inline ValidateEndpoint + ValidateRefreshInterval methods in SettingsWindow.xaml.cs with calls to Tank's centralized SettingsValidator.

**Outcome:** Single source of truth for validation; SettingsWindow focuses on UI, validation logic delegated to SettingsValidator.cs

**Build status:** ✅ Success (0 errors, 0 warnings, 2.5s)

**Phase 2b validator integration:** ✅ Complete

---

### Decision: Architecture Verification (Tank 2nd Pass)

**Author:** Tank (Platform Developer)  
**Date:** 2026-04-28  
**Status:** Verified  

**Verification scope:**
- Defaults-on-missing already implemented in AppSettingsService.LoadAsync ✅
- Reload-before-save pattern already correct in UpdateEndpointAsync / UpdateRefreshIntervalAsync ✅
- No gaps identified in Phase 2b architecture ✅

**Result:** Phase 2a + 2b ready for testing; no additional backend work required

---

---

### Decision: OllamaLogService Contract and Hybrid Source Approach

**Author:** Tank (Platform Developer)  
**Date:** 2026-06-28  
**Status:** Implemented — approved by elbruno (Option C hybrid, raw log lines, all Ollama server output, log path %USERPROFILE%\.ollama\logs\server.log)

---

## Context

Mini Monitor Optional Display plan (`.squad/files/mini-monitor-optional-display-plan.md`) requires a new service that captures Ollama server log lines for display in the mini monitor panel (S4, S5).

No prior log-capture service existed. `OllamaCliService.StartOllama()` did not redirect stdout/stderr. `DiagnosticsLogService` is the app's own internal logger, not an Ollama consumer.

---

## Decision: Hybrid Log Source (Option C)

Two modes, resolved at runtime:

### Mode 1 — Process-Owned (app started Ollama via `StartOllama`)
- `OllamaCliService.StartOllama()` now optionally redirects `stdout` + `stderr` when an `OllamaLogService` reference is injected.
- Subscribes to `OutputDataReceived` / `ErrorDataReceived` events and forwards each line to `OllamaLogService.OnOwnedProcessOutput(line)`.
- Calls `OllamaLogService.SetProcessOwned()` to suppress file-tail polling.

### Mode 2 — External Ollama (already running, not started by this app)
- Polls `%USERPROFILE%\.ollama\logs\server.log` every 2 seconds.
- Opens with `FileShare.ReadWrite | FileShare.Delete` to handle file locking gracefully.
- Tracks last-read byte offset; on each poll reads only new content.
- Detects log rotation/truncation (file shrinks) and resets offset to 0.

---

## Public Interface

```csharp
// ElBruno.OllamaMonitor.Services.IOllamaLogService
public interface IOllamaLogService
{
    event Action<string>? LogLineReceived;  // fired on background thread; callers must dispatch to UI
    IReadOnlyList<string> RecentLines { get; }  // snapshot of last ≤5 lines; thread-safe
    void Start();  // idempotent; begin capture
    void Stop();   // idempotent; stop capture
}
```

---

## Internal Surface (for OllamaCliService wiring only)

```csharp
// On OllamaLogService concrete class — NOT on the interface
internal void SetProcessOwned();                // switches to process-owned mode, stops file polling
internal void OnOwnedProcessOutput(string? line); // forwards a line from the managed process
```

---

## Ring Buffer

- Max 5 most-recent lines.
- Protected by `Lock _syncRoot`. Thread-safe for concurrent writes (process events) and reads (UI bindings).
- `LogLineReceived` event fired after appending; handler exceptions are caught and logged as warnings.

---

## Log File Path

```
%USERPROFILE%\.ollama\logs\server.log
```

Resolved via:
```csharp
Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
             ".ollama", "logs", "server.log")
```

---

## Log Content

- **Raw lines** from Ollama server output (no filtering, no timestamp stripping).
- **All Ollama server output** (stdout + stderr merged) — approved by elbruno.

---

## Registration

`App.xaml.cs` (`LaunchTrayApplicationAsync`):
```csharp
_ollamaLogService = new OllamaLogService(diagnostics);
var ollamaCliService = new OllamaCliService(diagnostics, _ollamaLogService);
```

Disposed in `OnExit` via `_ollamaLogService?.Dispose()`.

---

## Files Changed

| File | Change |
|------|--------|
| `Configuration/AppSettings.cs` | +3 bool props: `ShowCpuInMiniMonitor`, `ShowMemoryInMiniMonitor`, `ShowOllamaLogsInMiniMonitor` (all `= false`) |
| `Services/IOllamaLogService.cs` | New interface (as above) |
| `Services/OllamaLogService.cs` | New concrete service (hybrid implementation) |
| `Services/OllamaCliService.cs` | Optional `OllamaLogService?` ctor param; `StartOllama` refactored; `IDisposable` added |
| `App.xaml.cs` | `_ollamaLogService` field; wired into `OllamaCliService`; disposed on exit |

---

## Handoff Notes for Trinity (S5)

- Consume `IOllamaLogService` from App's `_ollamaLogService` field — pass it into `MainWindowViewModel` constructor.
- Call `ollamaLogService.Start()` when `ShowOllamaLogsInMiniMonitor` becomes `true`; `Stop()` when it goes `false`.
- Subscribe to `LogLineReceived` on the service; marshal to UI thread via `Application.Current.Dispatcher.InvokeAsync` before updating `OllamaLogLines` observable collection.
- `RecentLines` provides an initial snapshot on startup to pre-populate the collection.
- `LogLineReceived` fires on a background thread (timer callback or process event thread) — always dispatch before touching WPF-bound collections.

---

### Decision: Mini Monitor UI / VM Changes (S2, S3, S5)

**Author:** Trinity (Desktop Developer)  
**Date:** 2026-06-28  
**Status:** Implemented — build green (0 errors)

---

## Summary

Implemented slices S2 (Settings UI), S3 (CPU/Memory visibility), and S5 (collapsible logs panel) of the Mini Monitor Optional Display plan.

---

## Files Changed

| File | Change |
|------|--------|
| `ViewModels/MainWindowViewModel.cs` | +IOllamaLogService field+param; +ShowCpuInMiniMonitor; +ShowMemoryInMiniMonitor; +ShowOllamaLogsInMiniMonitor; +IsLogsPanelExpanded; +OllamaLogLines; AppendLogLine; RefreshAsync live-apply; Dispose unsubscribe |
| `App.xaml.cs` | Pass `_ollamaLogService` as 4th arg to MainWindowViewModel constructor |
| `MiniMonitorWindow.xaml` | SizeToContent="Height"; BooleanToVisibilityConverter resource; Visibility bindings on CpuText+MemoryText; Expander row (Row 3); footer shifted to Row 4; Row 2 changed from * to Auto |
| `SettingsWindow.xaml` (root) | New "Mini Monitor Display" GroupBox with 3 bound checkboxes |
| `ViewModels/SettingsWindowViewModel.cs` | +ShowCpuInMiniMonitor; +ShowMemoryInMiniMonitor; +ShowOllamaLogsInMiniMonitor — loaded in LoadSettings(), saved in SaveAsync() with |
| `Windows/SettingsWindow.xaml` | +3 editable checkboxes under Metrics Collection |
| `Windows/SettingsWindow.xaml.cs` | LoadSettingsAsync loads 3 new checkboxes; OnSaveClicked includes them in `with` expression |

---

## New VM Members (MainWindowViewModel)

```csharp
public bool ShowCpuInMiniMonitor { get; private set; }
public bool ShowMemoryInMiniMonitor { get; private set; }
public bool ShowOllamaLogsInMiniMonitor { get; private set; }  // lazy-starts/stops IOllamaLogService
public bool IsLogsPanelExpanded { get; set; }
public ObservableCollection<string> OllamaLogLines { get; }    // max 5 lines, UI-thread safe
```

---

## Binding Summary (MiniMonitorWindow.xaml)

| Control | Binding | Notes |
|---------|---------|-------|
| CpuText TextBlock | `Visibility="{Binding ShowCpuInMiniMonitor, Converter=BoolToVisibilityConverter}"` | Hides row when false |
| MemoryText TextBlock | `Visibility="{Binding ShowMemoryInMiniMonitor, Converter=BoolToVisibilityConverter}"` | Hides row when false |
| Expander | `Visibility="{Binding ShowOllamaLogsInMiniMonitor, ...}"` + `IsExpanded="{Binding IsLogsPanelExpanded}"` | Entire panel hidden when flag false |
| ItemsControl inside Expander | `ItemsSource="{Binding OllamaLogLines}"` | Raw log lines, Consolas 10px |

---

## Live-Apply

All three flags re-read from `AppSettingsService.LoadAsync()` on every `RefreshAsync()` tick (every 2s by default). No app restart required. `ShowOllamaLogsInMiniMonitor` setter lazily starts/stops `IOllamaLogService`.

---

## Notes for Switch (Testing)

- Default: all three flags are `false` → CPU, Memory, and Logs panel are hidden in mini monitor.
- Enable "Show CPU in Mini Monitor" in Settings → CpuText row appears within next refresh (~2s).
- Enable "Show Ollama Logs in Mini Monitor" → Expander appears (collapsed). Expand to see up to 5 raw log lines. Lines update in real-time.
- Disable logs toggle while expanded → entire Expander hides immediately on next refresh.
- Window grows vertically when Expander is expanded; shrinks on collapse (`SizeToContent="Height"`).
- Settings can be saved from either the root SettingsWindow (dark theme, ViewModel-bound) or Windows/SettingsWindow (light theme, code-behind). Both paths persist the 3 new flags.

---

### Decision: Mini Monitor Unit Tests (Slice S6)

**Author:** Switch (Quality Engineer)  
**Date:** 2026-06-28  
**Status:** Done — 34/34 tests passing

---

## Summary

Unit tests added for Slice S6 of the Mini Monitor Optional Display feature. Covers the two testable units: `AppSettings` defaults/deserialization and the `OllamaLogService` ring buffer.

---

## Tests Added

### AppSettingsTests.cs — 4 new tests (file extended)

| Test | Assertion |
|------|-----------|
| `Defaults_ShowCpuInMiniMonitor_IsFalse` | `new AppSettings().ShowCpuInMiniMonitor == false` |
| `Defaults_ShowMemoryInMiniMonitor_IsFalse` | `new AppSettings().ShowMemoryInMiniMonitor == false` |
| `Defaults_ShowOllamaLogsInMiniMonitor_IsFalse` | `new AppSettings().ShowOllamaLogsInMiniMonitor == false` |
| `Deserialization_MissingMiniMonitorKeys_DefaultsToFalse` | JSON payload without the 3 new keys deserializes all three to `false` via `System.Text.Json` (camelCase policy, matching `AppSettingsService`) |

### OllamaLogServiceTests.cs — 11 new tests (new file)

| Test | Assertion |
|------|-----------|
| `RecentLines_IsEmpty_OnCreation` | Service starts with empty buffer |
| `RingBuffer_SingleLine_ContainsThatLine` | One line in → one line out |
| `RingBuffer_FiveLines_ContainsAllFive` | Exactly 5 lines fit |
| `RingBuffer_ExceedsCapacity_RetainsLastFiveOnly` | 7 lines in → 5 lines out |
| `RingBuffer_ExceedsCapacity_OldestLinesDropped` | Lines 1 and 2 evicted when 7 fed |
| `RingBuffer_ExceedsCapacity_PreservesInsertionOrder` | Surviving lines are `["line3".."line7"]` in order |
| `LogLineReceived_FiredForEachLine` | Event fires once per `OnOwnedProcessOutput` call |
| `LogLineReceived_FiredWithCorrectLineValue` | Event payload equals the appended line |
| `OnOwnedProcessOutput_NullLine_IsIgnored` | `null` is silently dropped |
| `OnOwnedProcessOutput_EmptyLine_IsIgnored` | `""` is silently dropped |
| `RecentLines_ReturnsSnapshot_NotLiveReference` | Snapshot taken before a second append is unaffected by subsequent writes |
| `LogLineReceived_ExceptionInHandler_DoesNotPropagateToAppendLine` | Bad subscriber exception is caught; line is still appended |

---

## Testability Seam

**Seam chosen:** `internal void OnOwnedProcessOutput(string? line)` on `OllamaLogService`.

- This is the same codepath exercised at runtime when `OllamaCliService` owns the process and forwards stdout/stderr lines. It directly calls `AppendLine`, exercising the ring buffer and event dispatch.
- **No file I/O, no timer, no real Ollama process** required.
- Exposed via `[assembly: InternalsVisibleTo("ElBruno.OllamaMonitor.Tests")]` added to `src/ElBruno.OllamaMonitor/AssemblyInfo.cs`. Runtime behavior and public surface are unchanged.

---

## Final Test Count

```
Passed: 34  |  Failed: 0  |  Skipped: 0
```

Run command:
```
dotnet test tests/ElBruno.OllamaMonitor.Tests/ElBruno.OllamaMonitor.Tests.csproj -c Debug --nologo
```

---

## Gaps — Not Unit-Testable

| Area | Reason | Coverage Path |
|------|--------|---------------|
| `MainWindowViewModel` visibility bindings (`ShowCpuInMiniMonitor`, `ShowMemoryInMiniMonitor`, `ShowOllamaLogsInMiniMonitor`) | Requires WPF dispatcher; no MVVM test harness present | Manual smoke test (Trinity notes) |
| `MiniMonitorWindow.xaml` Visibility/SizeToContent behavior | WPF window activation required | Manual smoke test |
| Settings persistence round-trip via `AppSettingsService.SaveAsync`/`LoadAsync` | `AppPaths.SettingsFilePath` writes to `%LOCALAPPDATA%`; path not injectable | JSON deserialization test covers the `System.Text.Json` defaults path; file I/O path relies on existing manual smoke |
| `OllamaLogService` file-tail polling (Mode 2 — external Ollama) | Log file path hard-coded; poller fires on 2s timer | Not unit-tested; timer-based polling is out of scope per task constraints |

---

## Acceptance Criteria Coverage

- ✅ `ShowCpuInMiniMonitor` defaults to `false`
- ✅ `ShowMemoryInMiniMonitor` defaults to `false`
- ✅ `ShowOllamaLogsInMiniMonitor` defaults to `false`
- ✅ Missing JSON keys → defaults (legacy settings file safe)
- ✅ Ring buffer capped at 5 lines
- ✅ Oldest lines dropped when cap exceeded
- ✅ Insertion order preserved
- ✅ `LogLineReceived` fires per line
- ✅ Null/empty lines silently ignored
- ✅ Bad subscriber exception does not corrupt the ring buffer

---

### Decision: Mini Monitor Optional Display Documentation (S7)

**Author:** Morpheus (Documentation Lead)  
**Date:** 2026-06-28  
**Status:** Complete — docs updated and verified

---

## Summary

Slice S7 documentation updates for the three new mini-monitor optional display options implemented by Tank (S1, S4) and Trinity (S2, S3, S5).

---

## Changes Made

### 1. docs/configuration.md

#### Default Configuration JSON
- Added three new settings with `false` defaults:
  - `showCpuInMiniMonitor`
  - `showMemoryInMiniMonitor`
  - `showOllamaLogsInMiniMonitor`

#### Settings Reference Section
Added complete documentation for each new setting:

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `showCpuInMiniMonitor` | bool | false | Display CPU metrics in Mini Monitor |
| `showMemoryInMiniMonitor` | bool | false | Display memory metrics in Mini Monitor |
| `showOllamaLogsInMiniMonitor` | bool | false | Display collapsible logs panel (last 5 lines) |

**Key details documented:**
- Live-apply design (no restart required)
- Changes visible on next refresh cycle (~2 seconds)
- Logs panel hybrid source behavior:
  - Redirected stdout/stderr when app starts Ollama
  - Tails `%USERPROFILE%\.ollama\logs\server.log` for external Ollama instances

### 2. docs/release-notes.md

#### New Version Entry: 0.10.0
- **Release Date:** 2026-06-28
- **Type:** Minor feature release

#### Feature Descriptions
- ✅ Optional Show CPU in Mini Monitor (disabled by default)
- ✅ Optional Show Memory in Mini Monitor (disabled by default)
- ✅ Optional Show Ollama logs in Mini Monitor with collapsible panel
- ✅ Hybrid log source (process redirect + file tail)
- ✅ Live-apply behavior (no restart)
- ✅ Monospace Consolas font for logs readability

#### Support Timeline Updated
- 0.10.0 now listed as Active (2026-06-28)
- Older versions shifted to Shipped status

### 3. README.md

#### "What's New" Section Updated
- Highlighted mini-monitor display options at the top
- Emphasized live-apply behavior
- Mentioned collapsible logs panel with 5-line limit
- Kept other features intact (model lifecycle, unload strategy, etc.)

---

## Documentation Philosophy Applied

**Consistent with Morpheus learnings:**
- Practical, developer-focused tone
- Avoided jargon; explained hybrid log source clearly
- Emphasized user-facing benefits (live-apply, real-time updates)
- All three settings documented as "disabled by default" to set expectations
- Included concrete paths (`%USERPROFILE%\.ollama\logs\server.log`)
- Links to Configuration Guide for deeper details

---

## Files Modified

| File | Changes |
|------|---------|
| `docs/configuration.md` | +5 new settings sections; updated Default Configuration JSON |
| `docs/release-notes.md` | +v0.10.0 release entry; updated Support Timeline |
| `README.md` | Updated "What's New" section |
| `.squad/agents/morpheus/history.md` | Appended learnings entry |

---

## Verification

All documentation changes verified:
- ✅ Settings keys match AppSettings.cs (Tank's S1 decision)
- ✅ Hybrid log source behavior documented per Tank's IOllamaLogService (S4 decision)
- ✅ UI descriptions match Trinity's XAML/VM bindings (S2, S3, S5 decisions)
- ✅ Live-apply behavior documented (Trinity's RefreshAsync integration)
- ✅ Release date matches current_datetime (2026-06-28)
- ✅ Tone consistent with existing Morpheus documentation standards
- ✅ No invented features; only documented implemented behavior

---

## Handoff Notes

**For elbruno (Project Lead):**
- Review release notes entry for tone/accuracy
- Verify version number (0.10.0 is next after current 0.8.1)
- Confirm release date (2026-06-28)

**For future releases:**
- Template established for "What's New" + release notes + settings documentation
- Configuration documentation pattern proven; reuse for future settings
- Live-apply behavior should be highlighted in docs for any similar features

---

## Next Steps

- ✅ Documentation complete
- Ready for Team validation (Switch/Neo review)
- Ready for public release

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
