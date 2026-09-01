using ItsAlways710.OllamaMonitor.Helpers;
using ItsAlways710.OllamaMonitor.Models;

namespace ItsAlways710.OllamaMonitor.Tests;

/// <summary>
/// Tests that StatusTextHelper builds Mini Monitor context lines named after each
/// attributed model (via the sample's pre-resolved ModelName), that at most one task
/// per model is shown (the most recently active one - recency of log activity
/// outranks magnitude of usage), and that unlabeled tasks (not yet
/// attributed - a transient state) keep their own line with their own stats.
/// Long model names are middle-ellipsized to fit the window's fixed width (stats
/// are never trimmed); a line's FullText keeps the unabridged name plus stats.
/// </summary>
public sealed class StatusTextHelperTests
{
    [Fact]
    public void EmptySamples_ReturnsUnavailable()
    {
        var result = StatusTextHelper.BuildMiniModelContextLines(Array.Empty<ContextWindowSample>());
        Assert.Equal(new[] { new MiniContextLine("Context: Unavailable", "Context: Unavailable") }, result);
    }

    [Fact]
    public void MultipleModels_EachGetOwnLabeledLine()
    {
        var samples = new[]
        {
            new ContextWindowSample { ModelName = "localfoo:latest", SlotTokens = 1000, UsedTokens = 200, UsedPercent = 20, TokensPerSecond = 10, TaskId = 1 },
            new ContextWindowSample { ModelName = "localfoo2:latest", SlotTokens = 1024, UsedTokens = 800, UsedPercent = 78.125, TaskId = 2 },
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(samples);

        Assert.Equal(new[]
        {
            new MiniContextLine("localfoo2:latest - 800/1024 - 78.1%", "localfoo2:latest - 800/1024 - 78.1%"),
            new MiniContextLine("localfoo:latest - 200/1000 - 20% - 10t/s", "localfoo:latest - 200/1000 - 20% - 10t/s"),
        }, result);
    }

    [Fact]
    public void MultipleSamplesPerModel_ShownOnce_AsMostRecentlyActiveTask()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new[]
        {
            new ContextWindowSample { ModelName = "localfoo:latest", SlotTokens = 500, UsedTokens = 256, UsedPercent = 51.2, TokensPerSecond = 100, TaskId = 1, LastUpdated = now },
            new ContextWindowSample { ModelName = "localfoo:latest", SlotTokens = 500, UsedTokens = 100, UsedPercent = 20, TaskId = 2, LastUpdated = now.AddMinutes(-5) },
            new ContextWindowSample { ModelName = "localfoo2:latest", SlotTokens = 400, UsedTokens = 300, UsedPercent = 75, TokensPerSecond = 50, TaskId = 3, LastUpdated = now },
            new ContextWindowSample { ModelName = "localfoo2:latest", SlotTokens = 400, UsedTokens = 50, UsedPercent = 12.5, TaskId = 4, LastUpdated = now.AddMinutes(-5) },
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(samples);

        Assert.Equal(new[]
        {
            new MiniContextLine("localfoo2:latest - 300/400 - 75% - 50t/s", "localfoo2:latest - 300/400 - 75% - 50t/s"),
            new MiniContextLine("localfoo:latest - 256/500 - 51.2% - 100t/s", "localfoo:latest - 256/500 - 51.2% - 100t/s"),
        }, result);
    }

    [Fact]
    public void TopSelection_PrefersRecentActivity_OverHigherUsage()
    {
        // The reported bug: an idle high-usage task kept being shown over an
        // actively-generating lower-usage task on the same model.
        var now = DateTimeOffset.UtcNow;
        var samples = new[]
        {
            new ContextWindowSample { ModelName = "localfoo:latest", SlotTokens = 188416, UsedTokens = 150000, UsedPercent = 79.6, TokensPerSecond = 40, TaskId = 1, LastUpdated = now.AddMinutes(-30) },
            new ContextWindowSample { ModelName = "localfoo:latest", SlotTokens = 5000, UsedTokens = 1000, UsedPercent = 20, TokensPerSecond = 15, TaskId = 2, LastUpdated = now },
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(samples);

        Assert.Equal(new[] { new MiniContextLine("localfoo:latest - 1000/5000 - 20% - 15t/s", "localfoo:latest - 1000/5000 - 20% - 15t/s") }, result);
    }

    [Fact]
    public void UnlabeledSamples_EachAppearOnOwnLine_WithOwnStats()
    {
        var samples = new[]
        {
            new ContextWindowSample { SlotTokens = 1000, UsedTokens = 25, UsedPercent = 2.5, TokensPerSecond = 5, TaskId = 1 },
            new ContextWindowSample { SlotTokens = 2048, UsedTokens = 100, UsedPercent = 4.8828125, TaskId = 2 },
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(samples);

        Assert.Equal(new[]
        {
            new MiniContextLine("100/2048 - 4.9%", "100/2048 - 4.9%"),
            new MiniContextLine("25/1000 - 2.5% - 5t/s", "25/1000 - 2.5% - 5t/s"),
        }, result);
    }

    [Fact]
    public void LabeledLines_Then_UnlabeledLines()
    {
        var samples = new[]
        {
            new ContextWindowSample { ModelName = "localfoo:latest", SlotTokens = 1000, UsedTokens = 500, UsedPercent = 50, TaskId = 1 },
            new ContextWindowSample { SlotTokens = 2000, UsedTokens = 200, UsedPercent = 10, TokensPerSecond = 3, TaskId = 2 },
            new ContextWindowSample { ModelName = "other:latest", SlotTokens = 1000, UsedTokens = 250, UsedPercent = 25, TaskId = 3 },
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(samples);

        Assert.Equal(new[]
        {
            new MiniContextLine("localfoo:latest - 500/1000 - 50%", "localfoo:latest - 500/1000 - 50%"),
            new MiniContextLine("other:latest - 250/1000 - 25%", "other:latest - 250/1000 - 25%"),
            new MiniContextLine("200/2000 - 10% - 3t/s", "200/2000 - 10% - 3t/s"),
        }, result);
    }

    [Fact]
    public void SingleTask_LineShowsPercentAndSpeed()
    {
        var samples = new[]
        {
            new ContextWindowSample { ModelName = "localfoo:latest", SlotTokens = 512, UsedTokens = 64, UsedPercent = 12.5, TokensPerSecond = 12.3, TaskId = 1 },
        };

        var result = StatusTextHelper.BuildMiniModelContextLines(samples);

        Assert.Equal(new[] { new MiniContextLine("localfoo:latest - 64/512 - 12.5% - 12.3t/s", "localfoo:latest - 64/512 - 12.5% - 12.3t/s") }, result);
    }

    [Fact]
    public void LongModelName_IsMiddleEllipsized_To_FitFixedWindow()
    {
        const string name = "unsloth/DinV2-Gemini-2.5-Fast-RL-72B-8bit-GGUF-Q4_K_M";
        var samples = new[]
        {
            new ContextWindowSample { ModelName = name, SlotTokens = 16384, UsedTokens = 8942, UsedPercent = 54.578, TokensPerSecond = 41.2, TaskId = 1 },
        };

        var line = Assert.Single(StatusTextHelper.BuildMiniModelContextLines(samples));

        // The stats - the reason the line exists - stay intact at the end.
        Assert.EndsWith(" - 8942/16384 - 54.6% - 41.2t/s", line.Text);

        // Only the name is shortened: head and tail kept, exactly one ellipsis.
        var namePart = line.Text.Split(" - ")[0];
        Assert.StartsWith("unsloth/", namePart);
        Assert.EndsWith("-Q4_K_M", namePart);
        Assert.Equal(1, namePart.Length - namePart.Replace("\u2026", string.Empty).Length);

        // And the whole line fits the window's fixed budget.
        Assert.True(line.Text.Length <= 48, $"expected line within 48 chars, got {line.Text.Length}: '{line.Text}'");

        // The tooltip carries the full name plus stats.
        Assert.Equal($"{name} - 8942/16384 - 54.6% - 41.2t/s", line.FullText);
    }

    [Fact]
    public void ExtremeName_StillFits_AndKeepsIdentifyingEnds()
    {
        var name = "org/" + new string('m', 92) + "-8bit";
        var samples = new[]
        {
            new ContextWindowSample { ModelName = name, SlotTokens = 4567, UsedTokens = 123, UsedPercent = 2.693, TokensPerSecond = 1.2, TaskId = 1 },
        };

        var line = Assert.Single(StatusTextHelper.BuildMiniModelContextLines(samples));

        var namePart = line.Text.Split(" - ")[0];
        Assert.StartsWith("org/", namePart);
        Assert.EndsWith("-8bit", namePart);
        Assert.Equal(1, namePart.Length - namePart.Replace("\u2026", string.Empty).Length);
        Assert.True(line.Text.Length <= 48, $"expected line within 48 chars, got {line.Text.Length}: '{line.Text}'");
        Assert.EndsWith($"{name} - 123/4567 - 2.7% - 1.2t/s", line.FullText);
    }

    [Fact]
    public void UnlabeledLine_FullTextMatchesText()
    {
        var samples = new[]
        {
            new ContextWindowSample { SlotTokens = 512, UsedTokens = 64, UsedPercent = 12.5, TokensPerSecond = 12.3, TaskId = 1 },
        };

        var line = Assert.Single(StatusTextHelper.BuildMiniModelContextLines(samples));

        Assert.Equal("64/512 - 12.5% - 12.3t/s", line.Text);
        Assert.Equal(line.Text, line.FullText);
    }

    [Theory]
    [InlineData(OllamaMonitorState.HighUsage, "#FFEF4444")]
    [InlineData(OllamaMonitorState.ModelLoaded, "White")]
    [InlineData(OllamaMonitorState.Running, "White")]
    [InlineData(OllamaMonitorState.NotReachable, "White")]
    [InlineData(OllamaMonitorState.Error, "White")]
    public void StateForeground_OnlyHighUsage_IsRed(OllamaMonitorState state, string expected)
    {
        Assert.Equal(expected, StatusTextHelper.GetStateForeground(state));
    }

    [Fact]
    public void CpuLine_BothHalvesPresent_ShowsOllamaThenSystem()
    {
        Assert.Equal("CPU: 8.4% (Ollama) | 12% (System)", StatusTextHelper.BuildCpuLine(8.4, 12));
    }

    [Fact]
    public void CpuLine_OnlyOneHalf_OmitsTheMissingHalf()
    {
        Assert.Equal("CPU: 8.4% (Ollama)", StatusTextHelper.BuildCpuLine(8.4, null));
        Assert.Equal("CPU: 12% (System)", StatusTextHelper.BuildCpuLine(null, 12));
    }

    [Fact]
    public void CpuLine_NoData_FallsBackToUnavailable()
    {
        Assert.Equal("CPU: Unavailable", StatusTextHelper.BuildCpuLine(null, null));
    }

    [Fact]
    public void MemoryLine_BothHalvesPresent_ShowsBytesThenPercent()
    {
        Assert.Equal("Memory: 58.1 MB (Ollama) | 42% (System)", StatusTextHelper.BuildMemoryLine(60_922_266L, 42));
    }

    [Fact]
    public void MemoryLine_OnlyOneHalf_OmitsTheMissingHalf()
    {
        Assert.Equal("Memory: 58.1 MB (Ollama)", StatusTextHelper.BuildMemoryLine(60_922_266L, null));
        Assert.Equal("Memory: 42% (System)", StatusTextHelper.BuildMemoryLine(null, 42));
    }

    [Fact]
    public void MemoryLine_NoData_FallsBackToUnavailable()
    {
        Assert.Equal("Memory: Unavailable", StatusTextHelper.BuildMemoryLine(null, null));
    }
}
