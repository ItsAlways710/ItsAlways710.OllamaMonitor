using System.Globalization;
using System.Text.RegularExpressions;
using ElBruno.OllamaMonitor.Models;

namespace ElBruno.OllamaMonitor.Services;

/// <summary>
/// Tracks per-task context-window usage parsed from Ollama server log lines:
///   slot   operator(): id 0 | task N | new prompt, n_ctx_slot = 188416, task.n_tokens = X
///   slot print_timing: id 0 | task N | ... tg = X.XX t/s
/// Subscribes to OllamaLogService.LogLineReceived (both process-owned and
/// file-polling modes fire it). State is keyed by the "task" id in the line,
/// since OLLAMA_MAX_LOADED_MODELS &gt; 1 allows concurrent tasks. Thread-safe.
/// </summary>
public sealed class ContextTrackingService : IDisposable
{
    private static readonly Regex TaskIdRegex = new(@"\btask\s*[=]?\s*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SlotTokensRegex = new(@"\bn_ctx_slot\s*[=]?\s*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UsedTokensRegex = new(@"\btask\.n_tokens\s*[=]?\s*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TokensPerSecondRegex = new(@"\btg\s*[=]?\s*(\d+(?:\.\d+)?)\s*t/s", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Entries with no log activity for this long are dropped from snapshots.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private readonly IOllamaLogService _logService;
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<int, ContextTaskState> _tasks = new();

    public ContextTrackingService(IOllamaLogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _logService.LogLineReceived += OnLogLineReceived;
    }

    /// <summary>Returns the current per-task context usage, pruned of stale entries.</summary>
    public IReadOnlyList<ContextWindowSample> GetSnapshot()
    {
        var now = DateTimeOffset.UtcNow;

        lock (_syncRoot)
        {
            var staleTaskIds = _tasks
                .Where(task => now - task.Value.LastUpdated > StaleAfter)
                .Select(task => task.Key)
                .ToList();

            foreach (var taskId in staleTaskIds)
            {
                _tasks.Remove(taskId);
            }

            return _tasks
                .OrderBy(task => task.Key)
                .Select(task => BuildSample(task.Key, task.Value))
                .ToList();
        }
    }

    public void Dispose() => _logService.LogLineReceived -= OnLogLineReceived;

    private void OnLogLineReceived(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var taskIdMatch = TaskIdRegex.Match(line);
        if (!taskIdMatch.Success || !int.TryParse(taskIdMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var taskId))
        {
            return;
        }

        var slotMatch = SlotTokensRegex.Match(line);
        var usedMatch = UsedTokensRegex.Match(line);
        var tokensPerSecondMatch = TokensPerSecondRegex.Match(line);

        if (!slotMatch.Success && !usedMatch.Success && !tokensPerSecondMatch.Success)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (!_tasks.TryGetValue(taskId, out var state))
            {
                state = new ContextTaskState();
                _tasks[taskId] = state;
            }

            if (slotMatch.Success && int.TryParse(slotMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var slotTokens))
            {
                state.SlotTokens = slotTokens;
            }

            if (usedMatch.Success && int.TryParse(usedMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var usedTokens))
            {
                state.UsedTokens = usedTokens;
            }

            if (tokensPerSecondMatch.Success &&
                double.TryParse(tokensPerSecondMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var tokensPerSecond))
            {
                state.TokensPerSecond = tokensPerSecond;
            }

            state.LastUpdated = DateTimeOffset.UtcNow;
        }
    }

    private static ContextWindowSample BuildSample(int taskId, ContextTaskState state)
    {
        var usedPercent = (double?)(state.SlotTokens is > 0 && state.UsedTokens is not null
            ? state.UsedTokens.Value * 100.0 / state.SlotTokens.Value
            : null);

        return new ContextWindowSample
        {
            TaskId = taskId,
            SlotTokens = state.SlotTokens,
            UsedTokens = state.UsedTokens,
            TokensPerSecond = state.TokensPerSecond,
            UsedPercent = usedPercent
        };
    }

    private sealed class ContextTaskState
    {
        public int? SlotTokens { get; set; }
        public int? UsedTokens { get; set; }
        public double? TokensPerSecond { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }
}
