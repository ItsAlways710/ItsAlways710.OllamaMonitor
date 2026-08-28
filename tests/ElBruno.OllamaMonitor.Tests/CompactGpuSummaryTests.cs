using ElBruno.OllamaMonitor.Helpers;
using ElBruno.OllamaMonitor.Models;

namespace ElBruno.OllamaMonitor.Tests;

/// <summary>
/// Tests the Mini Monitor GPU line (StatusTextHelper.BuildCompactGpuSummary): it must show
/// the usage percentage plus the used/total VRAM when both are known, keep the raw GPU
/// status string when the driver cannot be read, and degrade to "Unavailable" when no
/// GPU data exists at all.
/// </summary>
public sealed class CompactGpuSummaryTests
{
    // FormatBytes is 1024-based, so "18.5 GB" means 18.5 * 1024^3 bytes.
    private const long GiB = 1024L * 1024L * 1024L;
    private static ResourceSnapshot Snapshot(double? percent, long? usedBytes, long? totalBytes) => new()
    {
        GpuPercent = percent,
        VramUsedBytes = usedBytes,
        VramTotalBytes = totalBytes
    };

    [Fact]
    public void PercentAndVram_BothKnown_ShowsUsedOverTotal()
    {
        var result = StatusTextHelper.BuildCompactGpuSummary(Snapshot(47.3, 18 * GiB + GiB / 2, 24 * GiB));
        Assert.Equal("47.3% (18.5 GB / 24 GB)", result);
    }

    [Fact]
    public void PercentOnly_NoVram_UsageAlone()
    {
        var result = StatusTextHelper.BuildCompactGpuSummary(Snapshot(12.0, null, null));
        Assert.Equal("12%", result);
    }

    [Fact]
    public void PartialVram_OmitsThePair()
    {
        // One missing side must not yield "(x / Unavailable)".
        var result = StatusTextHelper.BuildCompactGpuSummary(Snapshot(12.0, 1 * GiB, null));
        Assert.Equal("12%", result);
    }

    [Fact]
    public void GpuStatusPresent_RawStatusWins()
    {
        var resources = new ResourceSnapshot
        {
            GpuPercent = 9.0,
            VramUsedBytes = 1 * GiB,
            VramTotalBytes = 4 * GiB,
            GpuStatus = "nvidia-smi unavailable"
        };
        Assert.Equal("nvidia-smi unavailable", StatusTextHelper.BuildCompactGpuSummary(resources));
    }

    [Fact]
    public void NoPercent_ReportsUnavailable() =>
        Assert.Equal("Unavailable", StatusTextHelper.BuildCompactGpuSummary(Snapshot(null, null, null)));

    [Fact]
    public void NullSnapshot_ReportsUnavailable() =>
        Assert.Equal("Unavailable", StatusTextHelper.BuildCompactGpuSummary(null));
}
