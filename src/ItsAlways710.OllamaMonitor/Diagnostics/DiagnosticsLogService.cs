namespace ItsAlways710.OllamaMonitor.Diagnostics;

public sealed class DiagnosticsLogService
{
    private readonly string _logDirectoryPath;
    private readonly Lock _syncRoot = new();

    public DiagnosticsLogService(string logDirectoryPath)
    {
        _logDirectoryPath = logDirectoryPath;
    }

    /// <summary>
    /// Whether diagnostic-level (<see cref="WriteVerbose"/>) logging is enabled. Off by
    /// default; the app sets it from <c>AppSettings.EnableVerboseLogging</c> at startup
    /// and on each refresh tick, so the Settings toggle applies live.
    /// </summary>
    public bool IsVerboseEnabled { get; set; }

    public void WriteInfo(string message) => Write("INFO", message);

    public void WriteWarning(string message) => Write("WARN", message);

    public void WriteError(string message, Exception? exception = null)
    {
        var fullMessage = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Write("ERROR", fullMessage);
    }

    /// <summary>
    /// Writes a diagnostic-level log line only when <see cref="IsVerboseEnabled"/> —
    /// otherwise a complete no-op (no file created, no line appended).
    /// Call sites whose detail requires expensive capture (process lookups, foreign
    /// window identity) should check <see cref="IsVerboseEnabled"/> before that capture,
    /// so a disabled setting skips the capture work, not just the write.
    /// </summary>
    public void WriteVerbose(string message)
    {
        if (!IsVerboseEnabled)
        {
            return;
        }

        Write("VERBOSE", message);
    }

    private void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(_logDirectoryPath);
            var logPath = Path.Combine(_logDirectoryPath, $"{DateTime.UtcNow:yyyyMMdd}.log");
            var line = $"[{DateTimeOffset.Now:O}] [{level}] {message}{Environment.NewLine}";

            lock (_syncRoot)
            {
                File.AppendAllText(logPath, line);
            }
        }
        catch
        {
            // Best-effort logging only.
        }
    }
}
