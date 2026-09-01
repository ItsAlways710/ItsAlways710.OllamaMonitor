using System.Diagnostics;

namespace ItsAlways710.OllamaMonitor.Interop;

/// <summary>
/// Identity of the reference window a topmost demotion was aimed at, captured at the
/// moment the WINDOWPOS was in flight (the reference handle is often short-lived and
/// gone by the time a later probe could run): owner pid, process name, window class
/// and DWM-cached title. Every part degrades to null on failure; capture never throws.
/// </summary>
public sealed record RefWindowIdentity
{
    /// <summary>The reference handle exactly as it appeared in the message.</summary>
    public IntPtr Handle { get; init; }

    /// <summary>Pid of the process that owns the reference window (0 = kernel window).</summary>
    public uint? OwnerPid { get; init; }

    /// <summary>Process name resolved from the owner pid; null when it cannot be resolved.</summary>
    public string? OwnerProcessName { get; init; }

    /// <summary>The reference window's class name; null when unavailable.</summary>
    public string? ClassName { get; init; }

    /// <summary>The reference window's title (DWM-cached); null when unavailable.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// Forensics for the demotion a topmost guard blocked: captures the reference window's
/// identity (pid, process, class, title) at the moment the handle is known valid and
/// classifies the sending thread, so the log line names its actor (in-process or
/// external) with no follow-up probe. All capture uses kernel-level or DWM-cache
/// lookups - never a cross-process message - so a wedged remote thread cannot block
/// the calling UI thread.
/// </summary>
public static class RefWindowForensics
{
    public static RefWindowIdentity Capture(IntPtr handle)
    {
        uint? ownerPid = WindowInterop.GetWindowOwnerPid(handle);
        if (ownerPid is null)
        {
            return new RefWindowIdentity { Handle = handle };
        }

        return new RefWindowIdentity
        {
            Handle = handle,
            OwnerPid = ownerPid,
            OwnerProcessName = ResolveProcessName(ownerPid.Value),
            ClassName = WindowInterop.GetClassNameSafe(handle),
            Title = WindowInterop.GetDwmTitle(handle)
        };
    }

    /// <summary>True when the given native thread id belongs to the current process.</summary>
    public static bool IsOwnProcessThread(uint threadId) =>
        WindowInterop.OwnsThread(threadId);

    /// <summary>The current thread's native thread id - the sender of the message being handled.</summary>
    public static uint GetCurrentThreadId() => WindowInterop.GetCurrentThreadId();

    /// <summary>
    /// Formats the identity for log embedding:
    /// <c>hwnd=0x831158; pid=1234; process=monarch; class=Foo; title='Bar'</c>;
    /// each missing part is rendered as <c>n/a</c>.
    /// </summary>
    public static string Format(RefWindowIdentity? identity)
    {
        if (identity is null)
        {
            return "n/a";
        }

        string title = identity.Title is null ? "n/a" : $"'{identity.Title}'";
        return $"hwnd=0x{(long)identity.Handle:X}; " +
               $"pid={identity.OwnerPid?.ToString() ?? "n/a"}; " +
               $"process={identity.OwnerProcessName ?? "n/a"}; " +
               $"class={identity.ClassName ?? "n/a"}; " +
               $"title={title}";
    }

    private static string? ResolveProcessName(uint pid)
    {
        try
        {
            using Process? process = Process.GetProcessById(unchecked((int)pid));
            return process?.ProcessName;
        }
        catch (Exception)
        {
            // Process already gone (common: short-lived actor windows live in short-lived
            // processes): report the pid and let the class/title identify the actor.
            return null;
        }
    }
}
