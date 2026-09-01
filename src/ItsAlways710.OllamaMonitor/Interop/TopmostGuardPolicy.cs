namespace ItsAlways710.OllamaMonitor.Interop;

/// <summary>
/// Pure decision logic for the Mini Monitor topmost defense: given the Z-order
/// position a WINDOWPOS message asks for, decide whether it would move the window
/// out of the topmost band. Kept dependency-free so it is unit-testable.
///
/// Windows topmost state is a persistent band (the WS_EX_TOPMOST extended style),
/// not a Z-order position. Per MSDN (SetWindowPos), it is stripped when a window
/// is repositioned to the bottom of the Z order or before any non-topmost window,
/// and once stripped it stays stripped until re-asserted. The guard intercepts
/// those demotions while the window is supposed to be topmost.
/// </summary>
public static class TopmostGuardPolicy
{
    /// <summary>HWND_TOP (0): keep the current band and move to its top.</summary>
    public const long HwndTop = 0;

    /// <summary>HWND_BOTTOM (1): repositions to the bottom and strips topmost state.</summary>
    public const long HwndBottom = 1;

    /// <summary>HWND_NOTOPMOST (-2): places above all non-topmost windows; strips topmost state.</summary>
    public const long HwndNotopmost = -2;

    /// <summary>HWND_TOPMOST (-1): keeps (or restores) topmost state.</summary>
    public const long HwndTopmost = -1;

    /// <summary>
    /// True when the requested <c>WindowPOS.hwndInsertAfter</c> position would demote a
    /// topmost window out of its band: HWND_BOTTOM, HWND_NOTOPMOST, insertion before any
    /// real window handle, or any other unrecognized sentinel. HWND_TOP and HWND_TOPMOST
    /// keep the window in the topmost band and are the only positions a topmost window
    /// should move to.
    /// </summary>
    public static bool IsDemotingPosition(long hwndInsertAfter)
    {
        // Everything except HWND_TOP and HWND_TOPMOST risks (or explicitly causes)
        // losing the band: be conservative and rewrite.
        return hwndInsertAfter != HwndTop && hwndInsertAfter != HwndTopmost;
    }
}
