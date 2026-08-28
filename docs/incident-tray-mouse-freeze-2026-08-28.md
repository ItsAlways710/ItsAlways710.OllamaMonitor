# OPEN ISSUE — system-wide mouse freeze during tray UI automation (2026-08-28)

**Status:** OPEN / unexplained. Logged per user instruction. Do not close without a root-cause explanation.

## Severity

System-wide mouse input froze, severe enough that the user had to force-kill the app
via keyboard-only navigation to recover. The taskbar overflow chevron showed as
"expanded" with an empty, non-rendering flyout; subsequent direct user clicks in that
area also did nothing.

## What happened

During verification of the 2026-08-28 "Show Mini Monitor on start" fix, an ad-hoc
Win32 probe (`C:\Temp\elbruno-probe`, since removed from any repo) attempted to drive
the Win11 taskbar tray because the overflow flyout did not expose the app's icon to
UI Automation. The probe used raw `SetCursorPos` + `mouse_event` (LEFTDOWN/UP,
RIGHTDOWN/UP) at coordinates derived from window rects while the probe was
DPI-unaware; system DPI was 150%, so those coordinates were DPI-virtualized and did
not match physical positions. A later probe version called
`SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)` but the freeze persisted.

## Working hypothesis (UNCONFIRMED)

A `mouse_event` button-down landed inside the shell flyout while the flyout was
torn-down or in a different coordinate space, leaving a button-press captured by the
shell (context-menu / flyout mouse capture holds system-wide input until released).
The matching button-up never reached a valid target, so the capture state never
cleared. This is consistent with the symptoms (frozen mouse, chevron stuck in
"expanded" visual state, user clicks ignored). It has **not** been proven — the
probe is gone, the shell state was reset by process kill/restart, and there is no
post-hoc way to inspect the capture state at the time.

## Rules going forward

1. No raw `mouse_event`/`SetCursorPos` automation against the Win11 taskbar flyout or
   tray area. It is non-idempotent and can leave the shell in an unrecoverable mouse-capture
   state.
2. Prefer in-process, passive verification (e.g. the harness in `C:\Temp\details-harness`,
   which renders the real `MainWindow` and captures it with no input events at all).
3. If any future tray work is unavoidable: use documented shell APIs, never synthetic
   button events in the flyout region, verify `GetCapture` round-trips (down→up in the
   same DPI-aware coordinate space), and have a recovery plan before the first event.
