using ElBruno.OllamaMonitor.Helpers;
using ElBruno.OllamaMonitor.Models;

namespace ElBruno.OllamaMonitor.Tests;

/// <summary>
/// Tests that StatusTextHelper builds Mini Monitor context lines named after each
/// attributed model (via the sample's pre-resolved ModelName), that at most one task
/// per model is shown (the most recently active one - recency of log activity
/// outranks magnitude of usage), and that unlabeled tasks (not yet
/// attributed - a transient state) keep their own line with their own stats.
/// </summary>
public sealed class StatusTextHelperTests
{
    [Fact]
    public void EmptySamples_ReturnsUnavailable()
    {
        var result = StatusTextHelper.BuildMiniModelContextLines(Array.Empty<ContextWindowSample>());
        Assert.Equal(new[] { "Context: Unavailable" }, result);
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
            "localfoo2:latest - 800/1024 - 78.1%",
            "localfoo:latest - 200/1000 - 20% - 10t/s",
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
            "localfoo2:latest - 300/400 - 75% - 50t/s",
            "localfoo:latest - 256/500 - 51.2% - 100t/s",
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

        Assert.Equal(new[] { "localfoo:latest - 1000/5000 - 20% - 15t/s" }, result);
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
            "100/2048 - 4.9%",
            "25/1000 - 2.5% - 5t/s",
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
            "localfoo:latest - 500/1000 - 50%",
            "other:latest - 250/1000 - 25%",
            "200/2000 - 10% - 3t/s",
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

        Assert.Equal(new[] { "localfoo:latest - 64/512 - 12.5% - 12.3t/s" }, result);
    }
}
