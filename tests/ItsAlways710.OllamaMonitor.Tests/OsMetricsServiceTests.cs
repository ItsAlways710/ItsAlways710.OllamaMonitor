using ItsAlways710.OllamaMonitor.Diagnostics;
using ItsAlways710.OllamaMonitor.Services;

namespace ItsAlways710.OllamaMonitor.Tests;

/// <summary>
/// Tests the whole-machine sampler against the real Windows APIs: the first
/// CPU sample has no prior state (null, then a bounded percentage afterwards),
/// and memory reports positive totals with a consistent used/total percentage.
/// </summary>
public sealed class OsMetricsServiceTests
{
    private static OsMetricsService CreateService() =>
        new(new DiagnosticsLogService(Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests")));

    [Fact]
    public async Task FirstCall_CpuPercentIsNull_MemoryIsAvailable()
    {
        var service = CreateService();

        var result = await service.GetMetricsAsync(CancellationToken.None);

        Assert.Null(result.CpuPercent);
        Assert.NotNull(result.MemoryTotalBytes);
        Assert.NotNull(result.MemoryUsedBytes);
        Assert.NotNull(result.MemoryPercent);
    }

    [Fact]
    public async Task SecondCall_CpuPercentIsBounded()
    {
        var service = CreateService();

        var first = await service.GetMetricsAsync(CancellationToken.None);
        await Task.Delay(50);
        var second = await service.GetMetricsAsync(CancellationToken.None);

        Assert.Null(first.CpuPercent);
        Assert.NotNull(second.CpuPercent);
        Assert.InRange(second.CpuPercent.Value, 0, 100);
    }

    [Fact]
    public async Task MemoryUsage_UsedIsPositiveAndPercentIsConsistent()
    {
        var service = CreateService();

        var result = await service.GetMetricsAsync(CancellationToken.None);

        var total = result.MemoryTotalBytes!.Value;
        var used = result.MemoryUsedBytes!.Value;
        var percent = result.MemoryPercent!.Value;

        Assert.True(total > 0, "physical RAM total must be positive");
        Assert.True(used > 0, "used memory must be positive");
        Assert.True(used <= total, "used memory cannot exceed total");
        Assert.InRange(percent, 0, 100);
        Assert.Equal(Math.Round(used * 100d / total, 1), Math.Round(percent, 1), 1);
    }
}
