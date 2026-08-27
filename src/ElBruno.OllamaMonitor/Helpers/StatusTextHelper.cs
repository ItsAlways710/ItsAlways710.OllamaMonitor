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

    /// <summary>
    /// Builds the Mini Monitor context lines (never the Details window's BuildContextSummary).
    /// Each loaded model with live task data gets one line named after it; at most one
    /// task per model is shown (the most active one). Tasks are attributed to models by
    /// matching the log's 'n_ctx_slot' against the model's 'context_length' from /api/ps.
    /// Tasks with no matching loaded model are dropped (leftovers of released models).
    /// Tasks that cannot be attributed by name (several loaded models share one context
    /// size) each still get their own line — labeled unknown but with each task's own
    /// stats; lines are never merged across tasks.
    /// </summary>
    public static IReadOnlyList<string> BuildMiniModelContextLines(
        IReadOnlyList<OllamaModelSnapshot> models,
        IReadOnlyList<ContextWindowSample> samples)
    {
        if (samples.Count == 0)
        {
            return new[] { "Context: Unavailable" };
        }

        if (models.Count == 0)
        {
            // No loaded models reported (API failure or all released): keep every live
            // task visible, unlabeled.
            return BuildUnlabeledTaskLines(samples);
        }

        // Sole candidate without a measured context (older Ollama builds): with exactly one
        // model loaded its slot is certain — only when it reports no context_length at all,
        // so we never claim a stale slot that doesn't match a measured model.
        var soleUnmeasured = models.Count == 1 && models[0].ContextLength is null
            ? models[0]
            : null;

        var modelsWithContext = models
            .Where(model => model.ContextLength is not null)
            .ToList();

        var attributed = new Dictionary<OllamaModelSnapshot, List<ContextWindowSample>>();
        var anonymousPool = new List<ContextWindowSample>();

        foreach (var sample in samples)
        {
            var matches = modelsWithContext
                .Where(model => sample.SlotTokens is not null && model.ContextLength == sample.SlotTokens)
                .ToList();

            if (matches.Count == 1)
            {
                // Exact slot-size match with a single loaded model: certain.
                AddToGroup(attributed, matches[0], sample);
            }
            else if (matches.Count > 1)
            {
                // Several loaded models share the same context size: not attributable
                // without guessing, so pool unlabeled.
                anonymousPool.Add(sample);
            }
            else if (soleUnmeasured is not null)
            {
                // One loaded model, unknown context size: it is the only candidate.
                AddToGroup(attributed, soleUnmeasured, sample);
            }
            else
            {
                // No loaded model claims this slot: leftover of a released model.
            }
        }

        var lines = attributed
            .OrderByDescending(pair => TopSample(pair.Value).UsedPercent ?? double.NegativeInfinity)
            .ThenByDescending(pair => TopSample(pair.Value).TokensPerSecond ?? double.NegativeInfinity)
            .ThenBy(pair => pair.Key.Name)
            .Select(pair => $"{pair.Key.Name} - {BuildTopTaskDetail(TopSample(pair.Value))}")
            .ToList();

        if (anonymousPool.Count > 0)
        {
            // Attribution is ambiguous by name, but the tasks are distinct — one line
            // per task, each with its own stats.
            lines.AddRange(BuildUnlabeledTaskLines(anonymousPool));
        }

        return lines.Count > 0 ? lines : new[] { "Context: Unavailable" };
    }

    private static void AddToGroup(
        Dictionary<OllamaModelSnapshot, List<ContextWindowSample>> attributed,
        OllamaModelSnapshot model,
        ContextWindowSample sample)
    {
        if (!attributed.TryGetValue(model, out var list))
        {
            attributed[model] = list = new List<ContextWindowSample>();
        }

        list.Add(sample);
    }

    private static ContextWindowSample TopSample(IReadOnlyList<ContextWindowSample> samples) => samples
        .OrderByDescending(sample => sample.UsedPercent ?? double.NegativeInfinity)
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

    private static List<string> BuildUnlabeledTaskLines(IReadOnlyList<ContextWindowSample> samples)
    {
        var groups = samples
            .GroupBy(sample => sample.TaskId)
            .Select(group => group.ToList())
            .ToList();

        var ordered = groups
            .OrderByDescending(group => TopSample(group).UsedPercent ?? double.NegativeInfinity)
            .ThenByDescending(group => TopSample(group).TokensPerSecond ?? double.NegativeInfinity)
            .ThenBy(group => group[0].TaskId);

        var lines = new List<string>();
        foreach (var group in ordered)
        {
            lines.Add(BuildTopTaskDetail(TopSample(group)));
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
