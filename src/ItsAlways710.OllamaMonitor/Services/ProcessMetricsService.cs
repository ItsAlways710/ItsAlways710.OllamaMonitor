using System.Diagnostics;
using ItsAlways710.OllamaMonitor.Diagnostics;
using ItsAlways710.OllamaMonitor.Interop;

namespace ItsAlways710.OllamaMonitor.Services;

public sealed class ProcessMetricsService
{
    private readonly DiagnosticsLogService _diagnostics;
    private readonly Dictionary<int, CpuSample> _cpuSamples = [];
    private readonly Dictionary<int, IoSample> _ioSamples = [];
    private readonly Lock _syncRoot = new();

    public ProcessMetricsService(DiagnosticsLogService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public Task<ProcessMetricsResult> GetMetricsAsync(bool enableDiskMetrics, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var processes = Process.GetProcessesByName("ollama")
            .OrderBy(process => SafeGetStartTime(process) ?? DateTime.MaxValue)
            .ToArray();

        if (processes.Length == 0)
        {
            return Task.FromResult(new ProcessMetricsResult(false, ErrorMessage: "Ollama process not found."));
        }

        if (processes.Length > 1)
        {
            _diagnostics.WriteWarning($"Multiple Ollama processes found. Using PID {processes[0].Id}.");
        }

        var process = processes[0];
        process.Refresh();

        var cpuPercent = GetCpuUsage(process);
        var startedAt = SafeGetStartTime(process);
        var diskMetrics = enableDiskMetrics ? GetDiskMetrics(process) : (ReadBytesPerSecond: (long?)null, WriteBytesPerSecond: (long?)null);

        return Task.FromResult(new ProcessMetricsResult(
            true,
            cpuPercent,
            process.WorkingSet64,
            process.PrivateMemorySize64,
            diskMetrics.ReadBytesPerSecond,
            diskMetrics.WriteBytesPerSecond,
            startedAt is null ? null : new DateTimeOffset(startedAt.Value),
            process.ProcessName));
    }

    private double? GetCpuUsage(Process process)
    {
        var now = DateTimeOffset.UtcNow;
        var totalProcessorTime = process.TotalProcessorTime;

        lock (_syncRoot)
        {
            if (!_cpuSamples.TryGetValue(process.Id, out var previousSample))
            {
                _cpuSamples[process.Id] = new CpuSample(totalProcessorTime, now);
                return null;
            }

            var elapsed = now - previousSample.Timestamp;
            if (elapsed <= TimeSpan.Zero)
            {
                return previousSample.LastCpuPercent;
            }

            var cpuTimeDelta = totalProcessorTime - previousSample.TotalProcessorTime;
            var cpuPercent = cpuTimeDelta.TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100d;
            var boundedCpuPercent = Math.Clamp(cpuPercent, 0, 100);
            _cpuSamples[process.Id] = new CpuSample(totalProcessorTime, now, boundedCpuPercent);
            return boundedCpuPercent;
        }
    }

    private (long? ReadBytesPerSecond, long? WriteBytesPerSecond) GetDiskMetrics(Process process)
    {
        if (!NativeMethods.GetProcessIoCounters(process.Handle, out var ioCounters))
        {
            return (null, null);
        }

        var now = DateTimeOffset.UtcNow;
        lock (_syncRoot)
        {
            if (!_ioSamples.TryGetValue(process.Id, out var previousSample))
            {
                _ioSamples[process.Id] = new IoSample(ioCounters.ReadTransferCount, ioCounters.WriteTransferCount, now);
                return (null, null);
            }

            var elapsedSeconds = (now - previousSample.Timestamp).TotalSeconds;
            if (elapsedSeconds <= 0)
            {
                return (null, null);
            }

            var readPerSecond = (long)((ioCounters.ReadTransferCount - previousSample.ReadBytes) / elapsedSeconds);
            var writePerSecond = (long)((ioCounters.WriteTransferCount - previousSample.WriteBytes) / elapsedSeconds);
            _ioSamples[process.Id] = new IoSample(ioCounters.ReadTransferCount, ioCounters.WriteTransferCount, now);
            return (Math.Max(0, readPerSecond), Math.Max(0, writePerSecond));
        }
    }

    private static DateTime? SafeGetStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return null;
        }
    }

    private sealed record CpuSample(TimeSpan TotalProcessorTime, DateTimeOffset Timestamp, double? LastCpuPercent = null);

    private sealed record IoSample(ulong ReadBytes, ulong WriteBytes, DateTimeOffset Timestamp);
}
