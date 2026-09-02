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

        // Prefer the real inference processes: Ollama spawns one
        // llama-server.exe per loaded model, and that is where the CPU/RAM
        // cost of inference (including CPU offload when a model spills out
        // of VRAM) actually lives. Summing all of them gives the true total
        // cost of active inference. When none are running, fall back to the
        // idle-state wrapper process(es).
        var runners = SnapshotProcesses("llama-server");
        if (runners.Length > 0)
        {
            // The displayed label stays "ollama" even though the numbers are
            // sourced from llama-server: this is a source change, not a rename.
            return Task.FromResult(BuildMetrics(runners, enableDiskMetrics, displayProcessName: "ollama"));
        }

        var processes = SnapshotProcesses("ollama");

        if (processes.Length == 0)
        {
            return Task.FromResult(new ProcessMetricsResult(false, ErrorMessage: "Ollama process not found."));
        }

        if (processes.Length > 1)
        {
            _diagnostics.WriteWarning($"Multiple Ollama processes found. Using PID {processes[0].Id}.");
        }

        return Task.FromResult(BuildMetrics(new[] { processes[0] }, enableDiskMetrics, displayProcessName: processes[0].ProcessName));
    }

    private static Process[] SnapshotProcesses(string processName) =>
        Process.GetProcessesByName(processName)
            .OrderBy(process => SafeGetStartTime(process) ?? DateTime.MaxValue)
            .ToArray();

    private ProcessMetricsResult BuildMetrics(Process[] processes, bool enableDiskMetrics, string displayProcessName)
    {
        double? cpuPercent = null;
        long workingSetBytes = 0;
        long privateMemoryBytes = 0;
        long? readPerSecond = null;
        long? writePerSecond = null;

        foreach (var process in processes)
        {
            try
            {
                cpuPercent = Sum(cpuPercent, GetCpuUsage(process));
                process.Refresh();
                workingSetBytes += process.WorkingSet64;
                privateMemoryBytes += process.PrivateMemorySize64;
                if (enableDiskMetrics)
                {
                    var disk = GetDiskMetrics(process);
                    readPerSecond = Sum(readPerSecond, disk.ReadBytesPerSecond);
                    writePerSecond = Sum(writePerSecond, disk.WriteBytesPerSecond);
                }
            }
            catch
            {
                // A process can exit between enumeration and sampling; skip
                // its contribution instead of failing the whole batch.
            }
        }

        // The snapshot is ordered oldest-start first, so index 0 is the
        // longest-running one (its start time is the aggregate's).
        var startedAt = SafeGetStartTime(processes[0]);

        return new ProcessMetricsResult(
            true,
            cpuPercent,
            workingSetBytes,
            privateMemoryBytes,
            readPerSecond,
            writePerSecond,
            startedAt is null ? null : new DateTimeOffset(startedAt.Value),
            displayProcessName);
    }

    private static double? Sum(double? acc, double? value) => value is null ? acc : (acc ?? 0) + value.Value;

    private static long? Sum(long? acc, long? value) => value is null ? acc : (acc ?? 0) + value.Value;

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
