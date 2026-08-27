using ElBruno.OllamaMonitor.Models;

namespace ElBruno.OllamaMonitor.Helpers;

public static class StatusTextHelper
{
    public static string GetStateLabel(OllamaMonitorState state) => state switch
    {
        OllamaMonitorState.NotReachable => "Not Reachable",
        OllamaMonitorState.Running => "Running",
        OllamaMonitorState.ModelLoaded => "Model Loaded",
        OllamaMonitorState.HighUsage => "High Usage",
        OllamaMonitorState.Error => "Error",
        _ => "Unknown"
    };

    public static string BuildTooltip(OllamaMonitorSnapshot snapshot)
    {
        var segments = new List<string>
        {
            $"Ollama: {GetStateLabel(snapshot.State)}"
        };

        if (snapshot.Models.Count > 0)
        {
            segments.Add(snapshot.Models[0].Name);
        }

        if (snapshot.Resources?.CpuPercent is not null)
        {
            segments.Add($"CPU {snapshot.Resources.CpuPercent.Value:0.#}%");
        }

        return TrimTooltip(string.Join(" | ", segments));
    }

    public static string BuildGpuSummary(ResourceSnapshot resources)
    {
        if (!string.IsNullOrWhiteSpace(resources.GpuStatus))
        {
            return resources.GpuStatus;
        }

        if (resources.GpuPercent is null)
        {
            return "GPU unavailable";
        }

        var gpuName = string.IsNullOrWhiteSpace(resources.GpuName) ? "GPU" : resources.GpuName;
        return $"{gpuName} {FormatPercent(resources.GpuPercent)} ({FormatBytes(resources.VramUsedBytes)} / {FormatBytes(resources.VramTotalBytes)})";
    }

    public static string BuildCompactGpuSummary(ResourceSnapshot? resources)
    {
        if (resources is null)
        {
            return "Unavailable";
        }

        if (!string.IsNullOrWhiteSpace(resources.GpuStatus))
        {
            return resources.GpuStatus;
        }

        if (resources.GpuPercent is null)
        {
            return "Unavailable";
        }

        return FormatPercent(resources.GpuPercent);
    }

    public static string BuildProcessorDisplay(long? size, long? sizeVram)
    {
        if (size is null || size.Value == 0)
        {
            return "100% CPU";
        }

        var vram = sizeVram ?? 0;

        if (vram <= 0)
        {
            return "100% CPU";
        }

        if (vram >= size.Value)
        {
            return "100% GPU";
        }

        var gpuPercent = (int)Math.Round(vram * 100.0 / size.Value);
        var cpuPercent = 100 - gpuPercent;
        return $"{cpuPercent}% CPU · {gpuPercent}% GPU";
    }

    public static string FormatPercent(double? value) => value is null ? "Unavailable" : $"{value.Value:0.#}%";

    public static string FormatBytes(long? value)
    {
        if (value is null)
        {
            return "Unavailable";
        }

        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double size = value.Value;
        var order = 0;
        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {suffixes[order]}";
    }

    public static string FormatBytesPerSecond(long? value) =>
        value is null ? "Unavailable" : $"{FormatBytes(value)} / s";

    public static string BuildContextSummary(IReadOnlyList<ContextWindowSample> samples)
    {
        if (samples.Count == 0)
        {
            return "Unavailable";
        }

        var showTaskIds = samples.Count > 1;
        var lines = samples.Select(sample =>
        {
            var parts = new List<string>();
            if (sample.UsedTokens is not null && sample.SlotTokens is not null)
            {
                parts.Add($"{sample.UsedTokens.Value:N0} / {sample.SlotTokens.Value:N0} tokens");
            }

            if (sample.UsedPercent is not null)
            {
                parts.Add($"{sample.UsedPercent.Value:0.#}% used");
            }

            if (sample.TokensPerSecond is not null)
            {
                parts.Add($"{sample.TokensPerSecond.Value:0.##} t/s");
            }

            var detail = parts.Count > 0 ? string.Join(" \u00B7 ", parts) : "waiting for log activity";
            return showTaskIds ? $"task {sample.TaskId}: {detail}" : detail;
        });

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatDateTime(DateTimeOffset? value) =>
        value is null ? "Unavailable" : value.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

    private static string TrimTooltip(string value)
    {
        const int maxLength = 63;
        return value.Length <= maxLength ? value : $"{value[..(maxLength - 3)]}...";
    }
}
