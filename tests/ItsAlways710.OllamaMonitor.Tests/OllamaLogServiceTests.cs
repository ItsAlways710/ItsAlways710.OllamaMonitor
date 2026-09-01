using ItsAlways710.OllamaMonitor.Diagnostics;
using ItsAlways710.OllamaMonitor.Services;

namespace ItsAlways710.OllamaMonitor.Tests;

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

/// <summary>
/// Tests for OllamaLogService log file path resolution between the known Ollama log locations.
/// Seam: OllamaLogService.ResolveLogPath (internal static).
/// </summary>
public sealed class OllamaLogPathResolutionTests
{
    [Fact]
    public void ResolveLogPath_BothExist_ReturnsMostRecentlyWritten()
    {
        using var dir = NewTempDir();

        var cli = Path.Combine(dir.Path, "cli.log");
        var desktop = Path.Combine(dir.Path, "desktop.log");
        File.WriteAllText(cli, "older");
        File.WriteAllText(desktop, "newer");
        File.SetLastWriteTimeUtc(cli, DateTimeOffset.UtcNow.AddMinutes(-5).UtcDateTime);

        Assert.Equal(desktop, OllamaLogService.ResolveLogPath([cli, desktop]));
    }

    [Fact]
    public void ResolveLogPath_FirstCandidateNewer_ReturnsFirstCandidate()
    {
        using var dir = NewTempDir();

        var cli = Path.Combine(dir.Path, "cli.log");
        var desktop = Path.Combine(dir.Path, "desktop.log");
        File.WriteAllText(desktop, "older");
        File.WriteAllText(cli, "newer");
        File.SetLastWriteTimeUtc(desktop, DateTimeOffset.UtcNow.AddMinutes(-5).UtcDateTime);

        Assert.Equal(cli, OllamaLogService.ResolveLogPath([cli, desktop]));
    }

    [Fact]
    public void ResolveLogPath_NoneExist_ReturnsFirstCandidate()
    {
        using var dir = NewTempDir();

        var first = Path.Combine(dir.Path, "does-not-exist-1.log");
        var second = Path.Combine(dir.Path, "does-not-exist-2.log");

        Assert.Equal(first, OllamaLogService.ResolveLogPath([first, second]));
    }

    [Fact]
    public void ResolveLogPath_EmptyCandidates_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => OllamaLogService.ResolveLogPath([]));
    }

    private static TempDir NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return new TempDir(path);
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir(string path) => Path = path;
        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

/// <summary>
/// Tests real file polling of OllamaLogService: appended lines are picked up by the poller,
/// lines that exist before Start are skipped (seek-to-end), and the ring buffer is fed.
/// </summary>
public sealed class OllamaLogFilePollingTests
{
    [Fact]
    public void Start_AppendsToFile_NewLineReceived_PreexistingSkipped()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(logDir);
        var logFile = Path.Combine(logDir, "server.log");
        File.WriteAllText(logFile, "seeded line before start\n");

        try
        {
            using var sut = new OllamaLogService(new DiagnosticsLogService(logDir), logFile);
            using var caught = new ManualResetEventSlim(false);
            string? received = null;
            sut.LogLineReceived += line =>
            {
                received = line;
                caught.Set();
            };

            sut.Start();

            const string appendedLine = "slot print_timing: id  0 | task 1 | n_gen = 10, tg = 12.5 t/s";
            File.AppendAllText(logFile, "\r\n" + appendedLine + "\r\n");

            Assert.True(caught.Wait(TimeSpan.FromSeconds(10)), "Poller did not deliver the appended line within 10 s.");

            Assert.Equal(appendedLine, received);
            var recent = sut.RecentLines;
            Assert.Contains(appendedLine, recent);
            Assert.DoesNotContain("seeded line before start", recent);
        }
        finally
        {
            Directory.Delete(logDir, recursive: true);
        }
    }
}
