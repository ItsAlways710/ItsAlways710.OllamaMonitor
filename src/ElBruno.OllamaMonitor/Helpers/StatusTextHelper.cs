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

    /// <summary>
    /// Foreground for the state header. High Usage is the warning state meant to
    /// draw the eye, so it is red; every other state keeps the routine white.
    /// </summary>
    public static string GetStateForeground(OllamaMonitorState state) =>
        state == OllamaMonitorState.HighUsage ? "#FFEF4444" : "White";

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
        var summary = FormatPercent(resources.GpuPercent);

        // Same VRAM figures the Details panel shows: used / total, when both are known.
        if (resources.VramUsedBytes is not null && resources.VramTotalBytes is not null)
        {
            summary += $" ({FormatBytes(resources.VramUsedBytes)} / {FormatBytes(resources.VramTotalBytes)})";
        }

        return summary;
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

    /// <summary>
    /// Builds the Mini Monitor context lines (never the Details window's BuildContextSummary).
    /// Each model with attributed task data gets one line named after it; at most one
    /// task per model is shown (the most recently active one - recency of log activity
    /// outranks magnitude of usage, so an idle high-usage task doesn't crowd out an
    /// actively-generating one). Attribution arrives pre-resolved
    /// on each ContextWindowSample (ModelName) from ContextTrackingService, which caches
    /// it per task (sticky) - so an unlabeled line means "not yet attributed" (a brief
    /// transient state), not "ambiguous by design". Unlabeled tasks each still get their
    /// own line with their own stats; lines are never merged across tasks.
    /// fit to the window's fixed width: a long model name is middle-ellipsized
    /// (the line's FullText keeps the unabridged name for its tooltip) while the
    /// stats are never trimmed.
    /// </summary>
    public static IReadOnlyList<MiniContextLine> BuildMiniModelContextLines(IReadOnlyList<ContextWindowSample> samples)
    {
        if (samples.Count == 0)
        {
            return new[] { new MiniContextLine("Context: Unavailable", "Context: Unavailable") };
        }

        var attributed = new Dictionary<string, List<ContextWindowSample>>();
        var anonymousPool = new List<ContextWindowSample>();

        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample.ModelName))
            {
                // Not yet attributed (transient state): keep it visible, unlabeled.
                anonymousPool.Add(sample);
                continue;
            }

            if (!attributed.TryGetValue(sample.ModelName, out var list))
            {
                attributed[sample.ModelName] = list = new List<ContextWindowSample>();
            }

            list.Add(sample);
        }

        var lines = attributed
            .OrderByDescending(pair => TopSample(pair.Value).UsedPercent ?? double.NegativeInfinity)
            .ThenByDescending(pair => TopSample(pair.Value).TokensPerSecond ?? double.NegativeInfinity)
            .ThenBy(pair => pair.Key)
            .Select(pair => BuildNamedMiniLine(pair.Key, BuildTopTaskDetail(TopSample(pair.Value))))
            .ToList();

        if (anonymousPool.Count > 0)
        {
            lines.AddRange(BuildUnlabeledTaskLines(anonymousPool));
        }

        return lines.Count > 0 ? lines : new[] { new MiniContextLine("Context: Unavailable", "Context: Unavailable") };
    }

    private static ContextWindowSample TopSample(IReadOnlyList<ContextWindowSample> samples) => samples
        .OrderByDescending(sample => sample.LastUpdated)
        .ThenByDescending(sample => sample.UsedPercent ?? double.NegativeInfinity)
        .ThenByDescending(sample => sample.TokensPerSecond ?? double.NegativeInfinity)
        .ThenBy(sample => sample.TaskId)
        .First();

    private static string BuildTopTaskDetail(ContextWindowSample top)
    {
        var parts = new List<string>();

        if (top.UsedTokens is not null && top.SlotTokens is not null)
        {
            parts.Add($"{top.UsedTokens.Value}/{top.SlotTokens.Value}");
        }

        if (top.UsedPercent is not null)
        {
            parts.Add($"{top.UsedPercent.Value:0.#}%");
        }

        if (top.TokensPerSecond is not null)
        {
            parts.Add($"{top.TokensPerSecond.Value:0.##}t/s");
        }

        return parts.Count > 0 ? string.Join(" - ", parts) : "waiting for log activity";
    }

    /// <summary>
    /// Maximum characters for a Mini Monitor context line. The window is fixed width
    /// (320px ~ 290px usable ~ 48 characters at the default 12px text), and WPF's
    /// TextTrimming would cut the right edge - the stats, which are what the line is
    /// for - so only the name may be shortened, and never the stats.
    /// </summary>
    private const int MiniLineMaxChars = 48;

    /// <summary>
    /// Builds one named context line "{name} - {detail}". When the line would exceed
    /// <see cref="MiniLineMaxChars"/>, the name is middle-ellipsized (head and tail kept -
    /// the identifying parts of most long model names) and the stats stay untouched.
    /// The unabridged name plus stats are retained in
    /// <see cref="MiniContextLine.FullText"/> for the line's tooltip.
    /// </summary>
    private static MiniContextLine BuildNamedMiniLine(string name, string detail)
    {
        var full = $"{name} - {detail}";

        if (full.Length <= MiniLineMaxChars)
        {
            return new MiniContextLine(full, full);
        }

        // Room for the name: the whole budget minus the stats, the fixed " - "
        // separator, and one character for the ellipsis itself.
        var available = Math.Max(3, MiniLineMaxChars - detail.Length - 4);
        var headLength = available / 2;
        var tailLength = available - headLength - 1;
        var shortened = $"{name[..headLength]}\u2026{name[^tailLength..]}";

        return new MiniContextLine($"{shortened} - {detail}", full);
    }

    private static List<MiniContextLine> BuildUnlabeledTaskLines(IReadOnlyList<ContextWindowSample> samples)
    {
        var groups = samples
            .GroupBy(sample => sample.TaskId)
            .Select(group => group.ToList())
            .ToList();

        var ordered = groups
            .OrderByDescending(group => TopSample(group).UsedPercent ?? double.NegativeInfinity)
            .ThenByDescending(group => TopSample(group).TokensPerSecond ?? double.NegativeInfinity)
            .ThenBy(group => group[0].TaskId);

        var lines = new List<MiniContextLine>();
        foreach (var group in ordered)
        {
            var detail = BuildTopTaskDetail(TopSample(group));
            lines.Add(new MiniContextLine(detail, detail));
        }

        return lines;
    }

    public static string FormatDateTime(DateTimeOffset? value) =>
        value is null ? "Unavailable" : value.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

    private static string TrimTooltip(string value)
    {
        const int maxLength = 63;
        return value.Length <= maxLength ? value : $"{value[..(maxLength - 3)]}...";
    }
}
