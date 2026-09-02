using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ItsAlways710.OllamaMonitor.Configuration;
using ItsAlways710.OllamaMonitor.Diagnostics;
using ItsAlways710.OllamaMonitor.Interop;

namespace ItsAlways710.OllamaMonitor;

public partial class MiniMonitorWindow : Window
{
    private static readonly TimeSpan PositionSaveDebounce = TimeSpan.FromMilliseconds(750);

    private static readonly TimeSpan TopmostWatchdogInterval = TimeSpan.FromMilliseconds(500);

    private readonly AppSettingsService? _settingsService;
    private readonly DiagnosticsLogService? _diagnostics;
    private readonly DispatcherTimer _topmostWatchdog;
    private bool _allowClose;
    private bool _positionPendingSave;
    private DateTime _lastSizeChangeUtc;
    private DispatcherTimer? _positionSaveTimer;

    public MiniMonitorWindow() : this(null, null)
    {
    }

    /// <summary>
    /// Creates the Mini Monitor window. Both services are optional so the window can be
    /// constructed without application infrastructure; topmost defense and position
    /// saving degrade gracefully when they are absent.
    /// </summary>
    public MiniMonitorWindow(AppSettingsService? settingsService, DiagnosticsLogService? diagnostics)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _diagnostics = diagnostics;
        LocationChanged += OnWindowLocationChanged;
        IsVisibleChanged += OnWindowIsVisibleChanged;
        Deactivated += OnWindowDeactivated;
        SizeChanged += OnWindowSizeChanged;

        // While the window is visible, poll the WS_EX_TOPMOST bit. This is the layer that
        // catches removals made via SetWindowLong(GWL_EXSTYLE) - those never surface an
        // interceptable window message, so the WM_WINDOWPOSCHANGING guard cannot see them.
        // Cost: one syscall per tick for a tray-resident window.
        _topmostWatchdog = new DispatcherTimer { Interval = TopmostWatchdogInterval };
        _topmostWatchdog.Tick += (_, _) => EnforceTopmost("watchdog");
        _topmostWatchdog.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // While this window is supposed to stay topmost, intercept any attempt by any
        // process (shell, window manager, ...) to move it out of the topmost band, and
        // rewrite the WINDOWPOS so it lands at HWND_TOPMOST instead. WPF's own moves use
        // HWND_TOP with SWP_NOZORDER and are not affected.
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProcTopmostGuard);
    }

    private IntPtr WndProcTopmostGuard(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowInterop.WmWindowPosChanging && !handled && Topmost)
        {
            long insertAfter = Marshal.ReadInt64(lParam, WindowInterop.WindowPosInsertAfterOffset);
            if (TopmostGuardPolicy.IsDemotingPosition(insertAfter))
            {
                uint flags = (uint)Marshal.ReadInt32(lParam, WindowInterop.WindowPosFlagsOffset);
                LogTopmostEvent("guard-rewrite", WindowInterop.GetExtendedStyle(hwnd), insertAfter, flags);

                // HWND_TOPMOST in WindowPOS.hwndInsertAfter only applies when the Z order is
                // actually changed: clear SWP_NOZORDER so the rewrite takes effect.
                if ((flags & WindowInterop.SwpNoZOrder) != 0)
                {
                    Marshal.WriteInt32(lParam, WindowInterop.WindowPosFlagsOffset, (int)(flags & ~WindowInterop.SwpNoZOrder));
                }

                Marshal.WriteInt64(lParam, WindowInterop.WindowPosInsertAfterOffset, TopmostGuardPolicy.HwndTopmost);
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// True when a window at (left, top) with the given size overlaps the virtual screen
    /// area by at least minimumVisibleExtent in both dimensions - i.e. it is actually
    /// reachable. Used to reject a saved position once all monitors that covered it are
    /// gone (disconnected) or the resolution shrank, so the window never starts invisible.
    /// </summary>
    public static bool IsPositionScreenVisible(double left, double top, double width, double height,
        double screenLeft, double screenTop, double screenWidth, double screenHeight,
        double minimumVisibleExtent)
    {
        var overlapWidth = Math.Min(left + width, screenLeft + screenWidth) - Math.Max(left, screenLeft);
        var overlapHeight = Math.Min(top + height, screenTop + screenHeight) - Math.Max(top, screenTop);
        return overlapWidth >= minimumVisibleExtent && overlapHeight >= minimumVisibleExtent;
    }

    public void PrepareForExit() => _allowClose = true;

    private void OnWindowLocationChanged(object? sender, EventArgs e)
    {
        if (_settingsService is null || !IsVisible)
        {
            return;
        }

        _positionPendingSave = true;
        if (_positionSaveTimer is null)
        {
            _positionSaveTimer = new DispatcherTimer { Interval = PositionSaveDebounce };
            _positionSaveTimer.Tick += (_, _) =>
            {
                _positionSaveTimer!.Stop();
                SavePosition();
            };
        }

        _positionSaveTimer.Stop();
        _positionSaveTimer.Start();
    }

    private void OnWindowIsVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        // Fires when Hide() takes effect - both the X button and the tray toggle route
        // through it - so the pending position is flushed before the window goes away.
        if (!IsVisible)
        {
            FlushPositionSave();
            return;
        }

        // A hide/show cycle can come back out of the topmost band (or, worst case, the bit
        // was never applied): re-assert and record whether the bit actually landed.
        EnforceTopmost("shown");
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // Losing focus (e.g. the user clicked another application) is the moment an
        // external actor typically demotes topmost windows - re-assert with zero delay.
        EnforceTopmost("deactivated");
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Track the last self-resize (SizeToContent=Height on a refresh tick) so a demotion
        // log can show whether it happened right after one - useful to attribute the event
        // to a window-tracking tool reacting to our resizes.
        _lastSizeChangeUtc = DateTime.UtcNow;
    }

    private void EnforceTopmost(string trigger)
    {
        if (!IsVisible || !Topmost)
        {
            return;
        }

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int exstyle = WindowInterop.GetExtendedStyle(hwnd);
        bool hasTopmost = (exstyle & WindowInterop.WsExTopmost) != 0;
        if (!hasTopmost)
        {
            _diagnostics?.WriteWarning(
                $"MiniMonitor topmost re-asserted [trigger={trigger}]: WS_EX_TOPMOST was missing; " +
                $"GWL_EXSTYLE=0x{exstyle:X}");

            if (_diagnostics is { IsVerboseEnabled: true })
            {
                _diagnostics.WriteVerbose(
                    $"MiniMonitor topmost re-asserted (forensics) [trigger={trigger}]: " +
                    $"foreground={WindowInterop.DescribeForegroundWindow()}; msSinceLastSizeChange={MsSinceLastSizeChange()}");
            }
            WindowInterop.RestoreTopmost(hwnd);
            return;
        }

        if (trigger != "shown")
        {
            return;
        }

        _diagnostics?.WriteInfo(
            $"MiniMonitor topmost OK [trigger=shown]: WS_EX_TOPMOST present; " +
            $"GWL_EXSTYLE=0x{exstyle:X}");

        if (_diagnostics is { IsVerboseEnabled: true })
        {
            _diagnostics.WriteVerbose(
                $"MiniMonitor topmost OK (forensics) [trigger=shown]: " +
                $"foreground={WindowInterop.DescribeForegroundWindow()}");
        }
    }

    private void LogTopmostEvent(string trigger, int exstyle, long insertAfter, uint flags)
    {
        string topmostState = (exstyle & WindowInterop.WsExTopmost) != 0 ? "present" : "missing";

        _diagnostics?.WriteWarning(
            $"MiniMonitor topmost demotion blocked [trigger={trigger}]: " +
            $"WS_EX_TOPMOST={topmostState}; " +
            $"GWL_EXSTYLE=0x{exstyle:X}; " +
            $"WindowPOS.hwndInsertAfter=0x{insertAfter:X}; " +
            $"flags=0x{flags:X8}");

        // Name the demotion's actor in the log itself (reference-window owner pid/
        // process/class/title, sending thread, foreground window) - but only when
        // verbose logging is on: the capture opens foreign processes, and the
        // reference handle is guaranteed valid only in this hook, so the capture and
        // the write share one gate.
        if (_diagnostics is { IsVerboseEnabled: true })
        {
            RefWindowIdentity refWindow = RefWindowForensics.Capture(new IntPtr(insertAfter));
            uint senderThreadId = RefWindowForensics.GetCurrentThreadId();

            _diagnostics.WriteVerbose(
                $"MiniMonitor topmost demotion blocked (forensics) [trigger={trigger}]: " +
                $"refWin={RefWindowForensics.Format(refWindow)}; " +
                $"senderTid=0x{senderThreadId:X}; senderIsOwnProcess={RefWindowForensics.IsOwnProcessThread(senderThreadId)}; " +
                $"foreground={WindowInterop.DescribeForegroundWindow()}; msSinceLastSizeChange={MsSinceLastSizeChange()}");
        }
    }

    private int MsSinceLastSizeChange() =>
        _lastSizeChangeUtc == default ? -1 : (int)Math.Round((DateTime.UtcNow - _lastSizeChangeUtc).TotalMilliseconds);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        FlushPositionSave();
        base.OnClosing(e);
    }

    private void OnWindowDrag(object? sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void FlushPositionSave()
    {
        _positionSaveTimer?.Stop();
        SavePosition();
    }

    private void SavePosition()
    {
        if (_settingsService is null || !_positionPendingSave)
        {
            return;
        }

        try
        {
            _settingsService.UpdateMiniMonitorPosition(Left, Top);
            _positionPendingSave = false;
        }
        catch
        {
            // Best effort: a failed save must not break the window lifecycle; the
            // position stays pending so a later hide/close retries the write.
        }
    }
}
