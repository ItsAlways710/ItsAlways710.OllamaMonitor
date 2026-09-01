using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ItsAlways710.OllamaMonitor.Interop;

/// <summary>
/// Minimal user32 surface for the Mini Monitor topmost defense: read and restore the
/// WS_EX_TOPMOST extended-style bit, and inspect the foreground window at the moment of
/// a demotion event for forensic logging.
/// </summary>
internal static class WindowInterop
{
    internal const int WmWindowPosChanging = 0x0046;

    internal const int GwlExStyle = -20;

    internal const int WsExTopmost = 0x00000008;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpNoOwnerZOrder = 0x0200;

    internal const int DwmWaTitle = 31;

    /// <summary>
    /// Offsets into the WINDOWPOS structure: hwnd, hwndInsertAfter (one IntPtr each),
    /// then x, y, cx, cy (four 32-bit ints), then flags.
    /// </summary>
    internal static readonly int WindowPosInsertAfterOffset = IntPtr.Size;
    internal static readonly int WindowPosFlagsOffset = IntPtr.Size * 2 + 4 * sizeof(int);

    private static readonly IntPtr HwndTopmost = new(TopmostGuardPolicy.HwndTopmost);

    /// <summary>True when the window's extended style currently carries WS_EX_TOPMOST.</summary>
    internal static bool HasTopmostBit(IntPtr hwnd) =>
        (GetExtendedStyle(hwnd) & WsExTopmost) != 0;

    /// <summary>
    /// Re-assert the topmost band per MSDN ("a window can be made a topmost window by
    /// setting hWndInsertAfter to HWND_TOPMOST"): position unchanged, no z-order shuffle
    /// of other windows, no activation.
    /// </summary>
    internal static void RestoreTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0,
            SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZOrder);
    }

    /// <summary>Read the full GWL_EXSTYLE word (0 on COM/interop failure).</summary>
    internal static int GetExtendedStyle(IntPtr hwnd)
    {
        try
        {
            return IntPtr.Size == 8
                ? (int)GetWindowLongPtrW(hwnd, GwlExStyle).ToInt64()
                : GetWindowLongW(hwnd, GwlExStyle);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Best-effort description of the foreground window at the moment of a call: handle,
    /// process id and process name. Used to attribute a topmost demotion to its likely
    /// actor (a window manager, shell, or our own process).
    /// </summary>
    internal static string DescribeForegroundWindow()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return "none";
            }

            GetWindowThreadProcessId(hwnd, out uint processId);
            string processName;
            try
            {
                processName = Process.GetProcessById((int)processId).ProcessName;
            }
            catch
            {
                processName = "unknown";
            }

            return $"hwnd=0x{hwnd.ToInt64():X} pid={processId} {processName}";
        }
        catch
        {
            return "unavailable";
        }
    }

    /// <summary>True when <paramref name="hWnd"/> is a currently valid window (kernel-level check).</summary>
    internal static bool IsWindow(IntPtr hWnd) => IsWindowW(hWnd);

    /// <summary>
    /// The pid of the process that owns the window (0 for kernel windows); null when the
    /// handle is not a live window. Kernel-level lookup; no cross-process message is sent.
    /// </summary>
    internal static uint? GetWindowOwnerPid(IntPtr hWnd)
    {
        if (!IsWindowW(hWnd))
        {
            return null;
        }

        return GetWindowThreadProcessId(hWnd, out uint pid) == 0 ? null : pid;
    }

    /// <summary>The window's class name; null when not a live window or on interop failure.</summary>
    internal static string? GetClassNameSafe(IntPtr hWnd)
    {
        if (!IsWindowW(hWnd))
        {
            return null;
        }

        char[] buffer = new char[256];
        int length = GetClassNameW(hWnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : null;
    }

    /// <summary>
    /// The window's title from DWM's cached copy (DWMWA_TITLE). Deliberately not
    /// GetWindowTextW: that sends a synchronous message to the owning thread and can hang
    /// the caller if that thread is wedged; the DWM-cache read cannot. Null when DWM has
    /// no title or on interop failure.
    /// </summary>
    internal static string? GetDwmTitle(IntPtr hWnd)
    {
        if (!IsWindowW(hWnd))
        {
            return null;
        }

        try
        {
            if (DwmGetWindowAttribute(hWnd, DwmWaTitle, out IntPtr titlePtr) != 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(titlePtr);
            }
            finally
            {
                // DWM serves DWMWA_TITLE buffers from the COM task heap; the marshal
                // copies the string, so the original must be released back or it leaks
                // one buffer per call.
                Marshal.FreeCoTaskMem(titlePtr);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The current thread's native thread id.</summary>
    internal static uint GetCurrentThreadId() => GetCurrentThreadIdInternal();

    /// <summary>
    /// True when <paramref name="threadId"/> is a live thread of the current process:
    /// the definitive in/external discriminator for who sent the message being handled.
    /// (Process.Threads rather than psapi's EnumThreadProcessIds: that export has been
    /// removed from psapi in recent Windows builds.)
    /// </summary>
    internal static bool OwnsThread(uint threadId)
    {
        try
        {
            using Process process = Process.GetCurrentProcess();
            foreach (var item in process.Threads)
            {
                // ProcessThreadCollection enumerates non-generic (object elements);
                // pattern-cast keeps this independent of the collection's variance.
                if (item is ProcessThread thread && unchecked((uint)thread.Id) == threadId)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            // Forensic path: a lookup failure must read as "not our thread", never throw
            // out of a window-message handler.
            return false;
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowPosW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
    private static extern uint GetCurrentThreadIdInternal();

    [DllImport("user32.dll", EntryPoint = "IsWindow")]
    private static extern bool IsWindowW(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, char[] lpClassName, int nMaxCount);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out IntPtr data);
}
