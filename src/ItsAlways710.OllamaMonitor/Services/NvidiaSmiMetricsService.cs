using System.ComponentModel;
using System.Globalization;
using System.Diagnostics;
using ItsAlways710.OllamaMonitor.Diagnostics;

namespace ItsAlways710.OllamaMonitor.Services;

public sealed class NvidiaSmiMetricsService
{
    private readonly DiagnosticsLogService _diagnostics;

    public NvidiaSmiMetricsService(DiagnosticsLogService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public async Task<GpuMetricsResult> GetMetricsAsync(bool enabled, CancellationToken cancellationToken)
    {
        if (!enabled)
        {
            return new GpuMetricsResult(false, StatusMessage: "GPU metrics disabled");
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=name,utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits",
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _diagnostics.WriteWarning($"nvidia-smi exited with code {process.ExitCode}: {stderr}");
                return new GpuMetricsResult(false, StatusMessage: "GPU not available");
            }

            var lines = stdout
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (lines.Length == 0)
            {
                return new GpuMetricsResult(false, StatusMessage: "GPU not available");
            }

            var parsedRows = lines
                .Select(ParseGpuRow)
                .Where(row => row is not null)
                .Select(row => row!)
                .ToList();

            if (parsedRows.Count == 0)
            {
                return new GpuMetricsResult(false, StatusMessage: "GPU not available");
            }

            var selected = parsedRows.OrderByDescending(row => row.GpuPercent ?? 0).First();
            return new GpuMetricsResult(
                true,
                selected.GpuPercent,
                selected.VramUsedBytes,
                selected.VramTotalBytes,
                selected.Name);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or TaskCanceledException)
        {
            _diagnostics.WriteWarning($"Unable to collect NVIDIA GPU metrics: {exception.Message}");
            return new GpuMetricsResult(false, StatusMessage: "GPU not supported");
        }
    }

    private static ParsedGpuRow? ParseGpuRow(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 4)
        {
            return null;
        }

        return new ParsedGpuRow(
            parts[0],
            ParseDouble(parts[1]),
            ToBytes(ParseDouble(parts[2])),
            ToBytes(ParseDouble(parts[3])));
    }

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static long? ToBytes(double? mib) =>
        mib is null ? null : (long?)(mib.Value * 1024d * 1024d);

    private sealed record ParsedGpuRow(string Name, double? GpuPercent, long? VramUsedBytes, long? VramTotalBytes);
}
