# Trinity — Project History

## Upcoming: Mini Monitor Optional Display (2026-06-28 planned, pending elbruno sign-off)

**Context:** Neo completed comprehensive implementation plan for 3 user-requested mini monitor improvements.

**Trinity responsibilities:**
- **S2 (Settings UI):** Add toggles to SettingsWindow for `ShowCpuInMiniMonitor`, `ShowMemoryInMiniMonitor`, `ShowOllamaLogsInMiniMonitor`
- **S3 (XAML Visibility):** Bind MiniMonitorWindow TextBlocks/controls to AppSettings flags using `BooleanToVisibilityConverter` (reuse `MainWindowViewModel`)
- **S5 (Logs Panel):** Create collapsible `Expander` control with 5-line log display, populate from Tank's `OllamaLogService`

**Status:** Plan complete. Awaiting elbruno sign-off before implementation begins. Tank handles S1+S4 (settings model, log service).

**Full plan:** `.squad/files/mini-monitor-optional-display-plan.md`

---

## Phase 2c: Settings UX Implementation (2026-04-28)

### Completed: Settings Window Implementation

**Requested by:** Bruno Capuano  
**Status:** ✅ Complete, build verified

#### Implementation Summary

Created WPF Settings Window per Neo's specification (`.squad/decisions/inbox/neo-settings-window-spec.md`). Single-column form displays all 9 AppSettings keys with Endpoint and RefreshIntervalSeconds editable (Phase 2b-ready).

#### Files Created

1. **`src/ElBruno.OllamaMonitor/Windows/SettingsWindow.xaml`** (184 lines)
   - Single-column form layout with 4 sections: Connection, Application Behavior, Metrics Collection, Alert Thresholds
   - Editable fields: Endpoint (TextBox), RefreshIntervalSeconds (TextBox)
   - Read-only fields: 7 remaining AppSettings keys (disabled checkboxes + read-only textboxes with gray background)
   - Inline error labels below editable fields (Visibility=Collapsed by default)
   - Footer warning banner: "⚠ Restart required for changes to take effect" (yellow background, border)
   - Buttons: Reset to Defaults, Cancel, Save (blue primary button, IsDefault=True)
   - Window properties: 500×580px, resizable, icon = tray-green.ico, ShowInTaskbar=True

2. **`src/ElBruno.OllamaMonitor/Windows/SettingsWindow.xaml.cs`** (195 lines)
   - Constructor: Takes AppSettingsService dependency, loads settings async on window Loaded event
   - Save handler: Implements last-write-wins pattern (reload from disk → apply edits → validate → save)
   - Validation: Inline ValidateEndpoint and ValidateRefreshInterval methods with TODO markers for Tank's service integration
   - Reset handler: Calls AppSettingsService.ResetAsync, reloads form fields, does NOT auto-close window
   - Cancel handler: Hide window without saving
   - PrepareForExit + OnClosing override: Hide-instead-of-close pattern (mirrors MiniMonitorWindow/MainWindow)

#### Files Modified

3. **`src/ElBruno.OllamaMonitor/Services/TrayIconService.cs`**
   - Added `Action _showSettingsWindow` constructor parameter and field
   - Inserted "Settings…" menu item at line 55 (between "Show Mini Monitor" and "Refresh")
   - Menu click handler invokes `_showSettingsWindow()` delegate

4. **`src/ElBruno.OllamaMonitor/App.xaml.cs`**
   - Added `using ElBruno.OllamaMonitor.Windows;` namespace
   - Added `private SettingsWindow? _settingsWindow;` field
   - Instantiate `_settingsWindow = new SettingsWindow(settingsService)` in LaunchTrayApplicationAsync
   - Pass `ShowSettingsWindow` delegate to TrayIconService constructor
   - Added `public void ShowSettingsWindow()` method: lazy show/activate pattern (mirrors ShowMiniMonitorWindow exactly)
   - OnExit: Call `_settingsWindow?.PrepareForExit()` and `_settingsWindow?.Close()`

#### Technical Implementation Details

**Validation Logic (Tank Integration TODOs):**
- ValidateEndpoint: Empty check, Uri.TryCreate, scheme check (http/https only)
- ValidateRefreshInterval: Range check (1-60 seconds)
- Both methods return `(bool ok, string? error)` tuples
- TODO markers at call sites for Tank's AppSettingsService.ValidateEndpoint/ValidateRefreshInterval replacements
- Validation errors display inline below fields (red text, Visibility=Visible on error)

**Save Flow (Last-Write-Wins Concurrency):**
1. User clicks Save button
2. Validate editable fields (Endpoint + RefreshInterval)
3. **Reload from disk:** `await _settingsService.LoadAsync()` — ensures CLI changes are not overwritten
4. Apply user edits to reloaded object via `with` expression
5. Save to disk: `await _settingsService.SaveAsync(updated)`
6. Close window on success
7. On validation failure: Show inline error, do NOT save, keep window open

**Lifecycle Pattern:**
- Instantiation: Created once in App.xaml.cs LaunchTrayApplicationAsync
- Show/Activate: ShowSettingsWindow() checks IsVisible → Show() → Activate()
- Hide-on-Close: PrepareForExit flag prevents unwanted close during user interaction
- Shutdown: PrepareForExit() called in App.OnExit before Close()

#### Build Verification

✅ **Build status:** Success (0 errors, 0 warnings)  
- Command: `dotnet build ElBruno.OllamaMonitor.sln`  
- Duration: 2.5s  
- Both projects compiled successfully

**Fixed during build:**
- Ambiguous MessageBox reference between System.Windows.Forms.MessageBox and System.Windows.MessageBox
- Resolution: Fully qualified as `System.Windows.MessageBox` (4 occurrences)

#### Phase 2a vs 2b Scope Delivered

**Phase 2a Complete (this session):**
- ✅ Settings window with all 9 fields visible
- ✅ Endpoint + RefreshIntervalSeconds editable with validation
- ✅ 7 remaining fields read-only (checkboxes disabled, textboxes styled gray)
- ✅ Save/Cancel/Reset buttons functional
- ✅ Tray menu "Settings…" entry
- ✅ App.xaml.cs lifecycle management
- ✅ Last-write-wins concurrency handling
- ✅ Inline validation with TODOs for Tank's validators

**Phase 2b enhancements (future):**
- Enable editing of remaining 7 fields (checkboxes, threshold textboxes)
- Change detection (enable Save/Reset only if form dirty)
- Selective reload without restart (stretch goal)

---

## Learnings

### Window-with-Form Pattern
- **Single-column layout:** Use StackPanel inside ScrollViewer for vertical stacking
- **Section headers:** TextBlock with Style (FontSize=14, FontWeight=SemiBold, Margin top/bottom)
- **Field labels:** TextBlock with Style (FontSize=12, gray foreground, margin above field)
- **Error labels:** TextBlock below field, red text, FontSize=11, Visibility=Collapsed by default
- **Footer warning banner:** Border with yellow background (#FFFFFBEB), border (#FFFBBF24), CornerRadius=4, icon + text in StackPanel
- **Button panel:** StackPanel with HorizontalAlignment=Right, buttons in order: secondary actions left (Reset), tertiary (Cancel), primary right (Save)
- **Primary button styling:** Background=#FF3B82F6 (blue), Foreground=White, IsDefault=True (Enter key triggers Save)

### Validation Call-Site Pattern
- **Inline validation methods:** Keep validation logic in code-behind with TODO markers for future service integration
- **Return tuples:** `(bool ok, string? error)` for clean error handling without exceptions
- **Validation timing:** On Save button click (not on field blur in Phase 2a — simpler implementation)
- **Error display:** Show error inline below field, block save, keep window open on validation failure
- **Error clearing:** Clear all errors at start of Save handler to reset state

### Last-Write-Wins Concurrency Pattern
- **Always reload before save:** `LoadAsync()` then apply edits then `SaveAsync()` prevents overwriting concurrent CLI changes
- **Use immutable records:** `with` expression for clean updates without mutation
- **Document in troubleshooting.md:** Advise users to avoid concurrent CLI + GUI writes in same 5-second window (rare edge case)

### Tray Menu Insertion Specifics
- **Insertion point:** Between related items (Settings after windows, before actions)
- **Menu item text:** Use ellipsis (…) to indicate modal dialogs (Windows standard)
- **Coupling strategy:** Pass Action delegate to TrayIconService to avoid tight coupling to App singleton
- **Menu item ordering:** Windows (Details, Mini Monitor), Settings, Actions (Refresh, Copy, Open), About (HomePage), Exit

### XAML Resource Styles
- **Reusable styles:** Define in Window.Resources for consistency (SectionHeaderStyle, FieldLabelStyle, EditableFieldStyle, ReadOnlyFieldStyle)
- **Read-only field styling:** Gray background (#FFF9FAFB), gray border (#FFE5E7EB), IsReadOnly=True
- **Editable field styling:** White background (default), darker border (#FFD1D5DB), Padding=8

---

## Phase 2c: Settings UX Implementation (2026-04-28 planned)

### Upcoming Ownership: Trinity Window/UI/Menu Wiring

**Context:** Neo completed Settings UX architecture analysis. Recommendation approved: do both tray menu entry + dedicated Settings form, phased across 2a/2b.

**Trinity responsibilities (Phase 2a/2b):**
- Add ToolStripMenuItem("Settings…") to TrayIconService.cs (before "Open Config Folder")
- Create SettingsWindow.xaml (Phase 2a: read-only display; Phase 2b: editable Tier 1 fields)
- Implement ShowSettingsWindow() in App.xaml.cs with single-instance semantics
- Handle window lifecycle (show/hide, shutdown integration)
- Async Save handler coordination (Phase 2a: no-op spinner; Phase 2b: call Tank validation methods)
- Display layout: Tier 1 (Endpoint, RefreshIntervalSeconds) + Tier 2 (GPU/Disk flags, thresholds)

**Tank/Switch responsibilities:** Validation logic extension, build/smoke test verification.

**Decision file:** `.squad/decisions.md` (Settings UX Architecture section).

---

## Phase 2a: Quick Wins Implementation (2026-04-26)

### Completed Tasks

#### T2.1: Mini Monitor — Move "Always on Top" from header to status bar
- **Change:** Removed TextBlock with "Always on top" text from header (Row 0 StackPanel)
- **Result:** Freed ~25px of vertical space in mini window header
- **Implementation:** Added footer Grid (Row 3) with 2-column layout to display both "Always on top" and "Last checked" text
- **Status:** ✅ Complete, verified visually in XAML

#### T2.2: Mini Monitor — Increase model label font size (12px → 14px)
- **Change:** Added explicit `FontSize="14"` to model display TextBlock
- **Result:** Improved readability of model names in the mini monitor
- **Verification:** Window still fits 3 models at 310px height (no regression)
- **Status:** ✅ Complete

#### T2.3: Add app icon to windows (Mini Monitor + Details Window)
- **Asset:** Used existing `Assets/TrayIcons/tray-green.ico` (green Ollama logo, 16×16 px)
- **Implementation:** Added `Icon="Assets/TrayIcons/tray-green.ico"` to both:
  - MiniMonitorWindow.xaml
  - MainWindow.xaml (details window)
- **Result:** Icons now appear in both window title bars (visible in taskbar and window chrome)
- **Status:** ✅ Complete

#### T2.4: Implement in-use model status indicator (green bullet)
- **New Model Property:** Added `IsActive` computed property to OllamaModelSnapshot
  - Logic: `ExpiresAt == null || DateTime.UtcNow < ExpiresAt.Value.UtcDateTime`
  - Indicates model is currently loaded/active
- **Value Converter:** Created `BoolToColorConverter` in Helpers/
  - Maps `IsActive: true` → Green brush (RGB 34, 197, 94)
  - Maps `IsActive: false` → Gray brush (RGB 107, 114, 128)
  - Returns SolidColorBrush (not Color) to bind to TextBlock.Foreground
- **UI Implementation:** Replaced simple CompactModelsText display with ItemsControl
  - Each model renders as: `● [ModelName]` where bullet color reflects IsActive state
  - Font size: 14px (matches T2.2 increase)
  - Margin: 2px between items
- **Architecture Note:** Used Run elements in TextBlock.Inlines to apply converter to bullet while keeping model name plain text
- **Status:** ✅ Complete, build verified

### Technical Learnings

1. **XAML namespace gotchas:** ItemsControl with DataTemplate binding requires proper xmlns:local declaration for converters
2. **Ambiguous Color references:** System.Drawing.Color vs System.Windows.Media.Color causes compilation errors—must fully qualify or alias
3. **WPF binding best practices:**
   - Converters should return Brush types for Foreground binding (not Color)
   - SolidColorBrush instances can be static readonly for performance
4. **Model snapshots vs ViewModel:** Mini monitor receives MainWindowViewModel.Models collection directly—no need to duplicate formatting logic in ViewModel

### Architecture Decisions Made

1. **Status Indicator Design:** Used ItemsControl + DataTemplate instead of ViewModel string formatting
   - Advantage: Reusable, clean separation of concerns
   - Keeps CompactModelsText available for other uses (details window, clipboard)
   
2. **Converter Strategy:** Chose SolidColorBrush approach for performance and clarity
   - Static readonly instances avoid GC pressure on repeated binding evaluation

### Files Modified

- `src/ElBruno.OllamaMonitor/MiniMonitorWindow.xaml`
  - Restructured Row 0 header (removed nested StackPanel)
  - Updated Row 3 footer to Grid layout
  - Replaced CompactModelsText TextBlock with ItemsControl
  - Added local namespace for converter access
  
- `src/ElBruno.OllamaMonitor/MainWindow.xaml`
  - Added window icon attribute

- `src/ElBruno.OllamaMonitor/Models/OllamaModelSnapshot.cs`
  - Added IsActive property

- `src/ElBruno.OllamaMonitor/Helpers/BoolToColorConverter.cs` (new file)
  - Implements IValueConverter for bool → Brush conversion

### Testing Checklist (All Passed ✅)

- ✅ Build succeeds: `dotnet build` (0 errors)
- ✅ No console errors
- ✅ Window dimensions reasonable (~310px height for mini monitor)
- ✅ Text legible with font size increase
- ✅ Icons present in window title bars
- ✅ Footer layout preserves "Always on top" and "Last checked" labels

### Next Steps (Phase 2b)

- Build history buffer for sparklines
- Implement Canvas-based usage charts
- Polish details window styling
- Test rendering performance with 3+ models

### No Blockers Encountered

All tasks completed successfully with no architectural constraints or dependencies discovered. Ready for Phase 2b chart implementation.

---

## Screenshot Capture Session (2026-04-25 14:16)

### Environment

- **Ollama Version:** 0.21.2
- **Ollama Status:** Running on localhost:11434
- **Build Configuration:** Debug (net10.0-windows)
- **Runtime Status:** Application launched and captured successfully

### Available Models

| Model | Size (GB) |
|-------|-----------|
| deepseek-coder-v2:16b | 8.9 |
| gemma4:latest | 9.6 |
| qwen2.5-coder:latest | 4.7 |
| gpt-oss:20b | 13.8 |
| gemma4:e2b | 7.2 |
| llama3.2:latest | 2.0 |
| phi4-mini:latest | 2.5 |
| ministral-3:latest | 6.0 |
| deepseek-r1:latest | 5.2 |
| devstral-small-2:latest | 15.2 |
| gpt-oss:120b | 65.4 |

### Screenshots Captured

| Filename | Size | Description |
|----------|------|-------------|
| `mini-window.png` | 140.98 KB | Compact tray monitor view (captured 14:16:54) |
| `main-window.png` | 145.63 KB | Main expanded window showing model and metrics (captured 14:16:36) |
| `dashboard-metrics.png` | 145.35 KB | Dashboard view with additional metrics state (captured 14:16:38) |

**Location:** `docs/assets/`

### Notes

- All screenshots captured at full desktop resolution (1920×1080)
- Application running with ~130 MB memory footprint
- No errors or issues encountered during capture
- Models were populated and ready for monitoring
- Window icons successfully displaying in title bars (green Ollama icon)

---

## Tray Double-Click Behavior Update (2026-04-26)

### Task: Update default tray icon double-click action

**Requested by:** Bruno Capuano

**Objective:** Change the system tray icon double-click behavior to open the Mini Monitor window instead of the Details (Main) window.

### Implementation

**File Modified:** `src/ElBruno.OllamaMonitor/Services/TrayIconService.cs`

**Change:** Line 50
- **Before:** `_notifyIcon.DoubleClick += (_, _) => ShowWindow();`
- **After:** `_notifyIcon.DoubleClick += (_, _) => ShowMiniMonitorWindow();`

### Learnings

1. **Tray double-click handler location:** `TrayIconService.cs` line 50 wires the `NotifyIcon.DoubleClick` event
2. **Window launch pattern:** Both `ShowWindow()` (lines 109-122) and `ShowMiniMonitorWindow()` (lines 124-132) follow the same pattern:
   - Check if window is already visible; if not, call `Show()`
   - If minimized, restore to normal state (MainWindow only has this check)
   - Call `Activate()` to bring window to front
3. **No configurable setting:** AppSettings.cs does not contain a tray double-click target setting—behavior is hard-coded in TrayIconService
4. **Context menu remains unchanged:** Right-click tray menu still provides explicit "Show Details" and "Show Mini Monitor" options (lines 40-41, 54-55)

### Build Verification

✅ **Build status:** Success (0 errors, 0 warnings)
- Command: `dotnet build ElBruno.OllamaMonitor.sln`
- Duration: 4.5s
- Output: Both projects compiled successfully

### Manual Verification Instructions

To verify the change:
1. Launch the application (tray icon appears in system tray)
2. **Double-click** the tray icon
3. **Expected result:** MiniMonitorWindow opens (compact view with model list and metrics)
4. **Previous behavior:** MainWindow opened (detailed view)
5. Right-click context menu still provides explicit access to both windows

### Architecture Notes

- TrayIconService maintains references to **both** windows (MainWindow and MiniMonitorWindow)
- The double-click is a shortcut action; explicit window toggles remain available in the context menu
- This aligns with the Phase 2 focus on Mini Monitor as the primary user-facing view

---

## Validation Handoff Integration (2026-04-28)

**Requested by:** Bruno Capuano  
**Task:** Replace inline validation helpers in SettingsWindow with Tank's SettingsValidator static methods

### Changes Made

**File Modified:** `src/ElBruno.OllamaMonitor/Windows/SettingsWindow.xaml.cs`

1. **Validation calls updated (lines 68-86):**
   - Replaced `ValidateEndpoint(endpoint)` → `SettingsValidator.ValidateEndpoint(endpoint)`
   - Replaced `ValidateRefreshInterval(refreshInterval)` → `SettingsValidator.ValidateRefreshInterval(refreshInterval)`
   - Updated tuple field references: `.ok` → `.Ok`, `.error` → `.Error`
   - Removed TODO comments

2. **Inline helper methods deleted (lines 174-196):**
   - Removed private `ValidateEndpoint(string endpoint)` method
   - Removed private `ValidateRefreshInterval(int seconds)` method
   - Removed TODO comment block

### Build Verification

✅ **Build status:** Success (0 errors, 0 warnings)  
- Command: `dotnet build ElBruno.OllamaMonitor.sln`  
- Duration: 1.9s  
- Both projects compiled successfully

### Integration Notes

- Using statement `using ElBruno.OllamaMonitor.Configuration;` already present (line 3)
- SettingsValidator implements same validation rules as inline helpers (endpoint URI + scheme check, 1-60 second interval range)
- Return tuple fields match PascalCase convention (.Ok, .Error) per Tank's implementation
- No functional changes to validation logic—straight swap of implementation location
