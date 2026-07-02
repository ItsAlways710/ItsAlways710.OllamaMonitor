# Neo — History

## Learnings

### 2026-04-28 — Settings Window Design Spec (Phase 2a + 2b)

**Spec issued:** `.squad/decisions/inbox/neo-settings-window-spec.md` — Complete implementation blueprint for Trinity (UI/window lifecycle) and Tank (validation) running in parallel. Open question resolved: RefreshIntervalSeconds changes require restart (v1); live reload deferred to Phase 2b+ as simpler path per Charter.

### 2026-04-28 — Settings UX Architecture Decision

**Analysis requested:** Pros/cons of adding Settings entry to tray context menu + dedicated Settings form.

**Key findings:**
- **Tray menu entry:** Low friction (one menu item), essential for discoverability (Windows standard). Meaningless without Settings form.
- **Settings form:** High ROI (removes CLI dependency), feasible Phase 2 addition (~5–8 story points). Validation, endpoint testing, and restart semantics are mitigatable.
- **Settings precedence:** CLI (`ollamamon config set`) and GUI (form) both write settings.json. Last-write-wins is acceptable; no file locking needed. Require all writers to reload before save (already true in AppSettingsService).
- **Recommendation:** Do both. Phase 2a: menu + read-only viewer. Phase 2b: editable form with validation.
- **Scope:** Tier 1 (Endpoint, RefreshIntervalSeconds) editable Phase 2a. Tier 2 (thresholds, flags) read-only Phase 2a, editable Phase 2b. Cost: ~1–2 PT for 2a, ~3–4 PT for 2b.

**Precedence & concurrency decision:** Last-write-wins is fine. Both writers must reload from disk before saving (standard pattern, no changes needed to AppSettingsService). Document in troubleshooting.md: avoid concurrent CLI + GUI writes.

**Decision file:** `.squad/decisions/inbox/neo-settings-ux-recommendation.md`

### 2026-04-27 — Tray Double-Click Default Updated
- Trinity updated systray icon double-click to open MiniMonitorWindow by default (TrayIconService.cs line 50). Phase 2a quick-win, build verified. Aligns Mini Monitor as primary interface.

### 2026-04-24 — Phase 1 Architecture Contract

- **Environment:** .NET 10.0.203 stable SDK available. Use `net10.0-windows` TFM.
- **PRD location:** `docs/plans/ElBruno.OllamaMonitor.PRD.md` (~1000 lines, 4 phases, only Phase 1 in scope).
- **Repo rules:** All source in `src/`, all docs in `docs/`, root holds only README.md, LICENSE, .sln, .gitignore, .gitattributes.
- **Key packages:** H.NotifyIcon.Wpf for tray, System.Text.Json for Ollama API, no DI container Phase 1.
- **Ownership split:**
  - Tank: Program.cs, Commands/, Ollama/, Metrics/, Config/, Diagnostics/, .csproj
  - Trinity: App.xaml, MainWindow.xaml, Tray/
  - Morpheus: README.md, LICENSE, docs/**
  - Switch: build/pack verification, CLI smoke tests
- **High-risk:** WinExe+PackAsTool console output, H.NotifyIcon.Wpf net10 compat, CPU% two-sample, nvidia-smi spawn cost.
- **Simplifications:** No DI container, no MVVM framework, no unit tests, no logging framework, no settings dialog—all Phase 2+.
- **Config path:** `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json`
- **Decision file:** `.squad/decisions/inbox/neo-phase1-contract.md`

### 2026-04-24 — Phase 1 Complete & Approved

- **Status:** All orchestration logs written, decisions merged, validation complete.
- **Team outcomes:** Tank delivered platform, Trinity delivered WPF integration, Morpheus delivered all documentation, Switch approved for private release.
- **Next: Phase 2** — DI container, MVVM framework, unit tests, logging framework, remote monitoring.

### 2026-06-28 — Mini Monitor Optional Display Plan

**Plan issued:** `.squad/files/mini-monitor-optional-display-plan.md`

**Key findings:**
- **MiniMonitorWindow shares `MainWindowViewModel`** — no separate VM (App.xaml.cs:99 sets DataContext).
- **Settings service:** `Configuration/AppSettingsService.cs` — JSON file, reload-before-save, auto-creates defaults on missing keys.
- **No existing Ollama log source:** `OllamaCliService.StartOllama()` does NOT redirect stdout/stderr. No file tailing or log streaming exists. This is the primary gap for the logs feature.
- **Recommended log approach:** Hybrid `OllamaLogService` (redirect when we own process, tail `%USERPROFILE%\.ollama\logs\server.log` otherwise).
- **Live-apply feasible:** `RefreshAsync()` already loads settings each cycle; new visibility bools can update without restart.
- **Backward compat:** C# record defaults handle missing JSON keys gracefully.

**Decision file:** `.squad/decisions/inbox/neo-mini-monitor-optional-display.md`

### 2025-01-24 — Sparkline Persistence Fix

- **Problem:** Mini monitor window sparklines invisible because model metrics History was lost on each refresh cycle.
- **Root cause:** `MainWindowViewModel.ApplySnapshot()` cleared and rebuilt the Models collection with fresh `OllamaModelSnapshot` instances every 2 seconds, destroying the `History` objects that accumulate metrics over time.
- **Solution:** Implemented persistent model cache (`Dictionary<string, OllamaModelSnapshot>`) in MainWindowViewModel:
  - Added `_modelCache` field to store models by name across refresh cycles
  - Created `GetOrUpdateModel()` helper that reuses existing cached models (preserving History) or creates new ones
  - Refactored `ApplySnapshot()` to populate Models collection from cache instead of creating fresh instances
  - Added automatic cleanup of stale models no longer in API response
- **Key insight:** C# record `with` syntax preserves object references for properties not explicitly overridden, so updating cached models with `existingModel with { ExpiresAt = ... }` keeps the same History instance alive.
- **Verification:** Model loaded with `ollama run llama3.2:latest`, metrics accumulate across 2-second refresh cycles, sparklines render with visible data traces after 10-15 seconds.
- **Files modified:** `src/ElBruno.OllamaMonitor/ViewModels/MainWindowViewModel.cs`

