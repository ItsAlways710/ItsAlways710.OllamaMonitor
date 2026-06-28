# Morpheus — Documentation Lead History

## Upcoming: Mini Monitor Optional Display (2026-06-28 planned, pending elbruno sign-off)

**Context:** Neo completed comprehensive implementation plan for 3 user-requested mini monitor improvements.

**Morpheus responsibilities:**
- **S7 (Documentation):** Update docs/configuration.md with new AppSettings flags; update docs/troubleshooting.md with guidance on optional display toggles

**Status:** Plan complete. Awaiting elbruno sign-off before documentation updates begin. Tank+Trinity+Switch handle S1-S6 (settings, UI, logs, tests).

**Full plan:** `.squad/files/mini-monitor-optional-display-plan.md`

---

## Session Notes

### 2026-04-28 — Settings Window UI Feature Shipped (Phase 2a + 2b)
- Settings window with tray menu access shipped: SettingsWindow.xaml/.cs displays all 9 AppSettings; Endpoint + RefreshIntervalSeconds editable with validation; tray menu has new "Settings…" item. Build verified. Team will update docs/troubleshooting.md with CLI+GUI concurrency guidance.

### 2026-04-28 — Settings UX Architecture & Troubleshooting Documentation Note

**Context:** Neo completed Settings UX architecture analysis. Recommendation approved: do both tray menu entry + dedicated Settings form.

**Morpheus action item (Phase 2a/2b):**
- Update docs/troubleshooting.md with new section: *"Settings Changed Via CLI and GUI Simultaneously"*
  - Explain last-write-wins behavior
  - Recommend: Avoid editing settings from both interfaces concurrently
  - Note: Both code paths already reload from disk before saving (safe pattern)
  - Example: If CLI writes while GUI save pending, last writer's settings persist

**Decision context:** No file locking Phase 2 (overkill; concurrent writes rare). Both writers reload before save. Documented approach acceptable.

**Documentation philosophy:** Practical, developer-focused, acknowledges edge cases.

---

### 2026-04-27 — Tray Double-Click Default Updated
- Trinity updated systray icon double-click to open MiniMonitorWindow by default (TrayIconService.cs line 50). Phase 2a quick-win, build verified. Aligns Mini Monitor as primary interface. No documentation changes needed (user-facing behavior, no public API impact).

### 2026-04-27 — Q&A: Non-Default Ollama Endpoint Configuration
- **Documentation-worthy Q&A:** User asked how to configure Ollama running on a non-default port (Tank/Platform developer answered). Answer: Use `ollamamon config set endpoint <url>` or edit `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json`. Restart required.
- **Recommendation:** README or docs/configuration.md should include a dedicated section *"Configuring a Non-Default Ollama Endpoint"* with CLI examples. Current docs reference default endpoint but lack clear guidance for custom ports.
- **Priority:** Low (covered in config docs but not prominent); consider for next doc refresh.

### Phase 1 Documentation Completion (2026-04-24)

**Completed deliverables:**
1. ✅ ROOT README.md — Comprehensive quick-start guide with installation, commands, status reference
2. ✅ LICENSE — Placeholder with clear labeling (awaiting concrete license decision)
3. ✅ docs/architecture.md — Full technical architecture, state model, CLI flow, deployment info
4. ✅ docs/configuration.md — All settings explained, CLI commands, remote monitoring, thresholds
5. ✅ docs/development-guide.md — Prerequisites, build/run, folder structure, debugging, testing
6. ✅ docs/troubleshooting.md — 15+ common issues, solutions, advanced debugging tips
7. ✅ docs/release-notes.md — Version 0.1.0 release notes, known limitations, feature roadmap
8. ✅ docs/promotional/blog-post.md — Full ~8-minute blog post, use cases, technical highlights
9. ✅ docs/promotional/linkedin-post.md — 3 variants, hashtags, engagement hooks
10. ✅ docs/promotional/twitter-post.md — 5 tweet variants, thread version, quote responses
11. ✅ docs/promotional/image-prompts.md — 9 detailed AI image prompts with design guidelines
12. ✅ docs/promotional/images/.gitkeep — Directory placeholder for future generated images

**Documentation philosophy:**
- Practical and developer-focused
- Bruno/El Bruno style: friendly, slightly humorous, action-oriented
- Links between docs for easy navigation
- Phase 1 scoped (no Phase 2+ inventions)
- Clear examples and command-line snippets
- Troubleshooting prioritizes common real-world scenarios

**Key decisions documented:**
- 5-color tray icon state model (Gray/Green/Blue/Orange/Red)
- Default refresh interval: 2 seconds
- Configuration file at `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json`
- GPU metrics best-effort (nvidia-smi), graceful degradation
- CLI commands: help, config (show/set/reset)
- No .NET dependency injection for Phase 1 (manual composition)

### 2026-04-24 — Phase 1 Approved & Ready for Release

- **Orchestration log:** Written to `.squad/orchestration-log/2026-04-24T18-11-14Z-morpheus.md`
- **Decisions merged:** All 10 decision items from morpheus-phase1-docs.md consolidated into `.squad/decisions.md`
- **Team validation:** Switch confirmed all docs present and accurate; Neo approved architecture alignment
- **Status:** Ready for private release. License placeholder awaiting team choice.

## Learnings

### Architecture Patterns
- **State-driven UI:** OllamaMonitorState enum drives tray icon color and UX
- **Service aggregation:** OllamaStatusService combines Ollama API, process metrics, GPU metrics
- **Best-effort metrics:** GPU/disk I/O gracefully fall back to "N/A" rather than fail
- **Lazy initialization:** DispatcherTimer + refresh loop avoids polling overhead

### Documentation Structure
- **Root README:** Quick start + links, keep concise (not exhaustive)
- **Deep docs:** Separate architecture/config/dev/troubleshooting for clarity
- **Promotional materials:** Multiple lengths (280-char tweet to 8-min blog post)
- **Image prompts:** Detailed, design-guided, tool-agnostic

### User Preferences Identified
- Minimal footprint (tray app, not web dashboard)
- Visual feedback (color-coded icon)
- Scriptable (CLI for automation)
- Self-documenting (local JSON config file)
- Works offline (Ollama unreachable → gray icon, continues running)

### Key File Paths to Remember
- Settings: `%LOCALAPPDATA%\ElBruno\OllamaMonitor\settings.json`
- Logs: `%LOCALAPPDATA%\ElBruno\OllamaMonitor\logs\`
- Source: `src/ElBruno.OllamaMonitor/`
- Docs: `docs/` (root level)
- Promotional: `docs/promotional/`

### Implementation Notes
- Project uses two separate solutions: ElBruno.OllamaMonitor and ElBruno.OllamaMonitor.Tool
- WPF + System Tray integration (H.NotifyIcon.Wpf library)
- CLI routing handled by simple string matching, no third-party CLI framework
- App.xaml.cs is the entry point with startup logic

### Tone & Style
- Practical, friendly, slightly irreverent
- Avoid corporate speak
- Use examples (not just theory)
- Acknowledge limitations (GPU best-effort, no remote in Phase 1)
- Focus on solving real problems for local AI developers

## Next Phase Considerations
- Documentation patterns establish foundation for Phase 2 (MVVM, tests, logging framework)
- Troubleshooting guide can be expanded as real-world issues are discovered
- Promotional materials ready for social media launch
- Image prompts designed for modern AI image tools (DALL-E 3, Midjourney, etc.)
- Release notes template in place for future versions

### GitHub Pages Landing Page (2026-04-24)

**Completed deliverables:**
1. ✅ docs/index.html — Professional single-file GitHub Pages landing page
2. ✅ docs/README.md — Documentation hub with navigation and quick links

**Design decisions:**
- **Single-file HTML with inline CSS:** No external dependencies, fast load, future-proof for GitHub Pages
- **Responsive layout:** Mobile-first, flexbox-based grid for features section, collapsible nav on small screens
- **Color scheme:** Blue primary (#2563eb) with professional grays, supports both light and dark modes via CSS media queries
- **Typography:** System fonts (-apple-system, Segoe UI, etc.) for fast rendering and platform consistency
- **Accessibility:** Good contrast ratios (WCAG AA), semantic HTML, alt text on images, readable line-height (1.6)

**Content structure:**
1. **Hero:** Title, tagline, value prop, badges (.NET 10+, Windows 10+, Real-time), dual CTAs (Get Started, GitHub)
2. **Demo:** Embedded gif (ollamanitor-demo01.gif) with caption
3. **Features:** 6-card grid with icons and descriptions (status, model visibility, metrics, tray, lightweight, CLI)
4. **Installation:** 3 methods (NuGet, source, requirements), system prerequisites listed clearly
5. **Usage Guide:** Startup, tray icon colors table, config commands, dashboard interpretation
6. **Documentation Links:** 6-link grid pointing to configuration, architecture, dev, troubleshooting, publishing, release-notes
7. **Footer:** Links to GitHub, NuGet, license, author

**Asset decisions:**
- Logo: Used package-icon.png from src/ElBruno.OllamaMonitor.Tool/assets/
- Demo GIF: Referenced ../images/ollamanitor-demo01.gif (relative path from docs/)
- No external images needed; placeholder structure for future screenshots at docs/assets/

**Testing approach:**
- Validation: Opened locally via file:// protocol (no server required)
- Responsive: Tested layout at 1200px, 768px, 480px breakpoints
- Accessibility: Verified contrast, semantic structure, alt text present
- Graceful degradation: Image missing → alt text shown, CSS-only styling (no JS required)

**Technical highlights:**
- Zero JavaScript required (progressive enhancement approach)
- Sticky header with backdrop blur for visual polish
- CSS custom properties for theme colors (dark mode ready via prefers-color-scheme)
- Mobile-optimized: Stack buttons vertically, reduce fonts, compress spacing
- Fast rendering: inline CSS avoids render-blocking requests
- SEO-ready: meta description, semantic HTML (h1/h2, ul/li, section tags)

**Navigation structure:**
- Header nav links to: Features, Install, Usage, Docs, GitHub
- All internal links relative paths (no hardcoded domains)
- External links open in new tab (GitHub, NuGet, Ollama)
- Documentation grid provides quick access to 6 core guides

**Tone & style:**
- Friendly, approachable, developer-focused
- Emphasizes speed and ease of use
- Technical details balanced with "why" (e.g., "lightweight, always-on monitoring")
- Emoji used sparingly for visual interest (not excessive)

---

**Handoff status:** Documentation Phase 1 complete. Landing page added for GitHub Pages deployment. Ready for team review and public launch.

## Phase 2 Documentation Cleanup (2026-04-25)

**Audit findings:**
- ✅ System Tray Status section (lines 98-110) verified: Uses actual .ico files (tray-gray, tray-green, tray-blue, tray-orange, tray-red), descriptions accurate
- ✅ Configuration section moved: Consolidated to one-liner pointing to docs/configuration.md
- ✅ Quick Start simplified: Removed config commands, kept only: launch → check status → optional configure
- ✅ Promotional content reorganized: Moved from separate section to subsection under Documentation

**Changes made to README.md:**
1. **Quick Start (lines 54-66):** Reduced from ~40 lines to 3 core steps
   - Launch app
   - Check tray icon color
   - Link to Configuration Guide for detailed setup
2. **Configuration (line 104-106):** Condensed from ~25 lines JSON example + explanations to 1-line reference
3. **Documentation section (lines 108-123):** Reorganized promotional materials as subsection ("If you'd like to share this project:")
   - Kept all links functional (relative paths)
   - Better visual hierarchy

**Consolidation results:**
- README reduced from 195 → 144 lines (26% reduction)
- Improved scannability: Users see big picture fast, find details in dedicated docs
- Configuration Guide (docs/configuration.md) remains comprehensive: file location, all options, CLI commands, troubleshooting

**Quality assurance:**
- ✅ All internal links tested (relative paths to docs/)
- ✅ Emoji status table verified against actual tray icon files
- ✅ No information lost (just reorganized)
- ✅ Consistent with team style guide (friendly, practical, developer-focused)
