using System.Runtime.InteropServices;

namespace ItsAlways710.OllamaMonitor.Interop;

public static class ConsoleManager
{
    private const int AttachParentProcess = -1;

    public static void EnsureConsole()
    {
        if (NativeMethods.GetConsoleWindow() != IntPtr.Zero)
        {
            return;
        }

        if (!NativeMethods.AttachConsole(AttachParentProcess))
        {
            NativeMethods.AllocConsole();
        }
    }
}
