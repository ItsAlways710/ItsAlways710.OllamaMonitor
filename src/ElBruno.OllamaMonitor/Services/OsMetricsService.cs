using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Interop;

namespace ElBruno.OllamaMonitor.Services;

/// <summary>
/// Samples whole-machine (OS-level) CPU and memory usage — the "(System)"
/// half of the CPU/Memory lines. CPU is a two-sample <c>GetSystemTimes</c>
/// delta (the first sample has no prior state and returns null, the same
/// convention as <see cref="ProcessMetricsService"/>); memory is a direct
/// <c>GlobalMemoryStatusEx</c> read.
/// </summary>
public sealed class OsMetricsService
{
    private readonly DiagnosticsLogService _diagnostics;
    private readonly Lock _syncRoot = new();
    private SystemTimeSample? _previousSample;

    public OsMetricsService(DiagnosticsLogService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public Task<OsMetricsResult> GetMetricsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cpuPercent = GetCpuUsage();
        var memory = GetMemoryUsage();

        return Task.FromResult(new OsMetricsResult(
            cpuPercent,
            memory.TotalBytes,
            memory.UsedBytes,
            memory.Percent));
    }

    private double? GetCpuUsage()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            _diagnostics.WriteWarning("GetSystemTimes failed; system CPU% unavailable.");
            return null;
        }

        var idleTime = ToTicks(idle);
        // kernelTime INCLUDES idleTime, so total = kernel + user.
        var totalTime = ToTicks(kernel) + ToTicks(user);

        var now = DateTimeOffset.UtcNow;
        lock (_syncRoot)
        {
            if (_previousSample is null)
            {
                _previousSample = new SystemTimeSample(idleTime, totalTime, now);
                return null;
            }

            if (now - _previousSample.Timestamp <= TimeSpan.Zero)
            {
                return _previousSample.CpuPercent;
            }

            var totalDelta = totalTime - _previousSample.TotalTime;
            if (totalDelta <= 0)
            {
                return _previousSample.CpuPercent;
            }

            var idleDelta = idleTime - _previousSample.IdleTime;
            var cpuPercent = (1d - (double)idleDelta / totalDelta) * 100d;
            var boundedCpuPercent = Math.Clamp(cpuPercent, 0, 100);
            _previousSample = new SystemTimeSample(idleTime, totalTime, now, boundedCpuPercent);
            return boundedCpuPercent;
        }
    }

    private (long? TotalBytes, long? UsedBytes, double? Percent) GetMemoryUsage()
    {
        var memoryStatus = new NativeMethods.MemoryStatusEx
        {
            DwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MemoryStatusEx>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref memoryStatus))
        {
            _diagnostics.WriteWarning("GlobalMemoryStatusEx failed; system memory% unavailable.");
            return (null, null, null);
        }

        var totalBytes = (long)memoryStatus.UllTotalPhys;
        var usedBytes = totalBytes - (long)memoryStatus.UllAvailPhys;
        double? percent = totalBytes > 0 ? Math.Clamp(usedBytes * 100d / totalBytes, 0, 100) : null;
        return (totalBytes, usedBytes, percent);
    }

    private static ulong ToTicks(NativeMethods.FileTime fileTime) =>
        ((ulong)fileTime.DwHighDateTime << 32) | fileTime.DwLowDateTime;

    private sealed record SystemTimeSample(ulong IdleTime, ulong TotalTime, DateTimeOffset Timestamp, double? CpuPercent = null);
}
