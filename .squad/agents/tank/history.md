# Tank History

## Upcoming: Mini Monitor Optional Display (2026-06-28 planned, pending elbruno sign-off)

**Context:** Neo completed comprehensive implementation plan for 3 user-requested mini monitor improvements.

**Tank responsibilities:**
- **S1 (Settings Model):** Add `ShowCpuInMiniMonitor`, `ShowMemoryInMiniMonitor`, `ShowOllamaLogsInMiniMonitor` flags to AppSettings (all false by default)
- **S4 (Ollama Log Service):** Create hybrid `OllamaLogService` — redirect stdout when app owns `ollama serve` process, tail log file otherwise (architecture pending elbruno sign-off on 5 questions)

**Status:** Plan complete. Awaiting elbruno sign-off before implementation begins. Trinity handles S2+S3+S5 (Settings UI, XAML visibility, logs panel).

**Full plan:** `.squad/files/mini-monitor-optional-display-plan.md`

---

## Phase 2c: Settings UX Implementation (2026-04-28 planned)

### Upcoming Ownership: Tank Validation/AppSettingsService Extensions

**Context:** Neo completed Settings UX architecture analysis. Recommendation approved: do both tray menu entry + dedicated Settings form, phased across 2a/2b.

**Tank responsibilities (Phase 2a/2b):**
- Extend AppSettingsService with validation logic for Tier 1 editable fields (Endpoint format, RefreshIntervalSeconds range 1–60s)
- Optional: Reachability test for Endpoint (Phase 2b stretch)
- Ensure all Update* methods reload from disk before saving (already implemented; verify no regression)
- Verify no exceptions escape Save handler in Settings form
- Phase 2a: Format validation only; Phase 2b: Optional advanced validation (reachability, selective reload semantics)

**Trinity/Switch responsibilities:** Window UI/menu wiring, build/smoke test verification.

**Decision file:** `.squad/decisions.md` (Settings UX Architecture section).

**Key decision:** Last-write-wins for CLI + GUI settings precedence. Both writers already reload before save. Document in troubleshooting.md.

---

## Learnings

### 2026-04-28 — Settings File Auto-Creation Verification

- Bruno requested: "if no setting is available create one with the default values (the ones that we are using now)"
- **Behavior already present:** `AppSettingsService.LoadAsync()` lines 18-23 already implements this requirement
- When `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json` does not exist, LoadAsync:
  1. Creates `new AppSettings()` with defaults from property initializers (AppSettings.cs lines 5-13)
  2. Calls `SaveAsync()` to write indented JSON to disk (camelCase, WriteIndented=true)
  3. Returns defaults instance
- **No code changes needed** — requirement satisfied since Phase 1 implementation
- JSON deserializer config: System.Text.Json with PropertyNamingPolicy.CamelCase, supports missing keys gracefully (defaults applied via property initializers)
- Build status: ✅ Success (dotnet build ElBruno.OllamaMonitor.sln)
- File/methods verified: `Configuration\AppSettingsService.cs` LoadAsync (lines 14-44), SaveAsync (lines 52-59), AppSettings.cs (lines 3-14)

### 2026-04-28 — Settings Validators Implementation

- Created `Configuration\SettingsValidator.cs` with two pure static validation methods per Neo's spec (section 4)
- `ValidateEndpoint(string endpoint)` — Rejects null/whitespace, requires valid http(s) URL via `Uri.TryCreate`, tolerates trailing slash
- `ValidateRefreshInterval(int seconds)` — Enforces range 1-60 seconds inclusive
- Wired validators into CLI `CliCommandRunner.cs` for both `config set endpoint` and `config set refresh-interval` commands
- **CLI bug fixed:** Previously CLI saved invalid settings without validation (real bug, not hypothetical)
- Validators return errors to console stderr and exit code 1 on validation failure, preventing invalid values from persisting
- **Reload-before-save pattern verified:** `AppSettingsService.UpdateEndpointAsync` and `UpdateRefreshIntervalAsync` already call `LoadAsync()` before saving (lines 68-78) — no changes needed, pattern already correct per Neo's spec section 5
- Trinity handoff ready: validator class location `ElBruno.OllamaMonitor.Configuration.SettingsValidator`, exact method signatures documented in decision file
- Build status: ✅ Success (dotnet build ElBruno.OllamaMonitor.sln)

### 2026-04-27 — Tray Double-Click Default Updated
- Trinity updated systray icon double-click to open MiniMonitorWindow by default (TrayIconService.cs line 50). Phase 2a quick-win, build verified. Aligns Mini Monitor as primary interface.

### Phase 1 uses a two-project packaging split: `src\ElBruno.OllamaMonitor` is the Windows-first .NET 10 WPF desktop app, and `src\ElBruno.OllamaMonitor.Tool` is the `net10.0` global tool shim that owns `ollamamon`.
- .NET SDK tool packaging does not support `UseWPF`, `UseWindowsForms`, or `-windows` TFMs directly, so the tool package is built first and then enriched with the published desktop payload via `build\Pack-Tool.ps1` and `build\Inject-DesktopPayload.ps1`.
- Shared CLI/config source files are linked into the tool project from the desktop project (`AppPaths`, `Cli`, `Configuration`, `Diagnostics`, `Interop`) to keep config behavior aligned across tray and CLI entry points.
- Core Phase 1 service boundaries live under `src\ElBruno.OllamaMonitor\Ollama`, `...\Services`, and `...\Models`; the tray/UI layer is intentionally isolated in `App.xaml.cs`, `TrayIconService`, `MainWindow`, and `ViewModels`.
- GPU metrics are best-effort only: `NvidiaSmiMetricsService` returns friendly unavailable states instead of failing when `nvidia-smi` is missing or unparsable.
- Bruno prefers Windows-first runtime behavior, safe degradation when Ollama/GPU data is unavailable, and explicit configuration commands that keep `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json` as the single source of truth.

### 2026-04-24 — Phase 1 Implementation Complete

- **Orchestration log:** Written to `.squad/orchestration-log/2026-04-24T18-11-14Z-tank.md`
- **Team integration:** Trinity wired WPF, Morpheus documented config paths and design, Switch validated all flows
- **Validation:** Build, packaging, CLI smoke tests all passed
- **Status:** Ready for private release

