using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Services;

namespace ElBruno.OllamaMonitor.Tests;

/// <summary>
/// Tests for OllamaLogService ring buffer and event behavior.
/// Seam: OllamaLogService.OnOwnedProcessOutput (internal, accessible via InternalsVisibleTo).
/// No real Ollama process or file I/O is used; all lines are injected directly.
/// </summary>
public sealed class OllamaLogServiceTests : IDisposable
{
    private readonly OllamaLogService _sut;

    public OllamaLogServiceTests()
    {
        var diagnostics = new DiagnosticsLogService(
            Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests"));
        _sut = new OllamaLogService(diagnostics);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void RecentLines_IsEmpty_OnCreation()
    {
        Assert.Empty(_sut.RecentLines);
    }

    [Fact]
    public void RingBuffer_SingleLine_ContainsThatLine()
    {
        _sut.OnOwnedProcessOutput("line1");

        var lines = _sut.RecentLines;
        Assert.Single(lines);
        Assert.Equal("line1", lines[0]);
    }

    [Fact]
    public void RingBuffer_FiveLines_ContainsAllFive()
    {
        for (var i = 1; i <= 5; i++)
            _sut.OnOwnedProcessOutput($"line{i}");

        var lines = _sut.RecentLines;
        Assert.Equal(5, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line5", lines[4]);
    }

    [Fact]
    public void RingBuffer_ExceedsCapacity_RetainsLastFiveOnly()
    {
        for (var i = 1; i <= 7; i++)
            _sut.OnOwnedProcessOutput($"line{i}");

        var lines = _sut.RecentLines;
        Assert.Equal(5, lines.Count);
    }

    [Fact]
    public void RingBuffer_ExceedsCapacity_OldestLinesDropped()
    {
        for (var i = 1; i <= 7; i++)
            _sut.OnOwnedProcessOutput($"line{i}");

        var lines = _sut.RecentLines;
        // lines 1 and 2 should be evicted; lines 3–7 remain
        Assert.DoesNotContain("line1", lines);
        Assert.DoesNotContain("line2", lines);
    }

    [Fact]
    public void RingBuffer_ExceedsCapacity_PreservesInsertionOrder()
    {
        for (var i = 1; i <= 7; i++)
            _sut.OnOwnedProcessOutput($"line{i}");

        var lines = _sut.RecentLines;
        // Expect the last 5, in insertion order
        Assert.Equal(["line3", "line4", "line5", "line6", "line7"], lines);
    }

    [Fact]
    public void LogLineReceived_FiredForEachLine()
    {
        var received = new List<string>();
        _sut.LogLineReceived += line => received.Add(line);

        _sut.OnOwnedProcessOutput("a");
        _sut.OnOwnedProcessOutput("b");
        _sut.OnOwnedProcessOutput("c");

        Assert.Equal(["a", "b", "c"], received);
    }

    [Fact]
    public void LogLineReceived_FiredWithCorrectLineValue()
    {
        string? captured = null;
        _sut.LogLineReceived += line => captured = line;

        _sut.OnOwnedProcessOutput("hello ollama");

        Assert.Equal("hello ollama", captured);
    }

    [Fact]
    public void OnOwnedProcessOutput_NullLine_IsIgnored()
    {
        _sut.OnOwnedProcessOutput(null);

        Assert.Empty(_sut.RecentLines);
    }

    [Fact]
    public void OnOwnedProcessOutput_EmptyLine_IsIgnored()
    {
        _sut.OnOwnedProcessOutput(string.Empty);

        Assert.Empty(_sut.RecentLines);
    }

    [Fact]
    public void RecentLines_ReturnsSnapshot_NotLiveReference()
    {
        _sut.OnOwnedProcessOutput("snap");
        var snapshot = _sut.RecentLines;

        _sut.OnOwnedProcessOutput("new");

        // The snapshot captured before the second append must be unaffected
        Assert.Single(snapshot);
        Assert.Equal("snap", snapshot[0]);
    }

    [Fact]
    public void LogLineReceived_ExceptionInHandler_DoesNotPropagateToAppendLine()
    {
        _sut.LogLineReceived += _ => throw new InvalidOperationException("bad subscriber");

        // Should not throw; OllamaLogService catches handler exceptions
        var ex = Record.Exception(() => _sut.OnOwnedProcessOutput("safe"));
        Assert.Null(ex);

        // Line must still have been appended despite the bad subscriber
        Assert.Single(_sut.RecentLines);
        Assert.Equal("safe", _sut.RecentLines[0]);
    }
}
