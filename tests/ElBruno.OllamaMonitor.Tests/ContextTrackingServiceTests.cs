using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Services;

namespace ElBruno.OllamaMonitor.Tests;

/// <summary>
/// Tests for ContextTrackingService per-task parsing of Ollama server log lines.
/// Seam: lines are injected via OllamaLogService.OnOwnedProcessOutput (internal,
/// accessible via InternalsVisibleTo), which raises LogLineReceived directly —
/// no real Ollama process or file I/O is used.
/// </summary>
public sealed class ContextTrackingServiceTests : IDisposable
{
    private readonly OllamaLogService _logService;
    private readonly ContextTrackingService _sut;

    public ContextTrackingServiceTests()
    {
        var diagnostics = new DiagnosticsLogService(
            Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests"));
        _logService = new OllamaLogService(diagnostics);
        _sut = new ContextTrackingService(_logService);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _logService.Dispose();
    }

    private void Inject(string line) => _logService.OnOwnedProcessOutput(line);

    [Fact]
    public void GetSnapshot_NoLines_ReturnsEmpty()
    {
        Assert.Empty(_sut.GetSnapshot());
    }

    [Fact]
    public void NewPromptLine_PopulatesTaskSlotTokensAndUsedTokens()
    {
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 188416, task.n_tokens = 1234");

        var sample = Assert.Single(_sut.GetSnapshot());
        Assert.Equal(1, sample.TaskId);
        Assert.Equal(188416, sample.SlotTokens);
        Assert.Equal(1234, sample.UsedTokens);
        Assert.Null(sample.TokensPerSecond);
        Assert.Equal(1234.0 / 188416 * 100, sample.UsedPercent!.Value, 5);
    }

    [Fact]
    public void PrintTimingLine_PopulatesTokensPerSecond()
    {
        Inject("slot print_timing: id 0 | task 2 | tg = 12.34 t/s");

        var sample = Assert.Single(_sut.GetSnapshot());
        Assert.Equal(2, sample.TaskId);
        Assert.Equal(12.34, sample.TokensPerSecond!.Value, 3);
        Assert.Null(sample.SlotTokens);
        Assert.Null(sample.UsedTokens);
        Assert.Null(sample.UsedPercent);
    }

    [Fact]
    public void UsedPercent_OnlyComputedWhenBothTokenCountsKnown()
    {
        Inject("slot print_timing: id 0 | task 1 | tg = 12.34 t/s");
        var before = Assert.Single(_sut.GetSnapshot());
        Assert.Null(before.UsedPercent);

        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 2048, task.n_tokens = 512");
        var after = Assert.Single(_sut.GetSnapshot());
        Assert.Equal(25, after.UsedPercent!.Value, 5);
        Assert.Equal(12.34, after.TokensPerSecond!.Value, 3);
    }

    [Fact]
    public void MultipleTasks_AreTrackedByKey()
    {
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 188416, task.n_tokens = 100");
        Inject("slot   operator(): id 1 | task 5 | new prompt, n_ctx_slot = 8192, task.n_tokens = 200");
        Inject("slot print_timing: id 2 | task 5 | tg = 7.5 t/s");

        var snapshots = _sut.GetSnapshot().OrderBy(sample => sample.TaskId).ToList();
        Assert.Equal(2, snapshots.Count);

        var task1 = snapshots[0];
        Assert.Equal(1, task1.TaskId);
        Assert.Equal(188416, task1.SlotTokens);
        Assert.Equal(100, task1.UsedTokens);
        Assert.Null(task1.TokensPerSecond);

        var task5 = snapshots[1];
        Assert.Equal(5, task5.TaskId);
        Assert.Equal(8192, task5.SlotTokens);
        Assert.Equal(200, task5.UsedTokens);
        Assert.Equal(7.5, task5.TokensPerSecond!.Value, 3);
    }

    [Fact]
    public void LaterLine_UpdatesValuesForSameTask()
    {
        Inject("slot   operator(): id 0 | task 1 | new prompt, n_ctx_slot = 4096, task.n_tokens = 64");
        Inject("slot   operator(): id 0 | task 1 | token_count, task.n_tokens = 256");
        Inject("slot print_timing: id 0 | task 1 | tg = 99.9 t/s");

        var sample = Assert.Single(_sut.GetSnapshot());
        Assert.Equal(4096, sample.SlotTokens);
        Assert.Equal(256, sample.UsedTokens);
        Assert.Equal(99.9, sample.TokensPerSecond!.Value, 3);
        Assert.Equal(6.25, sample.UsedPercent!.Value, 5);
    }

    [Fact]
    public void UnrelatedLines_AreIgnored()
    {
        Inject("time=2026-08-26 level=INFO msg=\"listening on=[::]:11434\"");
        Inject("level=WARN msg=\"unknown flag\"");
        Inject("");

        Assert.Empty(_sut.GetSnapshot());
    }

    [Fact]
    public void LineWithoutTaskId_IsIgnoredEvenWithKnownFields()
    {
        Inject("some diagnostic: n_ctx_slot = 4096, task.n_tokens = 64");

        Assert.Empty(_sut.GetSnapshot());
    }

    [Fact]
    public void Dispose_UnsubscribesFromLogEvents()
    {
        _sut.Dispose();
        Inject("slot   operator(): id 0 | task 3 | new prompt, n_ctx_slot = 1024, task.n_tokens = 512");

        Assert.Empty(_sut.GetSnapshot());
    }
}
