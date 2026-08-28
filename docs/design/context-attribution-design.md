Digest+Port Runner Attribution — Design Document
Target repo: ElBruno.OllamaMonitor (fork), branch context-tracking Files touched: ContextTrackingService.cs, StatusTextHelper.cs Files explicitly NOT touched: the log-tailing mechanism behind IOllamaLogService.LogLineReceived; StatusTextHelper.BuildContextSummary (Details window)


This replaces the context-size-based model attribution in StatusTextHelper.BuildMiniModelContextLines with a digest+port join sourced from a new log line the app doesn't currently parse. It also fixes the orphan-line behavior in the models.Count == 0 branch of that same method.


________________


0. Scope & Non-Goals
In scope: reliably mapping a task (as already tracked by ContextTrackingService) to a model name, using ground-truth data from the Ollama log's model-load line instead of relying solely on /api/ps's ContextLength field.


Explicitly out of scope for this design (noted later as residual limitations, not solved here):


* Fully disambiguating two concurrently active, currently-loaded models that happen to share an identical context size. Task-level log lines (n_ctx_slot, task.n_tokens, tg, slot release) carry no port or digest — that information gap is real and this design does not close it. What it does close is the much more common failure mode: attribution breaking because /api/ps's ContextLength was missing, stale, or because the same model was reloaded at a different context size over time.
* Full WMI implementation for cold start (a mitigation is described at the level the request asked for — a documented gap plus a suggested direction — not implemented).
* Making the llama.cpp /slots endpoint load-bearing (noted as a future avenue only).


________________


1. New Log Line to Parse: starting llama-server
This line is emitted once per successful runner load, as a single atomic, non-interleaved line (unlike the slot/srv/cmn passthrough lines ContextTrackingService already parses). Example (line-wrapped for readability; the real line is one line):


time=... level=INFO source=llama_server.go:433 msg="starting llama-server"


cmd="...\llama-server.exe --model C:\...\blobs\sha256-f5f1dd89... --port 49424


--host 127.0.0.1 ... -c 188416 -np 1 ... --mmproj C:\...\blobs\sha256-ac3714bf... ..."


Verify before implementing: confirm what IOllamaLogService.LogLineReceived actually delivers for this line — raw file text (with doubled backslashes / escaped quotes as stored in the log) or something already unescaped upstream. The patterns below are written to not care either way: they never anchor on quote characters, only on flag tokens and hex digits, so they should match regardless of escaping.
1.1 Gate check
Cheap substring gate, evaluated first (see §7 for exact insertion point):


line.Contains("starting llama-server", StringComparison.OrdinalIgnoreCase)
1.2 Field regexes
Add alongside the existing ReleaseTokensRegex field, following the same one-regex-per-field style already used for SlotTokensRegex / UsedTokensRegex / TokensPerSecondRegex:


private static readonly Regex RunnerModelDigestRegex = new(


    @"--model\s+\S*?sha256[-:]([0-9a-fA-F]{64})",


    RegexOptions.Compiled | RegexOptions.CultureInvariant);


private static readonly Regex RunnerPortRegex = new(


    @"--port\s+(\d+)",


    RegexOptions.Compiled | RegexOptions.CultureInvariant);


private static readonly Regex RunnerMaxContextRegex = new(


    @"(?:^|\s)-c\s+(\d+)(?=\s|$)",


    RegexOptions.Compiled | RegexOptions.CultureInvariant);


private static readonly Regex RunnerMmprojDigestRegex = new(


    @"--mmproj\s+\S*?sha256[-:]([0-9a-fA-F]{64})",


    RegexOptions.Compiled | RegexOptions.CultureInvariant);


Notes:


* \S*?sha256[-:] (non-greedy) skips over the Windows path (C:\Users\...\blobs\) up to the digest regardless of path depth or username.
* [-:] accepts both the log's sha256-<hex> (dash, forced by Windows filename rules) and the API's sha256:<hex> (colon) — same pattern is reused for normalization in §2.
* RunnerModelDigestRegex and RunnerPortRegex and RunnerMaxContextRegex are required for a registry entry to be recorded; RunnerMmprojDigestRegex is optional (absent for non-multimodal models) and stored but not currently used in matching (see §10).
* If any required field fails to match, silently skip recording an entry for that line. This is also the natural degradation path for older Ollama builds whose log format differs — see §5, Tier 2.


________________


2. Digest Normalization
Blob paths on disk use sha256-<hex> (dash; Windows disallows : in filenames). The digest field returned by /api/ps is expected to use sha256:<hex> (colon) — verify this against a live /api/ps response before relying on it; if it turns out to already use dashes, the normalizer below is a no-op either way.


private static readonly Regex DigestHexRegex = new(


    @"sha256[-:]([0-9a-fA-F]{64})",


    RegexOptions.Compiled | RegexOptions.CultureInvariant);


private static string? NormalizeDigest(string? raw) =>


    raw is not null && DigestHexRegex.Match(raw) is { Success: true } m


        ? $"sha256:{m.Groups[1].Value.ToLowerInvariant()}"


        : null;


Apply this to both sides of every comparison — the log-derived digest and whatever OllamaModelSnapshot.Digest reports — so the separator convention never matters.


Dependency to verify: OllamaModelSnapshot (referenced in StatusTextHelper.cs via models[0].Name, model.ContextLength) was not in the attached files. Confirm it already exposes a Digest (or equivalent) string property populated from /api/ps's digest field; add one if it doesn't. If the property has a different name, substitute it throughout this design.


________________


3. New / Changed Data Structures
3.1 RunnerRegistryEntry (new, private nested type in ContextTrackingService)
private sealed class RunnerRegistryEntry


{


    public required int Port { get; init; }


    public required string ModelDigest { get; init; }   // normalized, "sha256:<hex>"


    public required int MaxContext { get; init; }        // from -c


    public string? MmprojDigest { get; init; }            // normalized, or null


    public required DateTimeOffset LoadedAt { get; init; }


}


Stored as an append-only list, not a Dictionary<int, RunnerRegistryEntry> keyed by port. Reason: ephemeral ports get reused by the OS over a long-running process; keying by port risks a later load silently overwriting an earlier entry at the same port, which would corrupt attribution for any task still referencing the earlier load. A list plus the "latest entry per active digest" reduction in §5 gets the same practical lookup behavior without that risk.


private readonly List<RunnerRegistryEntry> _runnerRegistry = new();


Guarded by the existing _syncRoot lock — same lock already used for _tasks.
3.2 ContextTaskState — add two fields
public string? AttributedModelDigest { get; set; }


public string? AttributedModelName { get; set; }


Added alongside the existing SlotTokens, UsedTokens, TokensPerSecond, LastUpdated. Once non-null, these are never cleared except by the task itself being pruned (existing StaleAfter mechanism in GetSnapshot) — this is what makes attribution "sticky" (§6).
3.3 ContextWindowSample — add two fields
ContextWindowSample's source wasn't in the attached files; add these following its existing property style (matching however TaskId/SlotTokens/etc. are declared there):


public string? ModelDigest { get; init; }


public string? ModelName { get; init; }


Populated in BuildSample from state.AttributedModelDigest / state.AttributedModelName.


________________


4. Runner Registry: Population & Retention
Population: exactly one write path — a successful parse of a starting llama-server line (§1) appends a new RunnerRegistryEntry with LoadedAt = DateTimeOffset.UtcNow.


No explicit unload removal. There is no reliably-parseable "this runner just unloaded" log line (keep-alive expiry and memory-pressure eviction don't name the evicted runner directly in a single atomic line). Entries are therefore never removed in response to an inferred unload — they remain in the list as a historical record, which is actually what §6's sticky-attribution behavior needs: a task's initial attribution lookup may need to find the registry entry for a runner that has since exited.


Retention (pure safety valve, not correctness-critical): cap the list at a fixed count (suggest 500) and trim the oldest entries when exceeded, independent of the task StaleAfter window. This is deliberately decoupled from task lifetime: a generous count cap keeps registry entries alive far longer than any single task's 30-minute StaleAfter window would ever need, so there's no risk of a registry entry expiring while a task that depends on it is still live. A time-based retention tied to StaleAfter was considered and rejected — it would couple two independently-sized concerns (registry growth vs. task display lifetime) for no benefit.


________________


5. Attribution Algorithm — Tier 1 / Tier 2 Precedence
Run once per task, only while unattributed, from inside GetSnapshot (§7) — not from OnLogLineReceived. OnLogLineReceived stays exactly as focused as it is today on updating raw per-task counters; centralizing attribution in GetSnapshot means it naturally re-runs every UI refresh tick until it succeeds, with no separate retry/backoff machinery needed.


TryAttributeTask(state, models):


    if state.AttributedModelName is not null: return      // already resolved, stop here


    if state.SlotTokens is null: return                     // nothing to match yet


    activeDigests = { NormalizeDigest(m.Digest) for m in models } minus nulls


    # Tier 1 — registry-based (new)


    activeRunners = _runnerRegistry


        .Where(e => activeDigests.Contains(e.ModelDigest))


        .GroupBy(e => e.ModelDigest)


        .Select(g => g.OrderByDescending(e => e.LoadedAt).First())   # latest load per active digest


        .ToList()


    tier1Matches = activeRunners.Where(e => e.MaxContext == state.SlotTokens).ToList()


    if tier1Matches.Count == 1:


        match = tier1Matches[0]


        model = models.FirstOrDefault(m => NormalizeDigest(m.Digest) == match.ModelDigest)


        state.AttributedModelDigest = match.ModelDigest


        state.AttributedModelName  = model?.Name


        return


    # Tier 2 — legacy, exactly today's StatusTextHelper logic, moved here unchanged


    modelsWithContext = models.Where(m => m.ContextLength is not null).ToList()


    legacyMatches = modelsWithContext.Where(m => m.ContextLength == state.SlotTokens).ToList()


    if legacyMatches.Count == 1:


        state.AttributedModelName   = legacyMatches[0].Name


        state.AttributedModelDigest = NormalizeDigest(legacyMatches[0].Digest)


        return


    # soleUnmeasured fallback, preserved verbatim from current behavior


    if models.Count == 1 and models[0].ContextLength is null


       and legacyMatches.Count == 0 and tier1Matches.Count == 0:


        state.AttributedModelName   = models[0].Name


        state.AttributedModelDigest = NormalizeDigest(models[0].Digest)


        return


    # else: leave unattributed, retried next GetSnapshot() call


Precedence is explicit and per-task, not a global mode switch: Tier 1 is attempted first for every task; Tier 2 only runs when Tier 1 doesn't produce exactly one match. This single per-task fallthrough correctly covers both situations the original request asked about:


* Older Ollama build whose log format Tier 1 can't parse → _runnerRegistry simply never gets entries for that digest → activeRunners is always empty → Tier 1 never matches → every task falls through to Tier 2, i.e. today's exact behavior, unchanged.
* Registry populated but this particular task's context happens to not resolve uniquely via Tier 1 (e.g. genuinely ambiguous — see §0) but would resolve via the legacy /api/ps field → Tier 2 still gets a chance per-task.


What Tier 1 actually fixes (being honest about it, per §0): it does not resolve the case of two currently-loaded, different digests genuinely sharing one context size — no source available to ContextTrackingService can, since task lines carry no port. What it fixes is every case where the old code's single failure mode was trusting a /api/ps.ContextLength value that was missing, stale, or invalidated by the same model reloading at a different context size over time (confirmed in the log analysis: the same digest was observed loaded at -c 4096 and separately at -c 131072 across different sessions) — Tier 1's MaxContext always comes from the specific load event's own launch flag, so it can't go stale the way a cached API field can.


________________


6. Persistence ("Sticky" Attribution) — Fixes the Orphan-Line Bug
Today, StatusTextHelper.BuildMiniModelContextLines re-derives attribution from scratch on every call from (models, samples), with no memory of prior calls. That's why the models.Count == 0 branch has to make a binary choice — dump everything as unlabeled or drop it — it has no record of what it knew a moment ago.


This design moves attribution and its persistence into ContextTrackingService, which already owns per-task state with its own lifecycle (_tasks, StaleAfter). Once TryAttributeTask sets state.AttributedModelName, it is never re-derived and never cleared except when the task itself is pruned by the existing StaleAfter mechanism in GetSnapshot. Concretely:


* A model unloads → it disappears from models → activeDigests no longer contains it → Tier 1 can't be attempted fresh for new tasks against it. But any task already attributed before the unload keeps its AttributedModelName untouched, because TryAttributeTask's first line (if state.AttributedModelName is not null: return) short-circuits before any of that matters.
* ContextWindowSample.ModelName therefore stays populated for the task's full displayed lifetime (through StaleAfter), regardless of what models looks like on any given refresh.
* StatusTextHelper.BuildMiniModelContextLines no longer needs models at all (§8) — it groups already-labeled samples by ModelName, full stop. The models.Count == 0 branch and BuildUnlabeledTaskLines-as-global-fallback disappear structurally, not just behaviorally — there's no code path left that can treat "API had nothing to say right now" as "forget every label I already knew."


Update the class doc-comment on ContextTrackingService — it currently says a finalized task's line "remains visible for as long as its model is loaded"; that's no longer accurate. It should say the line remains visible for up to StaleAfter regardless of whether the model is still loaded, since attribution is cached independently of the model's current load state.


________________


7. Changes to ContextTrackingService.cs — Concrete Edit Points
1. Add the four new regex fields from §1.2 and the DigestHexRegex from §2, placed after the existing ReleaseTokensRegex field.
2. Add _runnerRegistry (§3.1) as a new private field, next to _tasks.
3. Add the two new fields to the private ContextTaskState class (§3.2).
4. In OnLogLineReceived, insert the new gate check (§1.1) immediately after the existing IsNullOrWhiteSpace guard and before the existing releaseMatch block:


if (line.Contains("starting llama-server", StringComparison.OrdinalIgnoreCase))


{


    TryRegisterRunnerLoad(line);   // new private method, §1.2 + §4


    return;


}


5. Add the new private method TryRegisterRunnerLoad(string line) implementing §1.2/§4, writing under _syncRoot and applying the retention cap from §4.
6. Add the new private method TryAttributeTask(ContextTaskState state, IReadOnlyList<OllamaModelSnapshot> models) implementing §5.
7. Change GetSnapshot() → GetSnapshot(IReadOnlyList<OllamaModelSnapshot> models). Inside the existing lock, after the stale-task removal loop and before the final .Select(BuildSample) projection, add a pass that calls TryAttributeTask(task.Value, models) for each remaining task.
8. Update BuildSample to copy state.AttributedModelDigest / state.AttributedModelName onto the new ContextWindowSample.ModelDigest / ModelName fields.
9. Update the class-level doc-comment per §6's last paragraph.


________________


8. Changes to StatusTextHelper.cs — Concrete Edit Points
1. Change the signature: BuildMiniModelContextLines(IReadOnlyList<OllamaModelSnapshot> models, IReadOnlyList<ContextWindowSample> samples) → BuildMiniModelContextLines(IReadOnlyList<ContextWindowSample> samples). The models parameter is no longer needed — every sample now already carries its own ModelName (or null) from ContextTrackingService.
2. Remove: the models.Count == 0 branch, soleUnmeasured computation, modelsWithContext, and the whole per-sample matches loop that compared model.ContextLength == sample.SlotTokens. All of that logic has moved into ContextTrackingService.TryAttributeTask.
3. Replace with: group samples by ModelName (non-null) using the same AddToGroup / TopSample / BuildTopTaskDetail helpers already present (unchanged — they operate on samples, not on the attribution mechanism, so they carry over as-is). Samples with ModelName == null go through the existing BuildUnlabeledTaskLines — but now this path is only reached for a task that has genuinely never yet resolved via either tier (a transient state, not the systemic fallback it is today).
4. Update the method's doc-comment: remove the sentence about matching n_ctx_slot against context_length; note instead that attribution now arrives pre-resolved on each ContextWindowSample, and that an unlabeled line means "not yet attributed" rather than "ambiguous by design."
5. Leave BuildContextSummary untouched — it never did context-size matching in the first place (it lists samples by task id regardless of model), so it's unaffected by this change and requires no edits.


________________


9. Cold-Start Limitation & Suggested Mitigation
If the monitor starts after models are already loaded, _runnerRegistry starts empty — the app only observes starting llama-server lines going forward from whenever tailing begins, not historical ones from before it started (tailing mechanism is out of scope to change here). Until each already-running model's next natural load event (keep-alive expiry + reload, or an explicit unload/reload), Tier 1 has nothing to match for it, and attribution for tasks against those pre-existing runners falls through entirely to Tier 2 — i.e., exactly today's behavior, with exactly today's limitations, until the registry catches up on its own.


Suggested mitigation (not designed in detail here, per request): a one-time WMI query at startup —


SELECT ProcessId, CommandLine, CreationDate FROM Win32_Process WHERE Name='llama-server.exe'


— run through the same §1.2 field regexes against CommandLine, with LoadedAt taken from CreationDate, to pre-seed _runnerRegistry immediately instead of waiting for a reload. Flagged as a follow-up enhancement, not required for this design to be correct (it degrades gracefully to Tier 2 without it).


________________


10. Known Residual Limitations
* Two currently-loaded, different digests sharing one context size: genuinely unresolvable from task-level log lines alone (no port tag on slot/srv lines) — see §0/§5. Only a per-port live source (/slots, if confirmed available — see below) or an OS-level correlation (WMI + a way to tie a specific in-flight request to a specific port) could close this fully.
* Task-ID collisions across concurrent runners: task numbering in the slot/srv lines appears to be a per-runner-process-local counter (observed values climb steadily within one long-lived runner's session). If two different runners are ever concurrently active, their task IDs could collide, since nothing in those lines is runner-scoped. ContextTrackingService's _tasks dictionary is keyed on the raw task id with no runner disambiguation — this is a pre-existing characteristic of the current design, not something introduced here, but worth flagging since it's adjacent to this work and not solved by it.
* /slots endpoint: worth testing (GET http://127.0.0.1:<port>/slots against a port captured from §1), not confirmed available in this Ollama/llama.cpp build, and not load-bearing for this design. If it turns out to be enabled, it would be a straightforwardly better source for live per-task usage than log-scraping, and could also resolve the two limitations above (since it's queried per-port, it's inherently unambiguous) — but that would be a separate follow-up design, not a prerequisite for this one.
* Port reuse over long uptimes: mitigated by keying the registry as a list plus "latest entry per active digest" (§3.1, §5) rather than by port directly.


________________


11. Dependency Checklist (verify before implementing)
* Confirm OllamaModelSnapshot exposes a digest property populated from /api/ps's digest field (name may differ from Digest — substitute accordingly).
* Confirm /api/ps's digest format (sha256:<hex> assumed) — the normalizer in §2 tolerates either separator, so this only matters if the format turns out to be something else entirely.
* Confirm what raw text IOllamaLogService.LogLineReceived delivers for the starting llama-server line (escaped vs. unescaped) — the regexes in §1.2 are written to be indifferent to this, but worth a one-time sanity check against a real captured line.
* Confirm ContextWindowSample's actual declaration style (record vs. class, init vs. set) to match §3.3's additions to existing convention.
12. Summary of Required Call-Site Updates
* Every caller of ContextTrackingService.GetSnapshot() must be updated to pass the current IReadOnlyList<OllamaModelSnapshot> (the same list already fetched from /api/ps for other purposes elsewhere in the app).
* Every caller of StatusTextHelper.BuildMiniModelContextLines(models, samples) must drop the models argument.
* No changes required to any caller of BuildContextSummary.
13. Implementation Results and Verification Findings (added 2026-08-28)
Implemented as designed. Checklist verdicts, verified against the live system:
1. **OllamaModelSnapshot / OllamaPsModelResponse:** neither exposes a digest property, and the app never mapped the /api/ps `digest` field. No mapping needed - see finding 2 for why.
2. **/api/ps digest format (live probe, qwen3.8:27b184K active):** `"digest": "cd577b2f5c0c..."` - **bare 64-hex, no `sha256:` prefix. And it differs from the manifest model-layer digest (`f5f1dd89...`)** - the /api/ps digest is a different identifier, not the GGUF blob digest. Matching it against the local store would be impossible, which confirms the §3.1 manifest-bridge route as the only viable join. (Also confirmed live: `context_length` == the running runner's `-c`, 188416.)
3. **Log-line escaping (real captured line, `%LOCALAPPDATA%\Ollama\server.log`, Ollama-for-Windows build):** `cmd="C:\\Users\one_l\... --model C:\\Users\one_l\.ollama\models\blobs\sha256-<hex> --port 63995 ... -c 188416 ... --mmproj ... sha256-<hex> ..."` - doubled backslashes, unquoted spaces, single-line. All §1.2 regexes match it verbatim (verified by the real end-to-end test). This build adds `--spec-type draft-mtp`, `--spec-draft-n-max`, `--spec-draft-backend-sampling`, `--load-mode none` flags - inert to the regexes, and harmless to the `--model`/`-c` extraction.
4. **ContextWindowSample:** sealed record, `{ get; init; }` - the §3.3 fields were added in that exact style.
Implementation deltas from the draft, all inside the design's intent:
* **Store bridge lives in OllamaModelStore** (new `GetModelDigest` + `NormalizeDigest` + `GetManifestPathCandidates`), not in the app's path resolution: `library/<name>` and `manifests/registry/.../library/<name>` under the default `~/.ollama/models` or `~\.ollama\models` root, `latest` tag fallback. Tests drive a temporary store via the internal constructor `OllamaModelStore(roots)`.
* **Retention valve:** fixed to a 500-entry cap dropping oldest first (§9 left the number open).
* **Tier 2 + the sole-unmeasured fallback moved verbatim into ContextTrackingService** (per §7); observable behavior unchanged from today's StatusTextHelper logic.
* **GetSnapshot() now takes the `IReadOnlyList<OllamaModelSnapshot>`**; the only production caller is OllamaStatusService (its `GetSnapshotAsync` passes the models it already built). `BuildMiniModelContextLines` is now samples-only; its only production caller is MainWindowViewModel (updated).
Test state: full suite **77/77 green** (was 62). New coverage: store bridge (name/tag layout, latest fallback, digest normalization, missing-manifest), attribution (Tier 1 with stale ps context, per-model routing, Tier 2 fallback, sticky across unload, retention-cap eviction, incomplete-line rejection, ambiguity fall-through) and one real end-to-end: real local store + real captured line + deliberately stale 4096 ps context_length - resolvable only by the Tier 1 bridge, exactly the reported bug.


14. Root Cause Found in the Production Log (added 2026-08-28, after S13)
Captured evidence from %LOCALAPPDATA%\Ollama\server.log (rotated: server-2.log held the buggy window, 2026-08-28 05:06-05:41):


* Three runner loads in the buggy window: sha256-3646b4c1 at -c 131072 (05:06:32), sha256-dde5aa3f (llama3.2-memory) at -c 4096 (05:06:56 and 05:11:26), sha256-f5f1dd89 (qwen3.8) at -c 188416 (05:16:28 onward).
* In the llama3.2 runner window (05:06:56-05:11:26) both models' runners emitted the SAME id and task id concurrently: id 0 | task 0 | new prompt, n_ctx_slot = 4096, task.n_tokens = 33 AND, in the same window, id 0 | task 0 | new prompt, n_ctx_slot = 131072, task.n_tokens = 26.
* In qwen-only windows the logged task ids are non-contiguous (0, 56, 129, ..., 33774 in one; 0, 298, 645, 1290, 1891 in another), i.e. the counter advances faster than the parsed lines.
* The slot id (id 0) is per-runner too: it disambiguates nothing.


Conclusion: the task id in a log line is a per-runner counter that restarts at 0 for every concurrently loaded model. ContextTrackingService was keyed by task id alone (Dictionary<int, ContextTaskState>), so every model's runner wrote into the SAME state entry for a given id: their SlotTokens/UsedTokens/TokensPerSecond overwrote each other, and attribution (resolved once from whichever ctx value happened to be last) attached the wrong model name to the mixed stats - exactly the reported symptom (a llama3.2-memory line showing > 4k context and another model's t/s).


Fix (implemented in the same change set):


* Task state is now keyed by (n_ctx_slot, task id). A line WITH n_ctx_slot (its "new prompt" line) targets the (slot, task id) state; an unbound orphan state (timing/token lines seen before the prompt line) is folded into it when the prompt line arrives.
* A line WITHOUT n_ctx_slot (print_timing, token_count, slot release) targets that task id's most recently active state when several states share the id (colliding runners). Log lines carry no field identifying the owning runner; when both colliding id-0 tasks generate in the same instant one value may misroute for one line and self-correct on the task's next line. Strictly better than the old permanently-merged behavior.
* Attribution (tiers 1/2, sticky) is unchanged and more accurate: each state now carries the ctx of its own model, so a state can no longer be attributed from a colliding model's ctx.


New tests: Collision_SameTaskIdAcrossTwoModels_AreTrackedSeparately and Collision_ReleaseLine_TerminatesTheMostRecentlyActiveStateOnly (ContextTrackingServiceAttributionTests).


15. Display Selection Change: Recency Over Magnitude (added 2026-08-28, after S14)
Reported live bug: with multiple concurrent tasks on the same model, the displayed task was whichever had the highest UsedPercent - so an idle high-context session kept being shown over an actively-generating lower-context one, repeatedly.


* ContextWindowSample gains a LastUpdated property ({ get; init; }); ContextTrackingService.BuildSample populates it from the task state's LastUpdated.
* StatusTextHelper.TopSample now orders by LastUpdated first, then UsedPercent, then TokensPerSecond, then TaskId - recency of log activity outranks magnitude of usage. S5.3's "helpers carry over unchanged" note is superseded for TopSample only; BuildTopTaskDetail and BuildUnlabeledTaskLines remain unchanged.
* Inter-model line order still keys off the selected (top) sample's UsedPercent, so it tracks what is actually displayed.
* ContextTrackingService.FindTaskState (the ctx-collision target for ctx-less lines) orders candidates by a new per-state monotonic ActivitySeq counter (assigned under the sync lock on every state update) instead of LastUpdated, so same-tick updates resolve deterministically to the most recently touched state.


Tests: MultipleSamplesPerModel_ShownOnce_AsMostRecentlyActiveTask (renamed/updated with explicit LastUpdated values) and TopSelection_PrefersRecentActivity_OverHigherUsage (idle 79.6% 40t/s 30 minutes old vs active 20% 15t/s now - active wins).

Full suite: 80/80 green, stable across six consecutive runs.