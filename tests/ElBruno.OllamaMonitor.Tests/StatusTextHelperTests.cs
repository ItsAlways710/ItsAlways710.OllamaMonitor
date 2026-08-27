using ElBruno.OllamaMonitor.Helpers;
using ElBruno.OllamaMonitor.Models;

namespace ElBruno.OllamaMonitor.Tests;

public sealed class StatusTextHelperTests
{
    [Fact]
    public void BuildProcessorDisplay_NullSize_Returns100PercentCpu()
    {
        var result = StatusTextHelper.BuildProcessorDisplay(null, null);

        Assert.Equal("100% CPU", result);
    }

    [Fact]
    public void BuildProcessorDisplay_NullSizeVram_Returns100PercentCpu()
    {
        var result = StatusTextHelper.BuildProcessorDisplay(4_000_000_000L, null);

        Assert.Equal("100% CPU", result);
    }

    [Fact]
    public void BuildProcessorDisplay_ZeroSizeVram_Returns100PercentCpu()
    {
        var result = StatusTextHelper.BuildProcessorDisplay(4_000_000_000L, 0L);

        Assert.Equal("100% CPU", result);
    }

    [Fact]
    public void BuildProcessorDisplay_SizeVramEqualsSize_Returns100PercentGpu()
    {
        var size = 4_000_000_000L;
        var result = StatusTextHelper.BuildProcessorDisplay(size, size);

        Assert.Equal("100% GPU", result);
    }

    [Fact]
    public void BuildProcessorDisplay_SizeVramExceedsSize_Returns100PercentGpu()
    {
        var result = StatusTextHelper.BuildProcessorDisplay(4_000_000_000L, 5_000_000_000L);

        Assert.Equal("100% GPU", result);
    }

    [Fact]
    public void BuildProcessorDisplay_PartialSizeVram_ReturnsSplit()
    {
        // 3 GB VRAM out of 4 GB total → 75% GPU, 25% CPU
        var size = 4_000_000_000L;
        var sizeVram = 3_000_000_000L;
        var result = StatusTextHelper.BuildProcessorDisplay(size, sizeVram);

        Assert.Equal("25% CPU · 75% GPU", result);
    }

    [Fact]
    public void BuildProcessorDisplay_ZeroSize_Returns100PercentCpu()
    {
        var result = StatusTextHelper.BuildProcessorDisplay(0L, 0L);

        Assert.Equal("100% CPU", result);
    }

    [Fact]
    public void BuildMiniModelContextLines_NoSamples_ReturnsUnavailable()
    {
        var models = new[] { new OllamaModelSnapshot { Name = "qwen3.8:27b184K", ContextLength = 188416 } };

        var result = StatusTextHelper.BuildMiniModelContextLines(models, Array.Empty<ContextWindowSample>());

        Assert.Equal([ "Context: Unavailable" ], result);
    }

    [Fact]
    public void BuildMiniModelContextLines_SingleModel_AttributesTasksToIt()
    {
        var models = new[] { new OllamaModelSnapshot { Name = "qwen3.8:27b184K", ContextLength = 188416 } };
        var samples = new[]
        {
            new ContextWindowSample { TaskId = 1, SlotTokens = 188416, UsedTokens = 90559, UsedPercent = 90559 * 100.0 / 188416, TokensPerSecond = 33.08 }
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(models, samples);

        Assert.Equal([ "qwen3.8:27b184K - 90559/188416 - 48.1% - 33.08t/s" ], result);
    }

    [Fact]
    public void BuildMiniModelContextLines_TwoModels_AttributesEachByContextLengthAndOrdersByPercent()
    {
        var models = new[]
        {
            new OllamaModelSnapshot { Name = "qwen3.8:27b184K", ContextLength = 188416 },
            new OllamaModelSnapshot { Name = "llama3.2-memory:latest", ContextLength = 4096 }
        };
        var samples = new[]
        {
            new ContextWindowSample { TaskId = 1, SlotTokens = 188416, UsedTokens = 90559, UsedPercent = 90559 * 100.0 / 188416, TokensPerSecond = 33.08 },
            new ContextWindowSample { TaskId = 2, SlotTokens = 4096, UsedTokens = 3100, UsedPercent = 3100 * 100.0 / 4096, TokensPerSecond = 41.2 }
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(models, samples);

        Assert.Equal(
            [
                "llama3.2-memory:latest - 3100/4096 - 75.7% - 41.2t/s",
                "qwen3.8:27b184K - 90559/188416 - 48.1% - 33.08t/s"
            ],
            result);
    }

    [Fact]
    public void BuildMiniModelContextLines_AmbiguousTwoTasks_OneLinePerTaskWithOwnStats()
    {
        var models = new[]
        {
            new OllamaModelSnapshot { Name = "alpha:latest", ContextLength = 4096 },
            new OllamaModelSnapshot { Name = "beta:latest", ContextLength = 4096 }
        };
        var samples = new[]
        {
            new ContextWindowSample { TaskId = 1, SlotTokens = 4096, UsedTokens = 2048, UsedPercent = 2048 * 100.0 / 4096, TokensPerSecond = 40.0 },
            new ContextWindowSample { TaskId = 2, SlotTokens = 4096, UsedTokens = 3100, UsedPercent = 3100 * 100.0 / 4096, TokensPerSecond = 41.2 }
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(models, samples);

        // Same context size as both models: names unknown, but the tasks are distinct,
        // so each gets its own line with its own stats (ordered by percent desc).
        Assert.Equal(
            [
                "3100/4096 - 75.7% - 41.2t/s",
                "2048/4096 - 50% - 40t/s"
            ],
            result);
    }

    [Fact]
    public void BuildMiniModelContextLines_NoLoadedModels_OneLinePerTask()
    {
        var samples = new[]
        {
            new ContextWindowSample { TaskId = 1, SlotTokens = 188416, UsedTokens = 90559, UsedPercent = 90559 * 100.0 / 188416, TokensPerSecond = 33.08 },
            new ContextWindowSample { TaskId = 2, SlotTokens = 4096, UsedTokens = 3100, UsedPercent = 3100 * 100.0 / 4096, TokensPerSecond = 41.2 }
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(Array.Empty<OllamaModelSnapshot>(), samples);

        Assert.Equal(
            [
                "3100/4096 - 75.7% - 41.2t/s",
                "90559/188416 - 48.1% - 33.08t/s"
            ],
            result);
    }

    [Fact]
    public void BuildMiniModelContextLines_LoadedModelWithoutTasks_GetsNoLine()
    {
        var models = new[]
        {
            new OllamaModelSnapshot { Name = "qwen3.8:27b184K", ContextLength = 188416 },
            new OllamaModelSnapshot { Name = "llama3.2-memory:latest", ContextLength = 4096 }
        };
        var samples = new[]
        {
            new ContextWindowSample { TaskId = 2, SlotTokens = 4096, UsedTokens = 3100, UsedPercent = 3100 * 100.0 / 4096, TokensPerSecond = 41.2 }
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(models, samples);

        Assert.Equal([ "llama3.2-memory:latest - 3100/4096 - 75.7% - 41.2t/s" ], result);
    }

    [Fact]
    public void BuildMiniModelContextLines_TaskWithoutLoadedModel_IsDropped()
    {
        var models = new[]
        {
            new OllamaModelSnapshot { Name = "qwen3.8:27b184K", ContextLength = 188416 },
            new OllamaModelSnapshot { Name = "llama3.2-memory:latest", ContextLength = 4096 }
        };
        var samples = new[]
        {
            // Leftover of a released model (8192 slot) that is no longer in /api/ps.
            new ContextWindowSample { TaskId = 7, SlotTokens = 8192, UsedTokens = 4000, UsedPercent = 48.83, TokensPerSecond = 50.0 },
            new ContextWindowSample { TaskId = 1, SlotTokens = 188416, UsedTokens = 90559, UsedPercent = 90559 * 100.0 / 188416, TokensPerSecond = 33.08 },
            new ContextWindowSample { TaskId = 2, SlotTokens = 4096, UsedTokens = 3100, UsedPercent = 3100 * 100.0 / 4096, TokensPerSecond = 41.2 }
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(models, samples);

        Assert.Equal(
            [
                "llama3.2-memory:latest - 3100/4096 - 75.7% - 41.2t/s",
                "qwen3.8:27b184K - 90559/188416 - 48.1% - 33.08t/s"
            ],
            result);
    }

    [Fact]
    public void BuildMiniModelContextLines_CompletedTask_ShowsFinalUsageWithoutSpeed()
    {
        var models = new[]
        {
            new OllamaModelSnapshot { Name = "qwen3.8:27b184K", ContextLength = 188416 },
            new OllamaModelSnapshot { Name = "llama3.2-memory:latest", ContextLength = 4096 }
        };
        var samples = new[]
        {
            new ContextWindowSample { TaskId = 1, SlotTokens = 188416, UsedTokens = 113271, UsedPercent = 113271 * 100.0 / 188416, TokensPerSecond = null },
            new ContextWindowSample { TaskId = 2, SlotTokens = 4096, UsedTokens = 3100, UsedPercent = 3100 * 100.0 / 4096, TokensPerSecond = null }
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(models, samples);

        Assert.Equal(
            [
                "llama3.2-memory:latest - 3100/4096 - 75.7%",
                "qwen3.8:27b184K - 113271/188416 - 60.1%"
            ],
            result);
    }


    [Fact]
    public void BuildContextSummary_MultipleTasks_PreservesPerTaskLinesWithIds()
    {
        var samples = new[]
        {
            new ContextWindowSample { TaskId = 1, SlotTokens = 1000, UsedTokens = 100, UsedPercent = 10.0, TokensPerSecond = null },
            new ContextWindowSample { TaskId = 2, SlotTokens = 1000, UsedTokens = 50, UsedPercent = 5.0, TokensPerSecond = 12.34 }
        };

        var result = StatusTextHelper.BuildContextSummary(samples);

        var expected = string.Join(
            Environment.NewLine,
            "task 1: 100 / 1,000 tokens · 10% used",
            "task 2: 50 / 1,000 tokens · 5% used · 12.34 t/s");
        Assert.Equal(expected, result);
    }
}
