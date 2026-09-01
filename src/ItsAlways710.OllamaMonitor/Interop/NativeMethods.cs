using System.Runtime.InteropServices;

namespace ItsAlways710.OllamaMonitor.Interop;

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetProcessIoCounters(IntPtr hProcess, out IoCounters ioCounters);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetConsoleWindow();

    /// <summary>
    /// Cumulative CPU time counters (100-nanosecond units) since system boot.
    /// Note: <c>kernelTime</c> INCLUDES <c>idleTime</c>; busy time is
    /// (kernelTime + userTime) - idleTime.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FileTime
    {
        public uint DwLowDateTime;
        public uint DwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatusEx
    {
        public uint DwLength;
        public uint DwMemoryLoad;
        public ulong UllTotalPhys;
        public ulong UllAvailPhys;
        public ulong UllTotalPageFile;
        public ulong UllAvailPageFile;
        public ulong UllTotalVirtual;
        public ulong UllAvailVirtual;

        // GlobalMemoryStatusEx requires DwLength = 64 (the documented
        // extended struct size); current Windows rejects the 56-byte size.
        public ulong _Reserved;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    internal struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }
}
