using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Models;
using ElBruno.OllamaMonitor.Ollama;
using ElBruno.OllamaMonitor.Services;

namespace ElBruno.OllamaMonitor.Tests;

/// <summary>
/// End-to-end attribution against the REAL local Ollama store (default roots) and a
/// REAL captured "starting llama-server" line from this machine's server log. Silently
/// skips when the model is not installed (e.g. on a clean CI runner).
/// </summary>
public sealed class RealStoreAttributionTests
{
    private const string ModelName = "qwen3.8:27b184K";

    /// <summary>
    /// Verbatim shape of the real line (doubled backslashes, quoted cmd), captured from
    /// %LOCALAPPDATA%\Ollama\server.log on this machine.
    /// </summary>
    private const string RealLine =
        "time=2026-08-27T22:05:33.292-05:00 level=INFO source=llama_server.go:433 msg=\"starting llama-server\" " +
        "cmd=\"C:\\\\Users\\\\one_l\\\\AppData\\\\Local\\\\Programs\\\\Ollama\\\\lib\\\\ollama\\\\llama-server.exe " +
        "--model C:\\\\Users\\\\one_l\\\\.ollama\\\\models\\\\blobs\\\\sha256-f5f1dd8920d417aac2718b0bda3403da274301efdd6760b4f0f4b864ff2ad57d " +
        "--port 63995 --host 127.0.0.1 --no-webui --offline -c 188416 -np 1 --log-verbosity 4 --no-log-prefix " +
        "--no-log-timestamps --no-jinja --chat-template chatml " +
        "--mmproj C:\\\\Users\\\\one_l\\\\.ollama\\\\models\\\\blobs\\\\sha256-ac3714bfdddeca31351f2752bf1a63f266f4df87c0b68c895e44945ca704448e " +
        "--spec-type draft-mtp\"";

    [Fact]
    public void RealStore_RealLogLine_AttributesTheRealModel_ViaTier1Only()
    {
        var store = new OllamaModelStore();
        var digest = store.GetModelDigest(ModelName);
        if (digest is null)
        {
            return; // model not installed on this machine - nothing to verify end-to-end
        }

        var diagnostics = new DiagnosticsLogService(Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests"));
        var logService = new OllamaLogService(diagnostics);
        using var sut = new ContextTrackingService(logService, store);

        logService.OnOwnedProcessOutput(RealLine);
        logService.OnOwnedProcessOutput("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 188416, task.n_tokens = 90559, n_keep = 4");

        // Deliberately stale /api/ps context_length (the reported "stuck at 4096" symptom):
        // the legacy Tier 2 match cannot resolve this, so attribution requires the Tier 1
        // runner-registry-to-manifest bridge.
        var models = new[]
        {
            new OllamaModelSnapshot { Name = ModelName, ContextLength = 4096 },
        };
        var sample = Assert.Single(sut.GetSnapshot(models));

        Assert.Equal(ModelName, sample.ModelName);
        Assert.Equal(digest, sample.ModelDigest);
    }
}
