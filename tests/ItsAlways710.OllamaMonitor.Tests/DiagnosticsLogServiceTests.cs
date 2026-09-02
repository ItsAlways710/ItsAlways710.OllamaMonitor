using ItsAlways710.OllamaMonitor.Diagnostics;

namespace ItsAlways710.OllamaMonitor.Tests;

/// <summary>
/// Verbose-gating contract for DiagnosticsLogService: WriteVerbose is a complete
/// no-op (not even the log file is created) while IsVerboseEnabled is off, writes
/// a [VERBOSE] line when on, follows the flag at runtime, and never affects the
/// regular INFO/WARN levels.
/// </summary>
public sealed class DiagnosticsLogServiceTests
{
    private static string NewLogDirectory()
    {
        // Unique per test run so parallel test classes cannot cross-assert on files.
        return Path.Combine(
            Path.GetTempPath(),
            "ItsAlways710.OllamaMonitor.Tests",
            "Diagnostics-" + Guid.NewGuid().ToString("N"));
    }

    private static string TodayLog(string directory) =>
        Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMdd}.log");

    [Fact]
    public void WriteVerbose_FlagOffByDefault_WritesNothing()
    {
        string dir = NewLogDirectory();
        var sut = new DiagnosticsLogService(dir);

        sut.WriteVerbose("diagnostic detail");

        Assert.False(File.Exists(TodayLog(dir)), "a verbose write while the flag is off must not even create the log file");
    }

    [Fact]
    public void WriteVerbose_FlagOn_WritesVerboseLevelLine()
    {
        string dir = NewLogDirectory();
        var sut = new DiagnosticsLogService(dir);
        sut.IsVerboseEnabled = true;

        sut.WriteVerbose("diagnostic detail");

        string content = File.ReadAllText(TodayLog(dir));
        Assert.Contains("[VERBOSE]", content);
        Assert.Contains("diagnostic detail", content);
    }

    [Fact]
    public void RegularLevels_StillWrite_WhenFlagOffByDefault()
    {
        string dir = NewLogDirectory();
        var sut = new DiagnosticsLogService(dir);

        sut.WriteInfo("regular info");
        sut.WriteWarning("regular warning");

        string content = File.ReadAllText(TodayLog(dir));
        Assert.Contains("[INFO]", content);
        Assert.Contains("[WARN]", content);
        Assert.DoesNotContain("[VERBOSE]", content);
    }

    [Fact]
    public void WriteVerbose_FollowsFlag_ChangesAtRuntime()
    {
        string dir = NewLogDirectory();
        var sut = new DiagnosticsLogService(dir);

        sut.WriteVerbose("while-off-1");
        Assert.False(File.Exists(TodayLog(dir)));

        sut.IsVerboseEnabled = true;
        sut.WriteVerbose("while-on");
        string content = File.ReadAllText(TodayLog(dir));
        Assert.Contains("while-on", content);
        Assert.DoesNotContain("while-off-1", content);

        sut.IsVerboseEnabled = false;
        sut.WriteVerbose("while-off-2");
        content = File.ReadAllText(TodayLog(dir));
        Assert.DoesNotContain("while-off-2", content);
    }
}
