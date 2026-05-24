using ElBruno.OllamaMonitor.Helpers;

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
}
