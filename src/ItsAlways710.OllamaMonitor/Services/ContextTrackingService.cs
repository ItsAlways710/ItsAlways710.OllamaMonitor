using System.Globalization;
using System.Text.RegularExpressions;
using ItsAlways710.OllamaMonitor.Models;
using ItsAlways710.OllamaMonitor.Ollama;

namespace ItsAlways710.OllamaMonitor.Services;

/// <summary>
/// Tracks per-task context-window usage parsed from Ollama server log lines:
///   slot   operator(): id 0 | task N | new prompt, n_ctx_slot = 188416, task.n_tokens = X
///   slot print_timing: id 0 | task N | ... tg = X.XX t/s
///   slot release: id 0 | task N | stop processing: n_tokens = Y
///   "starting llama-server" (runner load event, used for model attribution)
/// A task is created on its "new prompt" line, refreshed on "print_timing" lines, and
/// finalized on its "slot release" line, whose final n_tokens become the task's last
/// measured usage. The task then stays in the store (speed cleared, since the run is
/// over); its Mini Monitor line remains visible for up to StaleAfter regardless of
/// whether its model is still loaded - attribution is resolved once and cached on the
/// task itself (sticky), independently of the model's current load state. Pruning
/// happens here only after a long inactivity window (StaleAfter).
/// Attribution precedence: Tier 1 matches the task's n_ctx_slot against the -c flag of
/// the runner load event recorded from the "starting llama-server" line (joined to the
/// active model via its weight-file digest, bridged from the local model store), Tier 2
/// falls back to the /api/ps context_length match.
/// Subscribes to OllamaLogService.LogLineReceived (both process-owned and
/// file-polling modes fire it). State is keyed by (n_ctx_slot, task id), not task
/// id alone: the task id is a per-runner counter that restarts at 0 for every
/// loaded model, so concurrently loaded models legitimately reuse the same ids,
/// whose log lines must not overwrite each other's stats. A line without n_ctx_slot
/// (timing, token_count, release) targets that task id's existing state - the most
/// recently active one when several states share the id. OLLAMA_MAX_LOADED_MODELS
/// &gt; 1 allows concurrent models. Thread-safe.
/// </summary>
public sealed class ContextTrackingService : IDisposable
{
    private static readonly Regex TaskIdRegex = new(@"\btask\s*[=]?\s*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SlotTokensRegex = new(@"\bn_ctx_slot\s*[=]?\s*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UsedTokensRegex = new(@"\btask\.n_tokens\s*[=]?\s*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TokensPerSecondRegex = new(@"\btg\s*[=]?\s*(\d+(?:\.\d+)?)\s*t/s", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ReleaseRegex = new(@"^\s*slot\s+release:\s*id\s*\d+\s*\|\s*task\s*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ReleaseTokensRegex = new(@"\bn_tokens\s*[=]?\s*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RunnerModelDigestRegex = new(@"--model\s+.*?sha256[-:]([0-9a-fA-F]{64})", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RunnerPortRegex = new(@"--port\s+(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RunnerMaxContextRegex = new(@"(?:^|\s)-c\s+(\d+)(?=\s|$)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RunnerMmprojDigestRegex = new(@"--mmproj\s+.*?sha256[-:]([0-9a-fA-F]{64})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Safety valve for the append-only runner registry (see design doc §4).</summary>
    private const int MaxRunnerRegistryEntries = 500;

    /// <summary>Entries with no log activity for this long are dropped from snapshots.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private readonly IOllamaLogService _logService;
    private readonly OllamaModelStore _modelStore;
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<TaskKey, ContextTaskState> _tasks = new();
    private readonly List<RunnerRegistryEntry> _runnerRegistry = new();
    private long _activityTick;

    public ContextTrackingService(IOllamaLogService logService)
        : this(logService, new OllamaModelStore())
    {
    }

    internal ContextTrackingService(IOllamaLogService logService, OllamaModelStore modelStore)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _modelStore = modelStore ?? throw new ArgumentNullException(nameof(modelStore));
        _logService.LogLineReceived += OnLogLineReceived;
    }

    /// <summary>Returns the current per-task context usage, pruned of stale entries, with model attribution resolved where possible.</summary>
    public IReadOnlyList<ContextWindowSample> GetSnapshot(IReadOnlyList<OllamaModelSnapshot> models)
    {
        var safeModels = models ?? (IReadOnlyList<OllamaModelSnapshot>)Array.Empty<OllamaModelSnapshot>();

        // Resolve outside the lock - the store does file I/O and is stateless.
        var modelDigests = ResolveModelDigests(safeModels);

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

            foreach (var state in _tasks.Values)
            {
                TryAttributeTask(state, safeModels, modelDigests);
            }

            return _tasks
                .OrderBy(task => task.Key.SlotTokens)
                .ThenBy(task => task.Key.TaskId)
                .Select(task => BuildSample(task.Key.TaskId, task.Value))
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

        // Runner load event: record the load's weight digest, port, and -c for model
        // attribution. Carries no task id and is handled before the task parsing below.
        if (line.Contains("starting llama-server", StringComparison.OrdinalIgnoreCase))
        {
            TryRegisterRunnerLoad(line);
            return;
        }

        // Explicit end-of-run signal: a release line carries the run's final n_tokens.
        // Finalize the task with those values and clear its speed - the task stays in the
        // store (its line remains visible up to StaleAfter, attribution sticky); it is
        // pruned later by the StaleAfter window when reading a snapshot, not here.
        var releaseMatch = ReleaseRegex.Match(line);
        if (releaseMatch.Success && int.TryParse(releaseMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var releasedTaskId))
        {
            lock (_syncRoot)
            {
                var releasedState = FindTaskState(releasedTaskId);
                if (releasedState is not null)
                {
                    var finalTokens = ReleaseTokensRegex.Match(line);
                    if (finalTokens.Success && int.TryParse(finalTokens.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var finalUsed))
                    {
                        releasedState.UsedTokens = finalUsed;
                    }

                    releasedState.TokensPerSecond = null;
                    releasedState.LastUpdated = DateTimeOffset.UtcNow;
                    releasedState.ActivitySeq = ++_activityTick;
                }
            }

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
            int? slotTokens = slotMatch.Success && int.TryParse(slotMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var slotValue)
                ? slotValue
                : null;

            var state = ResolveTaskState(taskId, slotTokens);
            if (slotTokens is not null)
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
            state.ActivitySeq = ++_activityTick;
        }
    }

    /// <summary>
    /// Locates (without creating) the state for a task id. When the id collides
    /// across runners (per-runner task counters restart at 0 for every loaded
    /// model) the most recently active state is returned - the log lines carry no
    /// field identifying the owning runner, and the misrouted value self-corrects
    /// on that task's next correctly-targeted line.
    /// </summary>
    private ContextTaskState? FindTaskState(int taskId)
    {
        var candidates = _tasks.Where(entry => entry.Key.TaskId == taskId).ToList();
        return candidates switch
        {
            { Count: 1 } => candidates[0].Value,
            { Count: > 1 } => candidates.OrderByDescending(entry => entry.Value.ActivitySeq).First().Value,
            _ => null
        };
    }

    /// <summary>
    /// Locates (creating if needed) the state for a task line. A line with
    /// n_ctx_slot targets the (slot, task id) state, folding in any unbound orphan
    /// state for the same id (timing seen before the "new prompt" line); a line
    /// without it targets the most recently active existing state for the id, or an
    /// unbound orphan state when the id is unknown so far.
    /// </summary>
    private ContextTaskState ResolveTaskState(int taskId, int? slotTokens)
    {
        if (slotTokens is not null)
        {
            var key = new TaskKey(slotTokens.Value, taskId);
            if (!_tasks.TryGetValue(key, out var state))
            {
                state = new ContextTaskState();
                _tasks[key] = state;
            }

            var orphanKey = new TaskKey(UnknownSlot, taskId);
            if (_tasks.TryGetValue(orphanKey, out var orphan))
            {
                FoldOrphanInto(orphan, state);
                _tasks.Remove(orphanKey);
            }

            return state;
        }

        return FindTaskState(taskId) ?? CreateOrphanState(taskId);
    }

    private ContextTaskState CreateOrphanState(int taskId)
    {
        var orphan = new ContextTaskState();
        _tasks[new TaskKey(UnknownSlot, taskId)] = orphan;
        return orphan;
    }

    /// <summary>Copies stats from an unbound orphan state into the bound state without overwriting values the bound state already has.</summary>
    private static void FoldOrphanInto(ContextTaskState orphan, ContextTaskState state)
    {
        if (state.SlotTokens is null)
        {
            state.SlotTokens = orphan.SlotTokens;
        }

        if (state.UsedTokens is null)
        {
            state.UsedTokens = orphan.UsedTokens;
        }

        if (state.TokensPerSecond is null)
        {
            state.TokensPerSecond = orphan.TokensPerSecond;
        }
    }

    /// <summary>
    /// (n_ctx_slot, task id) state key. The task id alone is not unique across
    /// concurrently loaded models - it restarts per runner - so two models' "task 0"
    /// lines must not share a state. UnknownSlot marks a state seen before the
    /// task's "new prompt" line supplied its context.
    /// </summary>
    private sealed record TaskKey(int SlotTokens, int TaskId);

    /// <summary>TaskKey.SlotTokens value for states seen before n_ctx_slot was known.</summary>
    private const int UnknownSlot = -1;
    /// <summary>
    /// Records one successful runner load from a "starting llama-server" line. All three
    /// required fields (weight digest, port, max context) must parse; anything less is
    /// skipped silently, which is also the degradation path for older Ollama log formats.
    /// </summary>
    private void TryRegisterRunnerLoad(string line)
    {
        var digestMatch = RunnerModelDigestRegex.Match(line);
        var portMatch = RunnerPortRegex.Match(line);
        var maxContextMatch = RunnerMaxContextRegex.Match(line);

        if (!digestMatch.Success || !portMatch.Success || !maxContextMatch.Success)
        {
            return;
        }

        var modelDigest = OllamaModelStore.NormalizeDigest(digestMatch.Groups[1].Value);
        if (modelDigest is null ||
            !int.TryParse(portMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
            !int.TryParse(maxContextMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxContext))
        {
            return;
        }

        var mmprojMatch = RunnerMmprojDigestRegex.Match(line);
        string? mmprojDigest = mmprojMatch.Success ? OllamaModelStore.NormalizeDigest(mmprojMatch.Groups[1].Value) : null;

        var entry = new RunnerRegistryEntry
        {
            Port = port,
            ModelDigest = modelDigest,
            MaxContext = maxContext,
            MmprojDigest = mmprojDigest,
            LoadedAt = DateTimeOffset.UtcNow
        };

        lock (_syncRoot)
        {
            _runnerRegistry.Add(entry);
            if (_runnerRegistry.Count > MaxRunnerRegistryEntries)
            {
                _runnerRegistry.RemoveRange(0, _runnerRegistry.Count - MaxRunnerRegistryEntries);
            }
        }
    }

    /// <summary>
    /// Resolves a task's model attribution, at most once per task. Tier 1 joins the
    /// task's n_ctx_slot to the -c flag of the runner load event whose weight digest
    /// matches an active model (bridge: active model name → local manifest → weight
    /// digest); Tier 2 is the legacy /api/ps context_length match. Unresolved tasks are
    /// retried on the next GetSnapshot() call.
    /// </summary>
    private void TryAttributeTask(
        ContextTaskState state,
        IReadOnlyList<OllamaModelSnapshot> models,
        IReadOnlyDictionary<string, string?> modelDigests)
    {
        // Sticky: once resolved, never re-derived and never cleared - only task pruning
        // removes it. This is what survives a mid-task model unload.
        if (state.AttributedModelName is not null)
        {
            return;
        }

        if (state.SlotTokens is null)
        {
            return;
        }

        // Tier 1 - registry-based. Each active model's weight digest (from the local
        // store manifest; /api/ps's own digest field is the manifest hash and can never
        // equal the weight blob) is looked up in the runner registry; a hit only counts
        // when that load's -c flag equals the task's measured n_ctx_slot.
        var tier1Matches = new List<(string Name, string Digest)>();
        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Name) || !modelDigests.TryGetValue(model.Name, out var digest) || digest is null)
            {
                continue;
            }

            var entry = _runnerRegistry
                .Where(runner => runner.ModelDigest == digest)
                .OrderByDescending(runner => runner.LoadedAt)
                .FirstOrDefault();

            if (entry is not null && entry.MaxContext == state.SlotTokens)
            {
                tier1Matches.Add((model.Name, digest));
            }
        }

        if (tier1Matches.DistinctBy(match => match.Name).Count() == 1)
        {
            var match = tier1Matches.DistinctBy(m => m.Name).First();
            state.AttributedModelName = match.Name;
            state.AttributedModelDigest = match.Digest;
            return;
        }

        // Tier 2 - legacy, the exact previous StatusTextHelper behavior, moved here.
        var modelsWithContext = models.Where(model => model.ContextLength is not null).ToList();
        var legacyMatches = modelsWithContext.Where(model => model.ContextLength == state.SlotTokens).ToList();
        if (legacyMatches.Count == 1)
        {
            modelDigests.TryGetValue(legacyMatches[0].Name, out var legacyDigest);
            state.AttributedModelName = legacyMatches[0].Name;
            state.AttributedModelDigest = legacyDigest;
            return;
        }

        // Sole candidate without a measured context (older Ollama builds): with exactly
        // one model loaded its slot is certain, only when it reports no context_length.
        if (models.Count == 1 && models[0].ContextLength is null)
        {
            modelDigests.TryGetValue(models[0].Name, out var unmeasuredDigest);
            state.AttributedModelName = models[0].Name;
            state.AttributedModelDigest = unmeasuredDigest;
            return;
        }

        // Otherwise: leave unattributed - retried on the next GetSnapshot() call.
    }

    /// <summary>Resolves the weight digest for every active model name (null when unresolvable).</summary>
    private IReadOnlyDictionary<string, string?> ResolveModelDigests(IReadOnlyList<OllamaModelSnapshot> models)
    {
        var digestByModel = new Dictionary<string, string?>(models.Count);
        foreach (var model in models)
        {
            if (digestByModel.ContainsKey(model.Name))
            {
                continue;
            }

            digestByModel[model.Name] = _modelStore.GetModelDigest(model.Name);
        }

        return digestByModel;
    }

    private static ContextWindowSample BuildSample(int taskId, ContextTaskState state)
    {
        var usedPercent = (double?)(state.SlotTokens is > 0 && state.UsedTokens is not null
            ? state.UsedTokens.Value * 100.0 / state.SlotTokens.Value
            : null);

        return new ContextWindowSample
        {
            TaskId = taskId,
            LastUpdated = state.LastUpdated,
            SlotTokens = state.SlotTokens,
            UsedTokens = state.UsedTokens,
            TokensPerSecond = state.TokensPerSecond,
            UsedPercent = usedPercent,
            ModelDigest = state.AttributedModelDigest,
            ModelName = state.AttributedModelName
        };
    }

    /// <summary>
    /// One recorded "starting llama-server" event. Append-only list (not port-keyed):
    /// ephemeral ports are reused by the OS, so keying by port would let a later load
    /// silently overwrite an earlier one and corrupt attribution of tasks still
    /// referencing the earlier load. Lookup reduces to "latest entry per digest".
    /// </summary>
    private sealed class RunnerRegistryEntry
    {
        public required int Port { get; init; }
        public required string ModelDigest { get; init; }
        public required int MaxContext { get; init; }
        public string? MmprojDigest { get; init; }
        public required DateTimeOffset LoadedAt { get; init; }
    }

    private sealed class ContextTaskState
    {
        public long ActivitySeq { get; set; }
        public int? SlotTokens { get; set; }
        public int? UsedTokens { get; set; }
        public double? TokensPerSecond { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public string? AttributedModelDigest { get; set; }
        public string? AttributedModelName { get; set; }
    }
}
