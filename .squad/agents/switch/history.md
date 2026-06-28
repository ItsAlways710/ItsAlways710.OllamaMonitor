# Switch History

## Upcoming: Mini Monitor Optional Display (2026-06-28 planned, pending elbruno sign-off)

**Context:** Neo completed comprehensive implementation plan for 3 user-requested mini monitor improvements.

**Switch responsibilities:**
- **S6 (Smoke Tests):** Verify CPU/Memory toggles hide/show correctly, logs panel expands/collapses, live setting changes apply without restart

**Status:** Plan complete. Awaiting elbruno sign-off before testing begins. Tank+Trinity handle S1-S5 (settings, UI, logs).

**Full plan:** `.squad/files/mini-monitor-optional-display-plan.md`

---

## Core Context

Phase 1 validation framework established and approved:
- Two-project split packaging pattern (`src\ElBruno.OllamaMonitor` desktop + `src\ElBruno.OllamaMonitor.Tool` shim)
- Build/packaging flow: `build\Pack-Tool.ps1` → `build\Inject-DesktopPayload.ps1`
- CLI/config shared via source linking; settings at `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json`
- Main validation paths: solution build, packaging scripts, tray wiring, acceptance docs
- Bruno's validation preference: strict language, explicit limits, proven key flows only
- Phase 1 doc gate requires root files + 5 technical docs + promotional set
- GitHub Pages deployment requires one-time settings configuration

## Phase 2 Learnings Archive

### Key Patterns (2026-04-25 to 2026-04-28)

**Smoke-Plan-as-Deliverable Pattern:** Comprehensive manual smoke test plans produced as deliverables before/after feature merge (Settings Window: 29 test cases, 8 sections, 15–20 min runtime). Recommendation: Apply to all Phase 2 UI features.

**v0.5.1 Release Readiness:** 8/8 acceptance criteria PASS (sparklines, stability, metrics, tray, version, themes, model indicators, UX). Edge cases validated. 🟢 Private release approved 2026-04-25.

**UI Pattern Learning:** ItemsControl + DataTemplate for model display (Trinity discovery); SolidColorBrush static resources for converter efficiency (avoiding GC pressure).

---

## Learnings

### 2026-04-28 — Smoke-Plan-as-Deliverable Pattern for WPF Features
- Produces **comprehensive manual smoke test plan** as *deliverable* before/after feature merge (not ad-hoc post-implementation)
- Benefits: Clarifies expected behavior early, provides executable QA checklist, documents edge cases/concurrency semantics, captures blocking clarifications
- Structure: Grouped by user workflow (Tray, Fields, Validation, CLI Parity, Regression, Build)
- **Recommendation:** Apply this pattern to all Phase 2 UI features (Mini Monitor, Details, Theme support)
