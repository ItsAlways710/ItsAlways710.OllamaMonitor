namespace ElBruno.OllamaMonitor.Models;

/// <summary>
/// Per-task context-window usage parsed from Ollama server log lines
/// (n_ctx_slot, task.n_tokens, tg). Keyed by the "task" id in the log.
/// </summary>
public sealed record ContextWindowSample
{
    public int TaskId { get; init; }

    /// <summary>Total context window allocated for the loaded model (n_ctx_slot).</summary>
    public int? SlotTokens { get; init; }

    /// <summary>Current live context usage for the task (task.n_tokens).</summary>
    public int? UsedTokens { get; init; }

    /// <summary>Most recent generation speed from slot print_timing (t/s).</summary>
    public double? TokensPerSecond { get; init; }

    /// <summary>UsedTokens as a percentage of SlotTokens, when both are known.</summary>
    public double? UsedPercent { get; init; }
}
