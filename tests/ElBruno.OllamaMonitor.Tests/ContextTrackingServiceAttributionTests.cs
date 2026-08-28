using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Models;
using ElBruno.OllamaMonitor.Ollama;
using ElBruno.OllamaMonitor.Services;

namespace ElBruno.OllamaMonitor.Tests;

/// <summary>
/// Tests ContextTrackingService model attribution: Tier 1 (runner registry from
/// "starting llama-server" lines, bridged to active models via the local store's
/// weight digests), Tier 2 (legacy /api/ps context_length), sticky persistence across
/// model unload, and the registry retention cap. A temporary model store is built for
/// each test, so no real Ollama installation is required.
/// </summary>
public sealed class ContextTrackingServiceAttributionTests : IDisposable
{
    private const string DigestA = "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1";
    private const string DigestB = "b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2";
    private const string DigestX = "c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3";
    private const string MmprojDigest = "d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4";

    private readonly string _root;
    private readonly OllamaLogService _logService;
    private readonly ContextTrackingService _sut;

    public ContextTrackingServiceAttributionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"ContextAttributionTests_{Guid.NewGuid():N}");
        WriteManifest("alphamodel/latest", DigestA);
        WriteManifest("betamodel/latest", DigestB);
        var store = new OllamaModelStore(_root);
        var diagnostics = new DiagnosticsLogService(Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests"));
        _logService = new OllamaLogService(diagnostics);
        _sut = new ContextTrackingService(_logService, store);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _logService.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void WriteManifest(string relativeName, string modelDigest)
    {
        var manifestPath = Path.Combine(_root, "manifests", "registry.ollama.ai", "library", relativeName);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var json = $$"""{"config":{"digest":"sha256:{{MmprojDigest}}","size":1},"layers":[{"mediaType":"application/vnd.ollama.image.projector","digest":"sha256:{{MmprojDigest}}","size":1},{"mediaType":"application/vnd.ollama.image.model","digest":"sha256:{{modelDigest}}","size":1}]}""";
        File.WriteAllText(manifestPath, json);
    }

    private void Inject(string line) => _logService.OnOwnedProcessOutput(line);

    private static OllamaModelSnapshot Model(string name, long? contextLength) =>
        new() { Name = name, ContextLength = contextLength };

    /// <summary>
    /// Mirrors the real captured format: quoted cmd, doubled backslashes,
    /// digest/port/max-context/mmproj present.
    /// </summary>
    private static string RunnerLine(string digest, int port, int maxContext) =>
        "time=2026-08-27T22:05:33.292-05:00 level=INFO source=llama_server.go:433 msg=\"starting llama-server\" " +
        $"cmd=\"C:\\\\Users\\\\tester\\\\lib\\\\llama-server.exe --model C:\\\\Users\\\\tester\\\\.ollama\\\\models\\\\blobs\\\\sha256-{digest} --port {port} --host 127.0.0.1 --no-webui --offline -c {maxContext} -np 1 --mmproj C:\\\\Users\\\\tester\\\\.ollama\\\\models\\\\blobs\\\\sha256-{MmprojDigest}\"";

    [Fact]
    public void Tier1_ResolvesTask_WhenApiContextLengthIsStale()
    {
        // ps still reports a stale context_length (4096) but the runner actually loaded -c 131072.
        Inject(RunnerLine(DigestA, 63995, 131072));
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 131072, task.n_tokens = 100");

        var sample = Assert.Single(_sut.GetSnapshot([Model("alphamodel:latest", 4096)]));

        Assert.Equal("alphamodel:latest", sample.ModelName);
        Assert.Equal($"sha256:{DigestA}", sample.ModelDigest);
    }

    [Fact]
    public void Tier1_AttributesEachTask_ToItsOwnRunnerLoad()
    {
        Inject(RunnerLine(DigestA, 63995, 4096));
        Inject(RunnerLine(DigestB, 64001, 8192));
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 4096, task.n_tokens = 10");
        Inject("slot   operator(): id 1 | task 2 | new prompt, n_ctx_slot = 8192, task.n_tokens = 20");

        var samples = _sut.GetSnapshot([Model("alphamodel:latest", 4096), Model("betamodel:latest", 8192)])
            .OrderBy(sample => sample.TaskId)
            .ToList();

        Assert.Equal("alphamodel:latest", samples[0].ModelName);
        Assert.Equal($"sha256:{DigestA}", samples[0].ModelDigest);
        Assert.Equal("betamodel:latest", samples[1].ModelName);
        Assert.Equal($"sha256:{DigestB}", samples[1].ModelDigest);
    }

    [Fact]
    public void Tier2_FallsThrough_WhenRunnerEvidenceIsMissing()
    {
        // Cold start: the model's runner load was never logged. Only beta's runner is known.
        Inject(RunnerLine(DigestB, 64001, 8192));
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 4096, task.n_tokens = 10");

        var sample = Assert.Single(_sut.GetSnapshot([Model("alphamodel:latest", 4096), Model("betamodel:latest", 8192)]));

        Assert.Equal("alphamodel:latest", sample.ModelName); // via the legacy context_length match
        Assert.Equal($"sha256:{DigestA}", sample.ModelDigest);
    }

    [Fact]
    public void Sticky_AttributionSurvivesModelUnload()
    {
        Inject(RunnerLine(DigestA, 63995, 4096));
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 4096, task.n_tokens = 10");

        var resolved = Assert.Single(_sut.GetSnapshot([Model("alphamodel:latest", 4096)]));
        Assert.Equal("alphamodel:latest", resolved.ModelName);

        // The model unloads between refreshes: the line keeps its label.
        var afterUnload = Assert.Single(_sut.GetSnapshot(Array.Empty<OllamaModelSnapshot>()));
        Assert.Equal("alphamodel:latest", afterUnload.ModelName);
        Assert.Equal($"sha256:{DigestA}", afterUnload.ModelDigest);
    }

    [Fact]
    public void Retention_OldestRunnerEntriesEvictFirst_NewTaskCannotResolve()
    {
        Inject(RunnerLine(DigestA, 63995, 4096));
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 4096, task.n_tokens = 10");

        var resolved = _sut.GetSnapshot([Model("alphamodel:latest", null), Model("betamodel:latest", 8192)])
            .Single(sample => sample.TaskId == 1);
        Assert.Equal("alphamodel:latest", resolved.ModelName);

        // Flood the registry past its cap so the oldest entry (DigestA) is trimmed away.
        for (var i = 0; i < 505; i++)
        {
            Inject(RunnerLine(DigestX, 65000 + i, 8192));
        }

        Inject("slot   operator(): id 0 | task 2 | new prompt, n_ctx_slot = 4096, task.n_tokens = 20");

        var snapshots = _sut.GetSnapshot([Model("alphamodel:latest", null), Model("betamodel:latest", 8192)])
            .OrderBy(sample => sample.TaskId)
            .ToList();
        Assert.Equal("alphamodel:latest", snapshots[0].ModelName); // task 1 already resolved - sticky
        Assert.Null(snapshots[1].ModelName); // task 2: registry no longer holds DigestA
    }

    [Fact]
    public void RunnerLineWithoutPort_IsNotRegistered()
    {
        var line = "msg=\"starting llama-server\" cmd=\"llama-server.exe --model blob/sha256-" + DigestA + " --host 127.0.0.1 -c 4096\"";
        Inject(line);
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 4096, task.n_tokens = 10");

        var sample = Assert.Single(_sut.GetSnapshot([Model("alphamodel:latest", null), Model("betamodel:latest", 8192)]));

        Assert.Null(sample.ModelName);
    }

    [Fact]
    public void AmbiguousTier1_FallsThrough_AndStaysUnresolved()
    {
        // Two runners, both -c 4096, both active: the task's slot cannot say which runner it is.
        Inject(RunnerLine(DigestA, 63995, 4096));
        Inject(RunnerLine(DigestB, 64001, 4096));
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 4096, task.n_tokens = 10");

        var sample = Assert.Single(_sut.GetSnapshot([Model("alphamodel:latest", 4096), Model("betamodel:latest", 4096)]));

        Assert.Null(sample.ModelName);
    }

    [Fact]
    public void Collision_SameTaskIdAcrossTwoModels_AreTrackedSeparately()
    {
        // Both models are loaded concurrently; their per-runner task counters both
        // start at 0, so "task 0" exists on two models at once (observed in the real
        // server log: three models' runners all emitted id 0 | task 0 lines).
        Inject(RunnerLine(DigestA, 63995, 188416));
        Inject(RunnerLine(DigestB, 64001, 4096));
        Inject("slot   operator(): id 0 | task 0 | new prompt, n_ctx_slot = 188416, task.n_tokens = 20");
        Inject("slot   operator(): id 0 | task 0 | new prompt, n_ctx_slot = 4096, task.n_tokens = 33");
        Inject("slot print_timing: id 0 | task 0 | n_gen = 628, tg = 208.95 t/s");

        var samples = _sut.GetSnapshot([Model("alphamodel:latest", 188416), Model("betamodel:latest", 4096)])
            .ToList();

        Assert.Equal(2, samples.Count);
        var alpha = samples.Single(sample => sample.SlotTokens == 188416);
        var beta = samples.Single(sample => sample.SlotTokens == 4096);

        Assert.Equal(20, alpha.UsedTokens);
        Assert.Null(alpha.TokensPerSecond);
        Assert.Equal(33, beta.UsedTokens);
        Assert.Equal(208.95, beta.TokensPerSecond!.Value, 3); // most recently active state for id 0

        Assert.Equal("alphamodel:latest", alpha.ModelName);
        Assert.Equal("betamodel:latest", beta.ModelName);
    }

    [Fact]
    public void Collision_ReleaseLine_TerminatesTheMostRecentlyActiveStateOnly()
    {
        Inject("slot   operator(): id 0 | task 0 | new prompt, n_ctx_slot = 4096, task.n_tokens = 33");
        Inject("slot print_timing: id 0 | task 0 | tg = 208.95 t/s");
        Inject("slot   operator(): id 0 | task 0 | new prompt, n_ctx_slot = 188416, task.n_tokens = 20");
        Inject("slot print_timing: id 0 | task 0 | n_gen = 10, tg = 42.78 t/s");
        Inject("slot release: id 0 | task 0 | stop processing: n_tokens = 90000, truncated = 0");

        var samples = _sut.GetSnapshot(Array.Empty<OllamaModelSnapshot>()).ToList();

        Assert.Equal(2, samples.Count);
        var finalized = samples.Single(sample => sample.SlotTokens == 188416);
        var untouched = samples.Single(sample => sample.SlotTokens == 4096);

        Assert.Equal(90000, finalized.UsedTokens);
        Assert.Null(finalized.TokensPerSecond);
        Assert.Equal(33, untouched.UsedTokens);
        Assert.Equal(208.95, untouched.TokensPerSecond!.Value, 3);
    }
}
